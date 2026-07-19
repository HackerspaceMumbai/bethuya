using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Infrastructure.Services;

public sealed partial class SessionizeService(
    HttpClient httpClient,
    IOptions<SessionizeOptions> options,
    ILogger<SessionizeService> logger)
    : ISessionizeService
{
    public async Task<IReadOnlyList<NormalizedSession>> GetSessionsAsync(string sessionizeEventId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionizeEventId);

        var settings = options.Value;
        var path = settings.SessionPathTemplate.Replace("{eventId}", Uri.EscapeDataString(sessionizeEventId), StringComparison.Ordinal);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (!string.IsNullOrWhiteSpace(settings.ApiToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiToken);
        }

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var sessions = await response.Content.ReadFromJsonAsync<List<SessionizeSessionDto>>(cancellationToken: ct) ?? [];
        var speakerLookup = await GetSpeakerLookupAsync(sessionizeEventId, ct);
        LogSessionizeSessionCount(logger, sessionizeEventId, sessions.Count);

        return sessions.Select(session => ToNormalizedSession(session, speakerLookup)).ToArray();
    }

    private async Task<IReadOnlyDictionary<string, SessionizeSpeakerDto>> GetSpeakerLookupAsync(
        string sessionizeEventId,
        CancellationToken ct)
    {
        var settings = options.Value;
        var path = settings.SpeakerPathTemplate.Replace("{eventId}", Uri.EscapeDataString(sessionizeEventId), StringComparison.Ordinal);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (!string.IsNullOrWhiteSpace(settings.ApiToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiToken);
        }

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var speakers = await response.Content.ReadFromJsonAsync<List<SessionizeSpeakerDto>>(cancellationToken: ct) ?? [];
        return speakers
            .Where(s => s.Id.ValueKind != JsonValueKind.Undefined)
            .ToDictionary(s => ToId(s.Id), StringComparer.Ordinal);
    }

    private static NormalizedSession ToNormalizedSession(
        SessionizeSessionDto session,
        IReadOnlyDictionary<string, SessionizeSpeakerDto> speakerLookup)
    {
        var speakers = GetSessionSpeakers(session.Speakers, speakerLookup)
            .Select(s => new NormalizedSpeaker(
                s.FullName ?? s.Name ?? "Unknown speaker",
                GitHubHandle: s.GitHub,
                TwitterHandle: s.Twitter,
                AvatarUrl: s.ProfilePicture))
            .ToArray() ?? [];

        return new NormalizedSession(
            session.Title ?? "Untitled session",
            session.Description,
            speakers,
            SessionSource.Sessionize,
            session.Id?.ToString(),
            session.StartsAt,
            session.EndsAt is not null && session.StartsAt is not null
                ? session.EndsAt.Value - session.StartsAt.Value
                : session.DurationMinutes is > 0
                    ? TimeSpan.FromMinutes(session.DurationMinutes.Value)
                    : null);
    }

    private static List<SessionizeSpeakerDto> GetSessionSpeakers(
        JsonElement speakers,
        IReadOnlyDictionary<string, SessionizeSpeakerDto> speakerLookup)
    {
        if (speakers.ValueKind != JsonValueKind.Array)
        {
                return [];
        }

        List<SessionizeSpeakerDto> result = [];
        foreach (var speakerElement in speakers.EnumerateArray())
        {
                if (speakerElement.ValueKind is JsonValueKind.String or JsonValueKind.Number
                    && speakerLookup.TryGetValue(ToId(speakerElement), out var speaker))
                {
                    result.Add(speaker);
                }
                else if (speakerElement.ValueKind == JsonValueKind.Object)
                {
                    var embeddedSpeaker = speakerElement.Deserialize<SessionizeSpeakerDto>();
                    if (embeddedSpeaker is not null)
                    {
                        result.Add(embeddedSpeaker);
                    }
                }
        }

        return result;
    }

    private static string ToId(JsonElement value)
        => value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : value.GetRawText();

    [LoggerMessage(
        EventId = 30,
        Level = LogLevel.Information,
        Message = "Retrieved {SessionCount} sessions from Sessionize event {SessionizeEventId}.")]
    private static partial void LogSessionizeSessionCount(
        ILogger logger,
        string sessionizeEventId,
        int sessionCount);

    private sealed record SessionizeSessionDto(
        [property: JsonPropertyName("id")] object? Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("startsAt")] DateTimeOffset? StartsAt,
        [property: JsonPropertyName("endsAt")] DateTimeOffset? EndsAt,
        [property: JsonPropertyName("duration")] int? DurationMinutes,
        [property: JsonPropertyName("speakers")] JsonElement Speakers);

    private sealed record SessionizeSpeakerDto(
        [property: JsonPropertyName("id")] JsonElement Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("fullName")] string? FullName,
        [property: JsonPropertyName("profilePicture")] string? ProfilePicture,
        [property: JsonPropertyName("github")] string? GitHub,
        [property: JsonPropertyName("twitter")] string? Twitter);
}
