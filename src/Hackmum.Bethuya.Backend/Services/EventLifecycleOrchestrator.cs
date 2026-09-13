using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

public sealed partial class EventLifecycleOrchestrator(
    BethuyaDbContext dbContext,
    ILumaRegistrationService lumaRegistrationService,
    ITeamsNotificationService teamsNotificationService,
    IWebCacheInvalidationService cacheInvalidationService)
    : IEventLifecycleOrchestrator
{
    public async Task<EventLifecycleOperationResult> TransitionAsync(
        Guid eventId,
        MeetupLifecycleState targetState,
        string actor,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        var occurredAt = DateTimeOffset.UtcNow;

        var evt = await LoadEventAggregateAsync(eventId, ct);

        evt.TransitionLifecycleTo(targetState, occurredAt);
        if (IsPublicLifecycle(targetState))
        {
            await QueueArchiveProjectionAsync(evt, ct);
        }
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

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            var artifact = CreatePublicationArtifact(evt);
            var outbox = CreateOutboxMessage(evt, artifact);
            var alreadyQueued = await dbContext.EventArchiveOutboxMessages
                .AnyAsync(message => message.EventId == outbox.EventId
                    && message.Destination == outbox.Destination
                    && message.IdempotencyKey == outbox.IdempotencyKey, ct);
            if (!alreadyQueued)
            {
                dbContext.EventArchiveOutboxMessages.Add(outbox);
            }
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        return ToResult(evt, "Event published and queued for archive synchronization.");
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

            await QueueArchiveProjectionAsync(evt, ct);
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

        await QueueArchiveProjectionAsync(evt, ct);
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
        await QueueArchiveProjectionAsync(evt, ct);
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
        var metadata = CreateMetadataYaml(evt, sessions);

        return new EventPublicationArtifact(folderPath, readme, metadata);
    }

    private static string CreateMetadataYaml(Event evt, AgendaSession[] sessions)
    {
        var builder = new StringBuilder()
            .Append("title: ").AppendLine(YamlString(evt.Title))
            .Append("slug: ").AppendLine(YamlString(Slugify(evt.Title)))
            .Append("date: ").AppendLine(evt.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
            .Append("city: ").AppendLine(YamlString(evt.Location ?? "Unknown"))
            .AppendLine("country: India")
            .Append("eventType: ").AppendLine(ToArchiveEventType(evt.Type))
            .AppendLine("series: null")
            .AppendLine("community: Hackerspace Mumbai")
            .Append("venue: ").AppendLine(YamlString(evt.Location ?? "TBD"))
            .AppendLine("website: https://hackmum.in")
            .AppendLine("eventPage: https://hackmum.in/past-events/")
            .Append("resourcesAvailable: ").AppendLine(sessions.Length > 0 ? "true" : "false")
            .Append("status: ").AppendLine(evt.LifecycleState is MeetupLifecycleState.Completed or MeetupLifecycleState.Archived ? "completed" : "upcoming")
            .AppendLine("timezone: Asia/Kolkata")
            .Append("description: ").AppendLine(YamlString(evt.Description ?? string.Empty))
            .AppendLine("bethuya:")
            .Append("  eventId: ").AppendLine(evt.Id.ToString("D"))
            .AppendLine("  projectionVersion: 1")
            .AppendLine("  managedFields:")
            .AppendLine("    - title")
            .AppendLine("    - date")
            .AppendLine("    - status")
            .AppendLine("    - description");

        return builder.ToString();
    }

    private static string ToArchiveEventType(EventType type)
        => type switch
        {
            EventType.Workshop => "workshop",
            EventType.Hackathon => "hackathon",
            EventType.Conference => "conference",
            EventType.Meetup or EventType.Panel or EventType.Social => "meetup",
            _ => "meetup"
        };

    private static string YamlString(string value)
        => '"' + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal) + '"';

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

    private static EventArchiveOutboxMessage CreateOutboxMessage(Event evt, EventPublicationArtifact artifact)
        => new()
        {
            EventId = evt.Id,
            Destination = "github-events",
            FolderPath = artifact.FolderPath,
            ReadmeMarkdown = artifact.ReadmeMarkdown,
            MetadataJson = artifact.MetadataJson,
            IdempotencyKey = CreateIdempotencyKey(evt.Id, artifact.MetadataJson)
        };

    private async Task QueueArchiveProjectionAsync(Event evt, CancellationToken ct)
    {
        var artifact = CreatePublicationArtifact(evt);
        var outbox = CreateOutboxMessage(evt, artifact);
        if (!await dbContext.EventArchiveOutboxMessages.AnyAsync(message =>
                message.EventId == outbox.EventId
                && message.Destination == outbox.Destination
                && message.IdempotencyKey == outbox.IdempotencyKey, ct))
        {
            dbContext.EventArchiveOutboxMessages.Add(outbox);
        }
    }

    private static bool IsPublicLifecycle(MeetupLifecycleState state)
        => state is MeetupLifecycleState.Published
            or MeetupLifecycleState.ScheduleAltered
            or MeetupLifecycleState.Delayed
            or MeetupLifecycleState.Completed
            or MeetupLifecycleState.Archived;

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
