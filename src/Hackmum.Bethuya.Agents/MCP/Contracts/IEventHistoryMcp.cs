using Hackmum.Bethuya.Core.Models;
using Refit;

namespace Hackmum.Bethuya.Agents.MCP.Contracts;

/// <summary>
/// MCP tool for retrieving historical event data to inform planning decisions.
/// Used by Scout agent to gather context for Planner.
/// </summary>
public interface IEventHistoryMcp
{
    /// <summary>
    /// Retrieves past events, optionally filtered by theme.
    /// </summary>
    [Get("/api/events/history")]
    Task<List<EventHistoryRecord>> GetPastEventsAsync(
        [Query] int limit = 10,
        [Query] string? theme = null,
        CancellationToken ct = default);
}

/// <summary>
/// Historical record of a past event used for planning reference.
/// </summary>
public sealed record EventHistoryRecord(
    Guid EventId,
    string Title,
    string? Theme,
    DateTime HeldAt,
    int AttendeeCount,
    List<SessionRecord> Sessions)
{
    /// <summary>
    /// Implicit conversion from Event to EventHistoryRecord for repository queries.
    /// </summary>
    public static implicit operator EventHistoryRecord(Event evt) =>
        new(
            EventId: evt.Id,
            Title: evt.Title,
            Theme: null,
            HeldAt: evt.StartDate.DateTime,
            AttendeeCount: evt.Registrations.Count,
            Sessions: evt.Agenda?.Sessions.ConvertAll(s => new SessionRecord(
                Title: s.Title,
                SpeakerId: s.Speaker ?? "Unknown",
                StartTime: s.StartTime,
                EndTime: s.EndTime,
                Description: s.Description ?? "")) ?? []);
}

/// <summary>
/// Session details from a historical event.
/// </summary>
public sealed record SessionRecord(
    string Title,
    string SpeakerId,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Description);
