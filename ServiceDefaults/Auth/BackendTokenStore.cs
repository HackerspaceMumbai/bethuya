using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ServiceDefaults.Auth;

/// <summary>Claim constants used to correlate authenticated cookies with server-side backend token sessions.</summary>
public static class BackendTokenClaimTypes
{
    /// <summary>Opaque session identifier stored on the user's cookie principal to look up backend OIDC tokens.</summary>
    public const string TokenSessionId = "bethuya:token-session";
}

/// <summary>Server-side OIDC token entry associated with an authenticated Web session.</summary>
public sealed record BackendTokenEntry(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt,
    string? TokenEndpoint,
    string? ClientId,
    string? ClientSecret);

/// <summary>
/// Server-side token store for Web-to-Backend bearer propagation across SSR requests and
/// Blazor InteractiveServer SignalR circuits, including refresh-token renewal.
/// </summary>
public interface IBackendTokenStore
{
    /// <summary>Stores or replaces the token entry for <paramref name="sessionId"/>.</summary>
    void Store(string sessionId, BackendTokenEntry entry);

    /// <summary>
    /// Returns a valid access token for <paramref name="sessionId"/>, refreshing it via the OIDC
    /// token endpoint when close to expiry, or <see langword="null"/> if unavailable.
    /// </summary>
    ValueTask<string?> GetValidAccessTokenAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Removes any stored tokens for <paramref name="sessionId"/>.</summary>
    void Remove(string sessionId);
}

internal sealed partial class InMemoryBackendTokenStore(
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    ILogger<InMemoryBackendTokenStore> logger) : IBackendTokenStore, IDisposable
{
    internal const string RefreshHttpClientName = "BethuyaOidcTokenRefresh";
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, SessionSlot> _sessions = new(StringComparer.Ordinal);
    private bool _disposed;

    public void Store(string sessionId, BackendTokenEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(entry);

        PruneUnrefreshableExpiredEntries();
        var slot = _sessions.GetOrAdd(sessionId, static _ => new SessionSlot());
        slot.Entry = entry;
    }

    public async ValueTask<string?> GetValidAccessTokenAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var slot))
        {
            return null;
        }

        var current = slot.Entry;
        if (current is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (current.ExpiresAt > now.Add(RefreshSkew))
        {
            return current.AccessToken;
        }

        if (string.IsNullOrWhiteSpace(current.RefreshToken) || string.IsNullOrWhiteSpace(current.TokenEndpoint))
        {
            if (current.ExpiresAt <= now)
            {
                Remove(sessionId);
                return null;
            }

            return current.AccessToken;
        }

        await slot.RefreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            current = slot.Entry;
            if (current is null)
            {
                return null;
            }

            now = timeProvider.GetUtcNow();
            if (current.ExpiresAt > now.Add(RefreshSkew))
            {
                return current.AccessToken;
            }

            var refreshed = await TryRefreshAsync(sessionId, current, now, cancellationToken).ConfigureAwait(false);
            if (refreshed is null)
            {
                if (current.ExpiresAt <= now)
                {
                    Remove(sessionId);
                    return null;
                }

                return current.AccessToken;
            }

            slot.Entry = refreshed;
            return refreshed.AccessToken;
        }
        finally
        {
            slot.RefreshLock.Release();
        }
    }

    public void Remove(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        if (_sessions.TryRemove(sessionId, out var slot))
        {
            slot.Entry = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var entry in _sessions.Values)
        {
            entry.RefreshLock.Dispose();
        }

        _sessions.Clear();
    }

    private async Task<BackendTokenEntry?> TryRefreshAsync(
        string sessionId,
        BackendTokenEntry current,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(current.RefreshToken)
            || string.IsNullOrWhiteSpace(current.TokenEndpoint)
            || !Uri.TryCreate(current.TokenEndpoint, UriKind.Absolute, out var tokenEndpointUri))
        {
            return null;
        }

        var formFields = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "refresh_token"),
            new("refresh_token", current.RefreshToken)
        };

        if (!string.IsNullOrWhiteSpace(current.ClientId))
        {
            formFields.Add(new("client_id", current.ClientId));
        }

        if (!string.IsNullOrWhiteSpace(current.ClientSecret))
        {
            formFields.Add(new("client_secret", current.ClientSecret));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpointUri)
        {
            Content = new FormUrlEncodedContent(formFields)
        };

        try
        {
            var client = httpClientFactory.CreateClient(RefreshHttpClientName);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogRefreshHttpFailure(logger, sessionId, (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<OidcRefreshResponse>(
                stream,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
            {
                LogRefreshPayloadInvalid(logger, sessionId);
                return null;
            }

            var expiresInSeconds = payload.ExpiresIn is > 0 ? payload.ExpiresIn.Value : 300;
            return current with
            {
                AccessToken = payload.AccessToken,
                RefreshToken = string.IsNullOrWhiteSpace(payload.RefreshToken)
                    ? current.RefreshToken
                    : payload.RefreshToken,
                ExpiresAt = now.AddSeconds(expiresInSeconds)
            };
        }
        catch (HttpRequestException ex)
        {
            LogRefreshException(logger, sessionId, ex);
            return null;
        }
        catch (JsonException ex)
        {
            LogRefreshException(logger, sessionId, ex);
            return null;
        }
    }

    private void PruneUnrefreshableExpiredEntries()
    {
        if (_sessions.Count < 128)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var pair in _sessions)
        {
            var entry = pair.Value.Entry;
            if (entry is not null
                && entry.ExpiresAt <= now
                && string.IsNullOrWhiteSpace(entry.RefreshToken))
            {
                _sessions.TryRemove(pair.Key, out _);
            }
        }
    }

    internal static DateTimeOffset ResolveExpiry(string? expiresInValue, TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        if (double.TryParse(expiresInValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            && seconds > 0)
        {
            return now.AddSeconds(seconds);
        }

        return now.AddMinutes(5);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to refresh backend access token for session {SessionId}; token endpoint returned status {StatusCode}.")]
    private static partial void LogRefreshHttpFailure(ILogger logger, string sessionId, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token endpoint returned an invalid refresh payload for session {SessionId}.")]
    private static partial void LogRefreshPayloadInvalid(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error refreshing backend access token for session {SessionId}.")]
    private static partial void LogRefreshException(ILogger logger, string sessionId, Exception exception);

    private sealed class SessionSlot
    {
        public SemaphoreSlim RefreshLock { get; } = new(1, 1);
        public BackendTokenEntry? Entry { get; set; }
    }

    private sealed class OidcRefreshResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public double? ExpiresIn { get; set; }
    }
}
