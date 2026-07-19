using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

public sealed partial class EventLifecycleOrchestrator(
    BethuyaDbContext dbContext,
    IGitHubEventRepository gitHubEventRepository,
    ILumaRegistrationService lumaRegistrationService,
    ITeamsNotificationService teamsNotificationService,
    IWebCacheInvalidationService cacheInvalidationService)
    : IEventLifecycleOrchestrator
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new() { WriteIndented = true };

    public async Task<EventLifecycleOperationResult> TransitionAsync(
        Guid eventId,
        MeetupLifecycleState targetState,
        string actor,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        var occurredAt = DateTimeOffset.UtcNow;

        var evt = await dbContext.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw new KeyNotFoundException($"Event {eventId} was not found.");

        evt.TransitionLifecycleTo(targetState, occurredAt);
        await dbContext.SaveChangesAsync(ct);

        return ToResult(evt, $"Lifecycle transitioned to {targetState} by {actor}.");
    }

    public async Task<EventLifecycleOperationResult> PublishAsync(
        Guid eventId,
        string actor,
        string? registrationUrl,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        var occurredAt = DateTimeOffset.UtcNow;
        var evt = await LoadEventAggregateAsync(eventId, ct);

        if (evt.LifecycleState != MeetupLifecycleState.Published)
        {
            evt.TransitionLifecycleTo(MeetupLifecycleState.Published, occurredAt);
        }

        evt.RegistrationUrl = NormalizeHttpsUrl(registrationUrl)
            ?? await lumaRegistrationService.GetRegistrationUrlAsync(eventId, ct)
            ?? evt.RegistrationUrl;

        var artifact = CreatePublicationArtifact(evt);
        var publication = await gitHubEventRepository.PublishEventAsync(
            new EventPublicationRequest(
                evt.Id,
                evt.Title,
                artifact.FolderPath,
                artifact.ReadmeMarkdown,
                artifact.MetadataJson,
                CreateIdempotencyKey(evt.Id, artifact.MetadataJson)),
            ct);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            evt.GitHubFolderUrl = publication.FolderUrl;
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        return ToResult(evt, "Event published.");
    }

    public async Task<EventLifecycleOperationResult> AlterScheduleAsync(
        Guid eventId,
        string actor,
        string reason,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var occurredAt = DateTimeOffset.UtcNow;
        EventLifecycleOperationResult? result = null;
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            var evt = await LoadEventAggregateAsync(eventId, ct);
            if (evt.LifecycleState != MeetupLifecycleState.ScheduleAltered)
            {
                evt.TransitionLifecycleTo(MeetupLifecycleState.ScheduleAltered, occurredAt);
            }

            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            result = ToResult(evt, $"Schedule altered by {actor}: {reason}");
        });

        await cacheInvalidationService.InvalidateEventAsync(eventId, ct);
        await teamsNotificationService.NotifyScheduleChangedAsync(eventId, reason, ct);
        return result ?? throw new InvalidOperationException("Schedule alteration did not produce a result.");
    }

    public async Task<EventLifecycleOperationResult> CompleteAsync(
        Guid eventId,
        string actor,
        DateTimeOffset? assetDueAt,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        var occurredAt = DateTimeOffset.UtcNow;
        var evt = await LoadEventAggregateAsync(eventId, ct);

        evt.TransitionLifecycleTo(MeetupLifecycleState.Completed, occurredAt);
        foreach (var session in evt.Agenda?.Sessions ?? [])
        {
            session.MarkPendingUpload(assetDueAt);
        }

        await dbContext.SaveChangesAsync(ct);
        return ToResult(evt, "Event completed; session assets are pending upload.");
    }

    public async Task<EventLifecycleOperationResult> ArchiveAsync(
        Guid eventId,
        string actor,
        bool overrideMissingAssets,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        var evt = await LoadEventAggregateAsync(eventId, ct);
        var missingAssets = evt.Agenda?.Sessions.Any(s => s.MissingRequiredAssets().Count > 0) == true;

        if (missingAssets && !overrideMissingAssets)
        {
            throw new InvalidOperationException("Event cannot be archived while required session assets are missing.");
        }

        evt.TransitionLifecycleTo(MeetupLifecycleState.Archived, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(ct);
        return ToResult(evt, "Event archived.");
    }

    private async Task<Event> LoadEventAggregateAsync(Guid eventId, CancellationToken ct)
        => await dbContext.Events
            .Include(e => e.Agenda)
            .ThenInclude(a => a!.Sessions)
            .FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw new KeyNotFoundException($"Event {eventId} was not found.");

    private static EventPublicationArtifact CreatePublicationArtifact(Event evt)
    {
        var folderPath = $"events/{evt.StartDate.Year.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{Slugify(evt.Title)}-{evt.Id:N}";
        var sessions = evt.Agenda?.Sessions.OrderBy(s => s.Order).ToArray() ?? [];
        var readme = CreateReadme(evt, sessions);
        var metadata = JsonSerializer.Serialize(
            new
            {
                evt.Id,
                evt.Title,
                evt.Description,
                lifecycleState = evt.LifecycleState.ToString(),
                evt.StartDate,
                evt.EndDate,
                evt.Location,
                evt.RegistrationUrl,
                sessions = sessions.Select(s => new
                {
                    s.Id,
                    s.Title,
                    s.Speaker,
                    s.StartTime,
                    s.EndTime,
                    s.Source,
                    s.AssetStatus
                })
            },
            MetadataJsonOptions);

        return new EventPublicationArtifact(folderPath, readme, metadata);
    }

    private static string CreateReadme(Event evt, IReadOnlyCollection<AgendaSession> sessions)
    {
        var builder = new StringBuilder()
            .Append("# ").AppendLine(EscapeMarkdown(evt.Title))
            .AppendLine()
            .Append("Lifecycle: ").AppendLine(evt.LifecycleState.ToString())
            .Append("Date: ").AppendLine(evt.StartDate.ToString("u"))
            .Append("Location: ").AppendLine(EscapeMarkdown(evt.Location ?? "TBD"))
            .AppendLine()
            .AppendLine("## Agenda");

        foreach (var session in sessions)
        {
            builder
                .Append("- ")
                .Append(session.StartTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" - ")
                .Append(session.EndTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture))
                .Append(": ")
                .Append(EscapeMarkdown(session.Title));

            if (!string.IsNullOrWhiteSpace(session.Speaker))
            {
                builder.Append(" (").Append(EscapeMarkdown(session.Speaker)).Append(')');
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string EscapeMarkdown(string value)
        => value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string Slugify(DateTimeOffset value) => value.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Slugify(string value)
    {
        var slug = SlugUnsafeCharacters().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "event" : slug[..Math.Min(slug.Length, 80)];
    }

    private static string CreateIdempotencyKey(Guid eventId, string metadataJson)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(metadataJson));
        return $"{eventId:N}-{Convert.ToHexString(hash)[..16]}";
    }

    private static string? NormalizeHttpsUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Registration URL must be an absolute HTTPS URL.", nameof(value));
        }

        return normalized;
    }

    private static EventLifecycleOperationResult ToResult(Event evt, string message)
        => new(evt.Id, evt.LifecycleState, evt.GitHubFolderUrl, evt.RegistrationUrl, message);

    [GeneratedRegex("[^a-z0-9-]+")]
    private static partial Regex SlugUnsafeCharacters();
}
