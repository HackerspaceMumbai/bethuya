using System.Globalization;
using System.Text.Json;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.AI.CopilotSdk;

/// <summary>Configuration for the Copilot SDK date recommendation service.</summary>
public sealed class CopilotSdkOptions
{
    public const string SectionName = "CopilotSdk";

    /// <summary>GitHub personal access token for authenticating with the Copilot CLI.</summary>
    public string? GitHubToken { get; set; }

    /// <summary>Model to use for recommendations (default: "gpt-4.1").</summary>
    public string Model { get; set; } = "gpt-4.1";
}

/// <summary>
/// Recommends optimal event dates using the GitHub Copilot SDK.
/// Manages a singleton <see cref="CopilotClient"/> and creates sessions per request.
/// </summary>
public sealed partial class DateRecommendationService : IDateRecommendationService, IAsyncDisposable
{
    private readonly IOptions<CopilotSdkOptions> _options;
    private readonly ILogger<DateRecommendationService> _logger;
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private CopilotClient? _client;
    private bool _disposed;

    public DateRecommendationService(
        IOptions<CopilotSdkOptions> options,
        ILogger<DateRecommendationService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<DateRecommendation> RecommendAsync(
        DateRecommendationContext context,
        CancellationToken ct = default)
    {
        var client = await GetOrCreateClientAsync(ct);

        var userPrompt = BuildUserPrompt(context);
        var title = context.Title ?? "(untitled)";

        LogRequestingRecommendation(_logger, title);

        var responseBuilder = new System.Text.StringBuilder();
        var done = new TaskCompletionSource();

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = _options.Value.Model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = CommunityEventPatterns.GetSystemPrompt()
            },
            OnPermissionRequest = PermissionHandler.ApproveAll
        }, ct);

        session.On(evt =>
        {
            if (evt is AssistantMessageEvent msg && msg.Data.Content is not null)
            {
                responseBuilder.Append(msg.Data.Content);
            }
            else if (evt is SessionIdleEvent)
            {
                done.TrySetResult();
            }
        });

        await session.SendAsync(new MessageOptions { Prompt = userPrompt }, ct);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        linked.Token.Register(() => done.TrySetCanceled());

        await done.Task;

        var responseText = responseBuilder.ToString();
        LogRawResponse(_logger, responseText);

        return ParseResponse(responseText);
    }

    private static string BuildUserPrompt(DateRecommendationContext context)
    {
        var parts = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"Today's date: {DateTime.UtcNow:yyyy-MM-dd}")
        };

        if (!string.IsNullOrWhiteSpace(context.Title))
            parts.Add($"Event title: {context.Title}");
        if (!string.IsNullOrWhiteSpace(context.Type))
            parts.Add($"Event type: {context.Type}");
        if (!string.IsNullOrWhiteSpace(context.Description))
            parts.Add($"Description: {context.Description}");
        if (!string.IsNullOrWhiteSpace(context.Location))
            parts.Add($"Location: {context.Location}");
        if (context.Capacity.HasValue)
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"Expected capacity: {context.Capacity.Value}"));

        parts.Add("Recommend the optimal start date/time and end date/time for this event.");

        return string.Join('\n', parts);
    }

    internal static DateRecommendation ParseResponse(string responseText)
    {
        var text = responseText.Trim();

        // Strip markdown JSON fencing if present
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0) text = text[(firstNewline + 1)..];
            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0) text = text[..lastFence];
            text = text.Trim();
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        var startDate = DateOnly.Parse(root.GetProperty("startDate").GetString()!, CultureInfo.InvariantCulture);
        var startTime = TimeOnly.Parse(root.GetProperty("startTime").GetString()!, CultureInfo.InvariantCulture);
        var endDate = DateOnly.Parse(root.GetProperty("endDate").GetString()!, CultureInfo.InvariantCulture);
        var endTime = TimeOnly.Parse(root.GetProperty("endTime").GetString()!, CultureInfo.InvariantCulture);
        var reasoning = root.TryGetProperty("reasoning", out var r) ? r.GetString() : null;

        return new DateRecommendation(startDate, startTime, endDate, endTime, reasoning);
    }

    private async Task<CopilotClient> GetOrCreateClientAsync(CancellationToken ct)
    {
        if (_client is not null) return _client;

        await _clientLock.WaitAsync(ct);
        try
        {
            if (_client is null)
            {
                var opts = new CopilotClientOptions
                {
                    AutoStart = true,
                    UseStdio = true
                };

                if (!string.IsNullOrWhiteSpace(_options.Value.GitHubToken))
                {
                    opts.GitHubToken = _options.Value.GitHubToken;
                }

                _client = new CopilotClient(opts);
                await _client.StartAsync(ct);

                LogClientStarted(_logger);
            }

            return _client!;
        }
        catch (Exception ex)
        {
            LogClientStartFailed(_logger, ex);
            throw;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_client is not null)
        {
            try
            {
                await _client.StopAsync();
            }
            catch (Exception ex)
            {
                LogClientStopError(_logger, ex);
            }
        }

        _clientLock.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Requesting date recommendation for event: {Title}")]
    private static partial void LogRequestingRecommendation(ILogger logger, string title);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Copilot SDK raw response: {Response}")]
    private static partial void LogRawResponse(ILogger logger, string response);

    [LoggerMessage(Level = LogLevel.Information, Message = "CopilotClient started successfully")]
    private static partial void LogClientStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start CopilotClient")]
    private static partial void LogClientStartFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error stopping CopilotClient")]
    private static partial void LogClientStopError(ILogger logger, Exception ex);
}
