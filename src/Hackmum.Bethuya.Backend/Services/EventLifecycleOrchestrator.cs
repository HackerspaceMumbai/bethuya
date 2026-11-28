using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hackmum.Bethuya.Backend.Services;

public sealed partial class EventLifecycleOrchestrator(
    BethuyaDbContext dbContext,
    ILumaRegistrationService lumaRegistrationService,
    ITeamsNotificationService teamsNotificationService,
    IWebCacheInvalidationService cacheInvalidationService,
    IGitHubEventRepository gitHubEventRepository)
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
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        return ToResult(evt, "Event published. Archive publication requires organizer confirmation.");
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
            session.MarkPendingUpload(assetDueAt?.ToUniversalTime());
        }

        await dbContext.SaveChangesAsync(ct);
        return ToResult(evt, "Event completed; session assets are pending upload. Archive publication requires organizer confirmation.");
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
        return ToResult(evt, "Event archived. Archive publication requires organizer confirmation.");
    }

    public async Task<EventArchivePublicationPreview> PreviewArchivePublicationAsync(
        Guid eventId,
        CancellationToken ct = default)
    {
        var evt = await LoadEventAggregateAsync(eventId, ct);
        EnsurePublicLifecycle(evt);
        var folderPath = evt.ArchiveFolderPath ?? CreateArchiveFolderPath(evt);
        await EnsureArchivePathAvailableAsync(evt, folderPath, ct);
        var request = CreatePublicationRequest(evt, folderPath);
        var preview = await gitHubEventRepository.PreviewEventAsync(request, ct);
        return new(eventId, preview);
    }

    public async Task<EventLifecycleOperationResult> ConfirmArchivePublicationAsync(
        Guid eventId,
        string actor,
        string previewToken,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewToken);
        var evt = await LoadEventAggregateAsync(eventId, ct);
        EnsurePublicLifecycle(evt);
        var folderPath = evt.ArchiveFolderPath ?? CreateArchiveFolderPath(evt);
        await EnsureArchivePathAvailableAsync(evt, folderPath, ct);
        var artifact = CreatePublicationArtifact(evt, folderPath);
        var preview = await gitHubEventRepository.PreviewEventAsync(
            CreatePublicationRequest(evt, folderPath, artifact),
            ct);
        if (!string.Equals(preview.PreviewToken, previewToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The archive changed after preview. Review a new preview before confirming.");
        }

        if (!preview.HasChanges)
        {
            throw new InvalidOperationException("The archive already matches this projection; there are no changes to queue.");
        }

        var idempotencyKey = CreateIdempotencyKey(
            Hackmum.Bethuya.Core.ValueObjects.EventId.From(evt.Id),
            artifact);
        var occurredAt = DateTimeOffset.UtcNow;
        var alreadyQueued = false;
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var currentEvent = await dbContext.Events.AsNoTracking()
                .Include(current => current.Agenda)
                .ThenInclude(agenda => agenda!.Sessions)
                .SingleAsync(current => current.Id == eventId, ct);
            EnsurePublicLifecycle(currentEvent);
            var currentFolderPath = currentEvent.ArchiveFolderPath ?? CreateArchiveFolderPath(currentEvent);
            var currentArtifact = CreatePublicationArtifact(currentEvent, currentFolderPath);
            var currentProjectionKey = CreateIdempotencyKey(
                Hackmum.Bethuya.Core.ValueObjects.EventId.From(currentEvent.Id), currentArtifact);
            if (!string.Equals(currentFolderPath, folderPath, StringComparison.Ordinal)
                || !string.Equals(currentProjectionKey, idempotencyKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The event projection changed while the archive preview was being confirmed. Review a new preview before confirming.");
            }

            var currentPublicationKey = currentEvent.ArchivePublicationIdempotencyKey;
            alreadyQueued = !await AddOutboxIfMissingAsync(
                CreateOutboxMessage(evt, artifact, actor, preview.PreviewToken),
                currentPublicationKey,
                ct);
            if (alreadyQueued)
            {
                return;
            }

            var updated = await dbContext.Events
                .Where(current => current.Id == eventId
                    && current.ArchivePublicationIdempotencyKey == currentPublicationKey)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(current => current.ArchiveFolderPath, folderPath)
                    .SetProperty(current => current.ArchivePublicationStatus, EventArchivePublicationStatus.Pending)
                    .SetProperty(current => current.ArchivePublicationRequestedBy, actor)
                    .SetProperty(current => current.ArchivePublicationRequestedAt, occurredAt)
                    .SetProperty(current => current.ArchivePublicationIdempotencyKey, idempotencyKey), ct);
            if (updated != 1)
            {
                throw new InvalidOperationException("Another archive confirmation advanced this event. Re-preview and confirm again.");
            }
            await transaction.CommitAsync(ct);
        });

        await dbContext.Entry(evt).ReloadAsync(ct);
        if (alreadyQueued)
        {
            return ToResult(evt, "This archive projection is already queued or has a pull request.");
        }

        return ToResult(evt, "Archive publication queued for Draft PR creation.");
    }

    public async Task<EventArchivePublicationPreview> PreviewArchiveAdoptionAsync(
        Guid eventId,
        string folderPath,
        CancellationToken ct = default)
    {
        var evt = await LoadEventAggregateAsync(eventId, ct);
        ValidateArchiveFolderPath(evt, folderPath);
        await EnsureArchivePathAvailableAsync(evt, folderPath, ct);
        var entry = await gitHubEventRepository.GetArchiveEntryAsync(folderPath, ct)
            ?? throw new InvalidOperationException("No archive entry exists at the specified path.");
        ValidateAdoptionIdentity(evt, entry);
        var preview = await gitHubEventRepository.PreviewEventWithEntryAsync(
            CreatePublicationRequest(evt, folderPath),
            entry,
            ct);
        return new(eventId, preview);
    }

    public async Task<EventArchiveAdoptionResult> ConfirmArchiveAdoptionAsync(
        Guid eventId,
        string folderPath,
        string previewToken,
        string actor,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(previewToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        var evt = await LoadEventAggregateAsync(eventId, ct);
        ValidateArchiveFolderPath(evt, folderPath);
        await EnsureArchivePathAvailableAsync(evt, folderPath, ct);
        var entry = await gitHubEventRepository.GetArchiveEntryAsync(folderPath, ct)
            ?? throw new InvalidOperationException("No archive entry exists at the specified path.");
        ValidateAdoptionIdentity(evt, entry);
        var preview = await gitHubEventRepository.PreviewEventWithEntryAsync(
            CreatePublicationRequest(evt, folderPath),
            entry,
            ct);
        if (!string.Equals(preview.PreviewToken, previewToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The archive changed after preview. Review a new preview before confirming adoption.");
        }

        var adoptedAt = evt.ArchiveAdoptedAt ?? DateTimeOffset.UtcNow;
        evt.ArchiveFolderPath = folderPath;
        evt.ArchiveAdoptedBy ??= actor;
        evt.ArchiveAdoptedAt = adoptedAt;
        evt.ArchiveSourceReadmeSha = entry.ReadmeSha;
        evt.ArchiveSourceMetadataSha = entry.MetadataSha;
        await dbContext.SaveChangesAsync(ct);
        return new(eventId, folderPath, evt.ArchiveAdoptedBy, adoptedAt);
    }

    public async Task<EventArchivePullRequestStatus> RefreshArchivePublicationStatusAsync(
        Guid eventId,
        CancellationToken ct = default)
    {
        var evt = await LoadEventAggregateAsync(eventId, ct);
        if (evt.ArchivePullRequestNumber is not { } pullRequestNumber
            || string.IsNullOrWhiteSpace(evt.ArchiveFolderPath))
        {
            throw new InvalidOperationException("This event has no archive pull request to refresh.");
        }

        var folderPath = evt.ArchiveFolderPath;
        var publicationKey = evt.ArchivePublicationIdempotencyKey;
        var observedStatus = evt.ArchivePublicationStatus;
        var status = await gitHubEventRepository.GetPublicationStatusAsync(
            pullRequestNumber,
            folderPath,
            ct);
        if (observedStatus is EventArchivePublicationStatus.Pending or EventArchivePublicationStatus.Failed)
        {
            // This is the last known PR, not evidence that the newly queued projection was applied.
            return status;
        }

        var publicationStatus = status.IsMerged
            ? EventArchivePublicationStatus.Published
            : status.IsClosed
                ? EventArchivePublicationStatus.Closed
                : EventArchivePublicationStatus.DraftPrPending;
        var updated = await dbContext.Events
            .Where(current => current.Id == eventId
                && current.ArchivePullRequestNumber == pullRequestNumber
                && current.ArchivePublicationIdempotencyKey == publicationKey
                && current.ArchivePublicationStatus == observedStatus
                && current.ArchiveFolderPath == folderPath)
            .ExecuteUpdateAsync(update => update
                .SetProperty(current => current.ArchivePullRequestUrl, status.PullRequestUrl)
                .SetProperty(current => current.ArchivePublicationStatus, publicationStatus)
                .SetProperty(current => current.GitHubFolderUrl,
                    current => status.IsMerged ? status.FolderUrl : current.GitHubFolderUrl), ct);
        if (updated != 1)
        {
            throw new InvalidOperationException("Archive publication changed while refreshing the previous PR. Refresh the current publication instead; no stale status was saved.");
        }

        await dbContext.Entry(evt).ReloadAsync(ct);
        return status;
    }

    private async Task<Event> LoadEventAggregateAsync(Guid eventId, CancellationToken ct)
        => await dbContext.Events
            .Include(e => e.Agenda)
            .ThenInclude(a => a!.Sessions)
            .FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw new KeyNotFoundException($"Event {eventId} was not found.");

    private static EventPublicationArtifact CreatePublicationArtifact(Event evt, string folderPath)
    {
        var folderSlug = Path.GetFileName(folderPath);
        var sessions = evt.Agenda?.Sessions.OrderBy(s => s.Order).ToArray() ?? [];
        var readme = CreateReadme(evt, sessions);
        var metadata = CreateMetadataYaml(evt, sessions, folderSlug);

        return new EventPublicationArtifact(folderPath, readme, metadata);
    }

    private static string CreateArchiveFolderPath(Event evt)
    {
        var localStart = ToAsiaKolkata(evt.StartDate);
        var slug = Slugify(evt.Title);
        var folderSlug = $"{localStart:yyyy-MM-dd}-{slug}-{evt.Id:N}";
        return $"events/{localStart:yyyy}/{folderSlug}";
    }

    private static EventPublicationRequest CreatePublicationRequest(
        Event evt,
        string folderPath,
        EventPublicationArtifact? artifact = null)
    {
        artifact ??= CreatePublicationArtifact(evt, folderPath);
        return new(
            Hackmum.Bethuya.Core.ValueObjects.EventId.From(evt.Id),
            evt.Title,
            artifact.FolderPath,
            artifact.ReadmeMarkdown,
            artifact.MetadataJson,
            CreateIdempotencyKey(Hackmum.Bethuya.Core.ValueObjects.EventId.From(evt.Id), artifact));
    }

    private static string CreateMetadataYaml(Event evt, AgendaSession[] sessions, string slug)
    {
        var localStart = ToAsiaKolkata(evt.StartDate);
        var builder = new StringBuilder()
            .Append("title: ").AppendLine(YamlString(evt.Title))
            .Append("slug: ").AppendLine(YamlString(slug))
            .Append("date: ").AppendLine(localStart.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
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
        var localStart = ToAsiaKolkata(evt.StartDate);
        var builder = new StringBuilder()
            .Append("# ").AppendLine(EscapeMarkdown(evt.Title))
            .AppendLine()
            .Append("Lifecycle: ").AppendLine(evt.LifecycleState.ToString())
            .Append("Date: ").AppendLine(localStart.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture))
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

    private static DateTimeOffset ToAsiaKolkata(DateTimeOffset value)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        return TimeZoneInfo.ConvertTime(value, timeZone);
    }

    private static string EscapeMarkdown(string value)
        => value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string Slugify(DateTimeOffset value) => value.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Slugify(string value)
    {
        var slug = SlugUnsafeCharacters().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "event" : slug[..Math.Min(slug.Length, 80)];
    }

    private static string CreateIdempotencyKey(Hackmum.Bethuya.Core.ValueObjects.EventId eventId, EventPublicationArtifact artifact)
    {
        var raw = $"{artifact.FolderPath}\n{artifact.ReadmeMarkdown}\n{artifact.MetadataJson}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"{eventId.Value:N}-{Convert.ToHexString(hash)[..16]}";
    }

    private static EventArchiveOutboxMessage CreateOutboxMessage(
        Event evt,
        EventPublicationArtifact artifact,
        string actor,
        string confirmedPreviewToken)
        => new()
        {
            EventId = Hackmum.Bethuya.Core.ValueObjects.EventId.From(evt.Id),
            Destination = "github-events",
            FolderPath = artifact.FolderPath,
            Title = evt.Title,
            ConfirmedBy = actor,
            ReadmeMarkdown = artifact.ReadmeMarkdown,
            MetadataJson = artifact.MetadataJson,
            ConfirmedPreviewToken = confirmedPreviewToken,
            IdempotencyKey = CreateIdempotencyKey(Hackmum.Bethuya.Core.ValueObjects.EventId.From(evt.Id), artifact)
        };

    private async Task<bool> AddOutboxIfMissingAsync(
        EventArchiveOutboxMessage outbox,
        string? currentPublicationKey,
        CancellationToken ct)
    {
        var existing = await dbContext.EventArchiveOutboxMessages.AsNoTracking().FirstOrDefaultAsync(message =>
                message.EventId == outbox.EventId
                && message.Destination == outbox.Destination
                && message.IdempotencyKey == outbox.IdempotencyKey,
            ct);
        if (existing is not null)
        {
            return await HandleExistingOutboxAsync(existing, outbox, currentPublicationKey, ct);
        }

        const string savepointName = "OutboxInsert";
        var transaction = dbContext.Database.CurrentTransaction;
        if (transaction is not null)
        {
            await transaction.CreateSavepointAsync(savepointName, ct);
        }

        try
        {
            dbContext.EventArchiveOutboxMessages.Add(outbox);
            await dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            if (transaction is not null)
            {
                await transaction.RollbackToSavepointAsync(savepointName, ct);
            }

            dbContext.Entry(outbox).State = EntityState.Detached;
            var winner = await dbContext.EventArchiveOutboxMessages.AsNoTracking().FirstOrDefaultAsync(message =>
                message.EventId == outbox.EventId
                && message.Destination == outbox.Destination
                && message.IdempotencyKey == outbox.IdempotencyKey, ct)
                ?? throw new InvalidOperationException("Archive confirmation conflicted without a matching outbox request. Re-preview and confirm again.", exception);
            return await HandleExistingOutboxAsync(winner, outbox, currentPublicationKey, ct);
        }
    }

    private async Task<bool> HandleExistingOutboxAsync(
        EventArchiveOutboxMessage existing,
        EventArchiveOutboxMessage outbox,
        string? currentPublicationKey,
        CancellationToken ct)
    {
        var sameSnapshot = string.Equals(existing.ConfirmedPreviewToken, outbox.ConfirmedPreviewToken, StringComparison.Ordinal);
        var reactivate = string.IsNullOrEmpty(existing.ConfirmedBy)
            || existing.ProcessedAt is not null
                && (existing.LastError is not null || !sameSnapshot
                    || !string.Equals(currentPublicationKey, outbox.IdempotencyKey, StringComparison.Ordinal));
        if (!reactivate)
        {
            if (existing.ProcessedAt is null && (!sameSnapshot
                || !string.Equals(currentPublicationKey, outbox.IdempotencyKey, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "An older archive confirmation is still queued or processing. Wait for it to finish, then re-preview and confirm again.");
            }

            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var updated = await dbContext.EventArchiveOutboxMessages
            .Where(message => message.Id == existing.Id
                && message.ProcessedAt == existing.ProcessedAt
                && message.ConfirmedPreviewToken == existing.ConfirmedPreviewToken
                && message.ConfirmedBy == existing.ConfirmedBy
                && message.ClaimToken == existing.ClaimToken)
            .ExecuteUpdateAsync(update => update
                .SetProperty(message => message.Title, outbox.Title)
                .SetProperty(message => message.ConfirmedBy, outbox.ConfirmedBy)
                .SetProperty(message => message.ConfirmedPreviewToken, outbox.ConfirmedPreviewToken)
                .SetProperty(message => message.ProcessedAt, (DateTimeOffset?)null)
                .SetProperty(message => message.AttemptCount, 0)
                .SetProperty(message => message.CreatedAt, now)
                .SetProperty(message => message.AvailableAt, now)
                .SetProperty(message => message.LockedUntil, (DateTimeOffset?)null)
                .SetProperty(message => message.ClaimToken, string.Empty)
                .SetProperty(message => message.LastError, (string?)null), ct);
        if (updated != 1)
        {
            throw new InvalidOperationException("Another request reactivated or claimed this archive confirmation. No snapshot was replaced; re-preview and confirm again.");
        }

        return true;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static void EnsurePublicLifecycle(Event evt)
    {
        if (evt.LifecycleState is not (MeetupLifecycleState.Published
            or MeetupLifecycleState.ScheduleAltered
            or MeetupLifecycleState.Delayed
            or MeetupLifecycleState.Completed
            or MeetupLifecycleState.Archived))
        {
            throw new InvalidOperationException("An event must be in a public lifecycle state before archive publication can be requested.");
        }
    }

    private async Task EnsureArchivePathAvailableAsync(Event evt, string folderPath, CancellationToken ct)
    {
        ValidateArchiveFolderPath(evt, folderPath);
        if (await dbContext.Events.AnyAsync(
                existing => existing.Id != evt.Id && existing.ArchiveFolderPath == folderPath,
                ct))
        {
            throw new InvalidOperationException("Another Bethuya event already claims this archive path.");
        }
    }

    private static void ValidateArchiveFolderPath(Event evt, string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        if (evt.ArchiveFolderPath is not null && !evt.ArchiveFolderPath.Equals(folderPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("This event already has an established archive path; archive folder moves are not supported.");
        }

        var match = ArchiveFolderPattern().Match(folderPath);
        if (!match.Success)
        {
            throw new InvalidOperationException("Archive path must be a repository-relative events/YYYY/YYYY-MM-DD-slug path.");
        }

        var localDate = ToAsiaKolkata(evt.StartDate);
        var pathDate = match.Groups["date"].Value;
        if (!match.Groups["year"].Value.Equals(localDate.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            || !pathDate.Equals(localDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Archive path '{folderPath}' does not match the event date {localDate:yyyy-MM-dd} in Asia/Kolkata. Archive folder moves are not supported; resolve the archive path manually before publication.");
        }
    }

    private static void ValidateAdoptionIdentity(Event evt, EventArchiveEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.MetadataYaml))
        {
            throw new InvalidOperationException("The specified archive entry must include event.yml so its identity can be validated.");
        }

        var titles = ReadYamlScalars(entry.MetadataYaml, "title", 0);
        var dates = ReadYamlScalars(entry.MetadataYaml, "date", 0);
        if (titles.Length != 1
            || dates.Length != 1
            || !string.Equals(titles[0], evt.Title, StringComparison.Ordinal)
            || !string.Equals(dates[0], ToAsiaKolkata(evt.StartDate).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The archive entry title or date does not exactly match this event; adoption was refused for human resolution.");
        }

        var identity = EventArchiveContentMerger.ReadBethuyaEventId(entry.MetadataYaml);
        if (identity is not null
            && (!Guid.TryParse(identity, out var existingEventId) || existingEventId != evt.Id))
        {
            throw new InvalidOperationException("The archive entry identifies a different or ambiguous Bethuya event; adoption was refused.");
        }
    }

    private static string[] ReadYamlScalars(string metadata, string key, int indentation)
    {
        return metadata.Split('\n')
            .Select(line => (Line: line.TrimEnd('\r'), Match: MetadataScalarPattern().Match(line.TrimEnd('\r'))))
            .Where(item => item.Match.Success
                && item.Match.Groups["indent"].Value.Length == indentation
                && item.Match.Groups["key"].Value.Equals(key, StringComparison.Ordinal))
            .Select(item => item.Match.Groups["value"].Value.Trim())
            .Select(value => value.Length >= 2 && value[0] == '"' && value[^1] == '"'
                ? value[1..^1]
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal)
                : value)
            .ToArray();
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

    [GeneratedRegex("^events/(?<year>\\d{4})/(?<date>\\d{4}-\\d{2}-\\d{2})-[a-zA-Z0-9][a-zA-Z0-9-]*\\z")]
    private static partial Regex ArchiveFolderPattern();

    [GeneratedRegex(@"^(?<indent> *)(?<key>[A-Za-z0-9_-]+)\s*:\s*(?<value>.*)$")]
    private static partial Regex MetadataScalarPattern();
}
