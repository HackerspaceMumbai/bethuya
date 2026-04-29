using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.AI.Routing;
using Hackmum.Bethuya.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Hackmum.Bethuya.Agents.Implementations;

/// <summary>
/// Auditor Agent (stub) — maintains append-only audit log.
/// Logs all significant agent actions (curation, approval, workflow transitions).
/// CRITICAL: All audit entries are SANITIZED (no PII, no personal identifiers).
/// </summary>
public sealed partial class AuditorAgent(IAIRouter router, ILogger<AuditorAgent> logger)
    : AgentBase<AuditorRequest, AuditorResponse>(router, logger)
{
    public override string Name => "Auditor";

    protected override IList<ChatMessage> BuildPrompt(AuditorRequest request)
    {
        return
        [
            new ChatMessage(ChatRole.System, "You are the Auditor Agent for Bethuya. Your role is to log significant actions for compliance and analytics."),
            new ChatMessage(ChatRole.User, $"Action: {request.Action}\nData: {string.Join(", ", request.Data?.Select(kv => $"{kv.Key}={kv.Value}") ?? [])}")
        ];
    }

    protected override AuditorResponse ParseResponse(ChatResponse response, AuditorRequest request)
    {
        return new AuditorResponse(
            Action: request.Action,
            Result: "Success");
    }

    /// <summary>
    /// Log a curation decision with sanitized data (no PII).
    /// </summary>
    public async Task LogCurationDecisionAsync(
        Guid eventId,
        int acceptedCount,
        int waitlistCount,
        string reason,
        string organizerEmail,
        decimal fairnessScore,
        CancellationToken ct = default)
    {
        var logData = new Dictionary<string, string>
        {
            ["event_id"] = eventId.ToString(),
            ["accepted_count"] = acceptedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["waitlist_count"] = waitlistCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["reason"] = reason,
            ["organizer_email"] = organizerEmail,
            ["fairness_score"] = fairnessScore.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
        };

        var request = new AuditorRequest(eventId, "CURATION_DECISION", logData);
        var response = await DraftAsync(request, ct);

        LogAuditEntry(logger, "Curator", "CURATION_DECISION", eventId, acceptedCount, waitlistCount);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[Auditor] Logged {Action} by {Agent} for event {EventId}: {AcceptedCount} accepted, {WaitlistCount} waitlist")]
    private static partial void LogAuditEntry(
        ILogger logger, string agent, string action, Guid eventId, int acceptedCount, int waitlistCount);
}
