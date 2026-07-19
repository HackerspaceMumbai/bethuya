using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

public sealed partial class SessionIngestionService(
    IEventRepository eventRepository,
    ISessionizeService sessionizeService,
    BethuyaDbContext dbContext,
    ILogger<SessionIngestionService> logger)
    : ISessionIngestionService
{
    public async Task<IReadOnlyList<NormalizedSession>> PreviewSessionizeAsync(Guid eventId, CancellationToken ct = default)
    {
        var evt = await eventRepository.GetByIdAsync(eventId, ct)
            ?? throw new KeyNotFoundException($"Event {eventId} was not found.");

        if (string.IsNullOrWhiteSpace(evt.SessionizeEventId))
        {
            throw new InvalidOperationException("Event does not have a Sessionize event id.");
        }

        var sessions = await sessionizeService.GetSessionsAsync(evt.SessionizeEventId, ct);
        return sessions.Select(SessionNormalizationEngine.Normalize).ToArray();
    }

    public async Task<int> ImportSessionizeAsync(Guid eventId, CancellationToken ct = default)
    {
        var normalizedSessions = await PreviewSessionizeAsync(eventId, ct);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var importedCount = 0;

        await strategy.ExecuteAsync(async () =>
        {
            var attemptImportedCount = 0;
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            var evt = await dbContext.Events
                .Include(e => e.Agenda)
                .ThenInclude(a => a!.Sessions)
                .FirstOrDefaultAsync(e => e.Id == eventId, ct)
                ?? throw new KeyNotFoundException($"Event {eventId} was not found.");

            evt.Agenda ??= new Agenda { EventId = evt.Id };
            var existingSourceIds = evt.Agenda.Sessions
                .Where(s => s.Source == SessionSource.Sessionize && s.SourceSessionId is not null)
                .Select(s => s.SourceSessionId!)
                .ToHashSet(StringComparer.Ordinal);

            var nextOrder = evt.Agenda.Sessions.Count == 0
                ? 1
                : evt.Agenda.Sessions.Max(s => s.Order) + 1;

            foreach (var session in normalizedSessions)
            {
                if (session.SourceSessionId is not null && existingSourceIds.Contains(session.SourceSessionId))
                {
                    continue;
                }

                evt.Agenda.Sessions.Add(ToAgendaSession(session, nextOrder++));
                if (session.SourceSessionId is not null)
                {
                    existingSourceIds.Add(session.SourceSessionId);
                }

                attemptImportedCount++;
            }

            evt.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            importedCount = attemptImportedCount;
        });

        LogSessionizeImport(logger, eventId, importedCount);
        return importedCount;
    }

    private static AgendaSession ToAgendaSession(NormalizedSession session, int order)
    {
        var primarySpeaker = session.Speakers.FirstOrDefault();
        var startTime = session.PreferredStartTime is null
            ? TimeOnly.MinValue
            : TimeOnly.FromDateTime(session.PreferredStartTime.Value.DateTime);
        var endTime = session.PreferredStartTime is not null && session.Duration is not null
            ? TimeOnly.FromDateTime(session.PreferredStartTime.Value.Add(session.Duration.Value).DateTime)
            : startTime.AddHours(1);

        return new AgendaSession
        {
            Title = session.Title,
            Description = session.Description,
            Speaker = primarySpeaker?.Name,
            SpeakerGitHubHandle = primarySpeaker?.GitHubHandle,
            SpeakerTwitterHandle = primarySpeaker?.TwitterHandle,
            SpeakerAvatarUrl = primarySpeaker?.AvatarUrl,
            Source = session.Source,
            SourceSessionId = session.SourceSessionId,
            ScheduledStartAt = session.PreferredStartTime,
            ScheduledEndAt = session.PreferredStartTime is not null && session.Duration is not null
                ? session.PreferredStartTime.Value.Add(session.Duration.Value)
                : null,
            StartTime = startTime,
            EndTime = endTime,
            Order = order
        };
    }

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Information,
        Message = "Imported {ImportedCount} Sessionize sessions for event {EventId}.")]
    private static partial void LogSessionizeImport(ILogger logger, Guid eventId, int importedCount);
}
