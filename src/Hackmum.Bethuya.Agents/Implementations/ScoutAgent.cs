using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.Agents.MCP.Contracts;
using Hackmum.Bethuya.AI.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Hackmum.Bethuya.Agents.Implementations;

/// <summary>
/// Scout Agent — stateless gatherer of external context for planning and curation.
/// Queries event history and speaker availability via MCP tools.
/// Executes all queries in parallel (fan-out pattern) for efficiency.
/// </summary>
public sealed partial class ScoutAgent(
    IAIRouter router,
    ILogger<ScoutAgent> logger,
    IEventHistoryMcp eventHistoryMcp,
    ISpeakerAvailabilityMcp speakerAvailabilityMcp)
    : AgentBase<ScoutRequest, ScoutResponse>(router, logger)
{
    private readonly IEventHistoryMcp _eventHistoryMcp = eventHistoryMcp;
    private readonly ISpeakerAvailabilityMcp _speakerAvailabilityMcp = speakerAvailabilityMcp;

    public override string Name => "Scout";

    /// <summary>
    /// Main entry point for Scout to gather external context.
    /// Routes by query type and parallelizes multiple queries.
    /// </summary>
    public async Task<ScoutResponse> GatherContextAsync(
        string queryType,
        Dictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        LogGathering(Logger, Name, queryType);

        try
        {
            var data = queryType switch
            {
                "speaker-availability" => await GatherSpeakerAvailabilityAsync(parameters, ct),
                "event-history" => await GatherEventHistoryAsync(parameters, ct),
                _ => new Dictionary<string, object>()
            };

            return new ScoutResponse(
                QueryType: queryType,
                Data: data);
        }
        catch (Exception ex)
        {
            LogContextError(Logger, ex, Name, queryType);
            throw;
        }
    }

    protected override IList<ChatMessage> BuildPrompt(ScoutRequest request)
    {
        return
        [
            new ChatMessage(ChatRole.System, $"""
                You are the Scout Agent for Bethuya, a community event platform.
                Your role is to gather and summarize external context for other agents.
                """),
            new ChatMessage(ChatRole.User, $"Gather context for: {request.QueryType}")
        ];
    }

    protected override ScoutResponse ParseResponse(ChatResponse response, ScoutRequest request)
    {
        return new ScoutResponse(
            QueryType: request.QueryType,
            Data: new Dictionary<string, object>());
    }

    /// <summary>
    /// Queries speaker availability for multiple speakers in parallel (fan-out).
    /// Parameters expected: "speakerIds" (comma-separated), "startDate", "endDate"
    /// </summary>
    private async Task<Dictionary<string, object>> GatherSpeakerAvailabilityAsync(
        Dictionary<string, string> parameters,
        CancellationToken ct)
    {
        if (!parameters.TryGetValue("speakerIds", out var speakerIdsStr) ||
            !parameters.TryGetValue("startDate", out var startDateStr) ||
            !parameters.TryGetValue("endDate", out var endDateStr))
        {
            return new Dictionary<string, object> { { "error", "Missing required parameters" } };
        }

        if (!DateOnly.TryParse(startDateStr, out var startDate) ||
            !DateOnly.TryParse(endDateStr, out var endDate))
        {
            return new Dictionary<string, object> { { "error", "Invalid date format" } };
        }

        var speakerIds = speakerIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();

        LogFanOut(Logger, Name, speakerIds.Count);

        var availabilityDict = new Dictionary<string, object>();

        try
        {
            // Fan-out: query all speakers concurrently
            var availableList = new List<SpeakerAvailability>();
            var unavailableList = new List<string>();

            await Parallel.ForEachAsync(
                speakerIds,
                new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = 3 },
                async (speakerId, innerCt) =>
                {
                    try
                    {
                        var availability = await _speakerAvailabilityMcp.GetAvailabilityAsync(
                            speakerId, startDate, endDate, innerCt);

                        lock (availableList)
                        {
                            availableList.Add(availability);
                        }

                        LogRetrieved(Logger, Name, speakerId);
                    }
                    catch (Exception ex)
                    {
                        LogRetrieveFailed(Logger, ex, Name, speakerId);

                        lock (unavailableList)
                        {
                            unavailableList.Add(speakerId);
                        }
                    }
                });

            availabilityDict["available"] = availableList;
            availabilityDict["unavailable"] = unavailableList;
            availabilityDict["retrievedAt"] = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            LogFanOutError(Logger, ex, Name);
            availabilityDict["error"] = ex.Message;
        }

        return availabilityDict;
    }

    /// <summary>
    /// Queries event history for pattern analysis.
    /// Parameters expected: "limit" (optional, default 5), "theme" (optional)
    /// </summary>
    private async Task<Dictionary<string, object>> GatherEventHistoryAsync(
        Dictionary<string, string> parameters,
        CancellationToken ct)
    {
        try
        {
            var limit = 5;
            if (parameters.TryGetValue("limit", out var limitStr) &&
                int.TryParse(limitStr, out var parsedLimit))
            {
                limit = parsedLimit;
            }

            var theme = parameters.TryGetValue("theme", out var themeValue) ? themeValue : null;

            LogQueryingHistory(Logger, Name, limit, theme ?? "any");

            var events = await _eventHistoryMcp.GetPastEventsAsync(limit: limit, theme: theme, ct: ct);

            return new Dictionary<string, object>
            {
                { "events", events },
                { "count", events.Count },
                { "retrievedAt", DateTimeOffset.UtcNow }
            };
        }
        catch (Exception ex)
        {
            LogHistoryError(Logger, ex, Name);
            return new Dictionary<string, object> { { "error", ex.Message } };
        }
    }

    #region Logging
    [LoggerMessage(Level = LogLevel.Information, Message = "[{Agent}] Gathering context for {QueryType}")]
    private static partial void LogGathering(ILogger logger, string agent, string queryType);

    [LoggerMessage(Level = LogLevel.Error, Message = "[{Agent}] Error gathering context for {QueryType}")]
    private static partial void LogContextError(ILogger logger, Exception ex, string agent, string queryType);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{Agent}] Fan-out query for {SpeakerCount} speakers")]
    private static partial void LogFanOut(ILogger logger, string agent, int speakerCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{Agent}] Retrieved availability for speaker {SpeakerId}")]
    private static partial void LogRetrieved(ILogger logger, string agent, string speakerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[{Agent}] Failed to retrieve availability for speaker {SpeakerId}")]
    private static partial void LogRetrieveFailed(ILogger logger, Exception ex, string agent, string speakerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "[{Agent}] Error during fan-out speaker availability query")]
    private static partial void LogFanOutError(ILogger logger, Exception ex, string agent);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{Agent}] Querying event history (limit: {Limit}, theme: {Theme})")]
    private static partial void LogQueryingHistory(ILogger logger, string agent, int limit, string theme);

    [LoggerMessage(Level = LogLevel.Error, Message = "[{Agent}] Error querying event history")]
    private static partial void LogHistoryError(ILogger logger, Exception ex, string agent);
    #endregion
}
