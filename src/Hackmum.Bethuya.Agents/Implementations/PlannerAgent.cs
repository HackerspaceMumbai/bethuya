using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.Agents.MCP.Contracts;
using Hackmum.Bethuya.AI.Routing;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace Hackmum.Bethuya.Agents.Implementations;

/// <summary>
/// Planner Agent — drafts agendas, timings, and speaker suggestions.
/// Runs on Azure OpenAI (non-sensitive content) with Foundry Hosted memory for decision rationale.
/// Queries Scout for speaker availability and event history context.
/// </summary>
public sealed partial class PlannerAgent(
    IAIRouter router,
    ILogger<PlannerAgent> logger,
    IEventHistoryMcp eventHistoryMcp,
    ISpeakerAvailabilityMcp speakerAvailabilityMcp)
    : AgentBase<PlannerRequest, PlannerResponse>(router, logger)
{
    private readonly IEventHistoryMcp _eventHistoryMcp = eventHistoryMcp;
    private readonly ISpeakerAvailabilityMcp _speakerAvailabilityMcp = speakerAvailabilityMcp;

    public override string Name => "Planner";

    /// <summary>
    /// Main entry point for drafting an agenda.
    /// Gathers context, builds prompt, calls AI, and parses structured result.
    /// </summary>
    public async Task<PlannerResponse> DraftAgendaAsync(PlannerRequest request, CancellationToken ct = default)
    {
        LogStarting(Logger, Name, request.Event.Id);

        try
        {
            // Gather event history and speaker availability context
            var eventHistory = await GatherEventHistoryAsync(ct);
            var speakerAvailability = await GatherSpeakerAvailabilityAsync(request, ct);

            // Build enhanced prompt with Scout context
            var enhancedRequest = request with
            {
                PriorEventsContext = FormatEventHistory(eventHistory),
                Constraints = AppendSpeakerContext(request.Constraints, speakerAvailability)
            };

            // Use base DraftAsync which calls BuildPrompt and ParseResponse
            var response = await DraftAsync(enhancedRequest, ct);

            LogComplete(Logger, Name, request.Event.Id);

            return response;
        }
        catch (Exception ex)
        {
            LogError(Logger, ex, Name, request.Event.Id);
            throw;
        }
    }

    protected override IList<ChatMessage> BuildPrompt(PlannerRequest request)
    {
        var systemPrompt = """
            You are the Planner Agent for Bethuya, a community event platform.
            Your job is to draft an event agenda with sessions, timings, and speaker suggestions.
            
            Guidelines:
            - Create a balanced agenda with varied session types
            - Suggest realistic time slots (30-60 min per session)
            - Include breaks and networking time
            - Consider the event type, capacity, and venue
            - Leverage historical patterns from prior events
            - Respect speaker availability constraints
            
            Respond with a valid JSON object (no markdown, no extra text):
            {
                "title": "Agenda title",
                "theme": "Event theme if identifiable",
                "sessions": [
                    {
                        "order": 1,
                        "title": "...",
                        "speaker": "Suggested speaker or TBD",
                        "startTime": "HH:mm",
                        "endTime": "HH:mm",
                        "description": "..."
                    }
                ],
                "reasoning": "Detailed explanation of agenda decisions"
            }
            """;

        var userPrompt = $"""
            Event: {request.Event.Title}
            Type: {request.Event.Type}
            Capacity: {request.Event.Capacity}
            Start: {request.Event.StartDate:yyyy-MM-dd HH:mm}
            End: {request.Event.EndDate:yyyy-MM-dd HH:mm}
            Duration: {(request.Event.EndDate - request.Event.StartDate).TotalHours:F1} hours
            Location: {request.Event.Location ?? "TBD"}
            Description: {request.Event.Description ?? "N/A"}
            
            Constraints & Context:
            {request.Constraints ?? "None"}
            
            Historical Patterns from Prior Events:
            {request.PriorEventsContext ?? "No prior events"}
            """;

        return
        [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ];
    }

    protected override PlannerResponse ParseResponse(ChatResponse response, PlannerRequest request)
    {
        var content = response.Text ?? "";

        try
        {
            // Extract JSON from response (handle possible markdown wrapping)
            var jsonContent = ExtractJson(content);
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            var sessions = new List<AgendaSession>();
            var sessionOrder = 0;

            if (root.TryGetProperty("sessions", out var sessionsElement) &&
                sessionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var session in sessionsElement.EnumerateArray())
                {
                    sessionOrder++;

                    var title = session.TryGetProperty("title", out var titleEl)
                        ? titleEl.GetString() ?? $"Session {sessionOrder}"
                        : $"Session {sessionOrder}";

                    var speaker = session.TryGetProperty("speaker", out var speakerEl)
                        ? speakerEl.GetString()
                        : null;

                    var startTimeStr = session.TryGetProperty("startTime", out var startEl)
                        ? startEl.GetString()
                        : "09:00";

                    var endTimeStr = session.TryGetProperty("endTime", out var endEl)
                        ? endEl.GetString()
                        : "10:00";

                    var description = session.TryGetProperty("description", out var descEl)
                        ? descEl.GetString()
                        : null;

                    if (TimeOnly.TryParseExact(startTimeStr, "HH:mm", CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var startTime) &&
                        TimeOnly.TryParseExact(endTimeStr, "HH:mm", CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var endTime))
                    {
                        sessions.Add(new AgendaSession
                        {
                            Title = title,
                            Speaker = speaker,
                            StartTime = startTime,
                            EndTime = endTime,
                            Description = description,
                            Order = sessionOrder
                        });
                    }
                }
            }

            // If parsing failed or no sessions, create a default agenda
            if (sessions.Count == 0)
            {
                sessions.Add(new AgendaSession
                {
                    Title = "Opening & Welcome",
                    StartTime = TimeOnly.FromDateTime(request.Event.StartDate.DateTime),
                    EndTime = TimeOnly.FromDateTime(request.Event.StartDate.AddMinutes(30).DateTime),
                    Description = "Welcome and introductions",
                    Order = 1
                });
            }

            var agenda = new Agenda
            {
                EventId = request.Event.Id,
                Status = AgendaStatus.ProposedByAgent,
                CreatedByAgent = Name,
                Sessions = sessions
            };

            return new PlannerResponse(
                DraftAgenda: agenda,
                RequiresHumanApproval: true,
                AgentReasoning: root.TryGetProperty("reasoning", out var reasoningEl)
                    ? reasoningEl.GetString()
                    : content);
        }
        catch (JsonException ex)
        {
            LogParseError(Logger, ex, Name);

            // Fallback to minimal agenda
            var defaultAgenda = new Agenda
            {
                EventId = request.Event.Id,
                Status = AgendaStatus.ProposedByAgent,
                CreatedByAgent = Name,
                Sessions =
                [
                    new AgendaSession
                    {
                        Title = "Opening & Welcome",
                        StartTime = TimeOnly.FromDateTime(request.Event.StartDate.DateTime),
                        EndTime = TimeOnly.FromDateTime(request.Event.StartDate.AddMinutes(30).DateTime),
                        Description = "Welcome and introductions",
                        Order = 1
                    }
                ]
            };

            return new PlannerResponse(
                DraftAgenda: defaultAgenda,
                RequiresHumanApproval: true,
                AgentReasoning: $"Default agenda (parsing error). Raw response: {content}");
        }
    }

    /// <summary>
    /// Queries event history MCP tool to gather patterns from prior events.
    /// </summary>
    private async Task<List<EventHistoryRecord>> GatherEventHistoryAsync(CancellationToken ct)
    {
        try
        {
            LogQueryingEventHistory(Logger, Name);
            return await _eventHistoryMcp.GetPastEventsAsync(limit: 5, ct: ct);
        }
        catch (Exception ex)
        {
            LogEventHistoryError(Logger, ex, Name);
            return [];
        }
    }

    /// <summary>
    /// Queries speaker availability MCP tool for planner context.
    /// (In Phase 2, this is a placeholder; full integration via Scout message bus comes in Phase 3)
    /// </summary>
    private async Task<List<SpeakerAvailability>> GatherSpeakerAvailabilityAsync(
        PlannerRequest request, CancellationToken ct)
    {
        try
        {
            LogQueryingSpeakerAvailability(Logger, Name);

            var startDate = DateOnly.FromDateTime(request.Event.StartDate.DateTime);
            var endDate = DateOnly.FromDateTime(request.Event.EndDate.DateTime);

            // In Phase 2, we query the MCP directly; Phase 3 integrates via message bus
            return await _speakerAvailabilityMcp.GetMultipleAvailabilityAsync(
                ["speaker-1", "speaker-2", "speaker-3"],
                startDate, endDate, ct);
        }
        catch (Exception ex)
        {
            LogSpeakerAvailabilityError(Logger, ex, Name);
            return [];
        }
    }

    /// <summary>
    /// Formats event history for inclusion in the prompt.
    /// </summary>
    private static string FormatEventHistory(List<EventHistoryRecord> history)
    {
        if (history.Count == 0)
            return "No prior events on record.";

        var formatted = new System.Text.StringBuilder();
        formatted.AppendLine("Prior Events (most recent):");

        foreach (var evt in history.Take(5))
        {
            var dateStr = evt.HeldAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            formatted.Append("  - ").Append(evt.Title).Append(" (").Append(dateStr)
                .Append(") @ ").Append(evt.AttendeeCount).AppendLine(" attendees");

            if (evt.Sessions.Count > 0)
            {
                var sessions = string.Join(", ", evt.Sessions.Select(s => s.Title));
                formatted.Append("    Sessions: ").AppendLine(sessions);
            }
        }

        return formatted.ToString();
    }

    /// <summary>
    /// Appends speaker availability context to constraints.
    /// </summary>
    private static string AppendSpeakerContext(string? constraints, List<SpeakerAvailability> speakers)
    {
        var sb = new System.Text.StringBuilder(constraints ?? "");

        if (speakers.Count > 0)
        {
            sb.AppendLine("Available Speakers:");
            foreach (var speaker in speakers)
            {
                var topics = string.Join(", ", speaker.Topics);
                sb.Append("  - ").Append(speaker.Name).Append(" (Topics: ")
                    .Append(topics).AppendLine(")");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Extracts JSON from response that may be wrapped in markdown code blocks.
    /// </summary>
    private static string ExtractJson(string content)
    {
        var jsonStart = content.IndexOf('{');
        var jsonEnd = content.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            return content.Substring(jsonStart, jsonEnd - jsonStart + 1);
        }

        return content;
    }

    #region Logging
    [LoggerMessage(Level = LogLevel.Information, Message = "[{Agent}] Starting agenda draft for event {EventId}")]
    private static partial void LogStarting(ILogger logger, string agent, Guid eventId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{Agent}] Agenda draft complete for event {EventId}")]
    private static partial void LogComplete(ILogger logger, string agent, Guid eventId);

    [LoggerMessage(Level = LogLevel.Error, Message = "[{Agent}] Error drafting agenda for event {EventId}")]
    private static partial void LogError(ILogger logger, Exception ex, string agent, Guid eventId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{Agent}] Querying event history")]
    private static partial void LogQueryingEventHistory(ILogger logger, string agent);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[{Agent}] Failed to retrieve event history")]
    private static partial void LogEventHistoryError(ILogger logger, Exception ex, string agent);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{Agent}] Querying speaker availability")]
    private static partial void LogQueryingSpeakerAvailability(ILogger logger, string agent);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[{Agent}] Failed to retrieve speaker availability")]
    private static partial void LogSpeakerAvailabilityError(ILogger logger, Exception ex, string agent);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[{Agent}] Failed to parse JSON response, using default agenda")]
    private static partial void LogParseError(ILogger logger, Exception ex, string agent);
    #endregion
}
