using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Microsoft.Extensions.Logging;

namespace Hackmum.Bethuya.Agents.Workflows;

/// <summary>
/// Human-in-the-loop approval workflow with diff generation and audit trail.
/// </summary>
public sealed partial class ApprovalWorkflow(ILogger<ApprovalWorkflow> logger)
{
    /// <summary>
    /// Creates a decision record for human review.
    /// </summary>
    public Decision CreateDecision(string entityType, Guid entityId, string decidedBy, string? diff = null)
    {
        LogCreatingDecision(logger, entityType, entityId, decidedBy);

        return new Decision
        {
            EntityType = entityType,
            EntityId = entityId,
            Type = DecisionType.Approve,
            Status = DecisionStatus.Pending,
            DecidedBy = decidedBy,
            Diff = diff
        };
    }

    /// <summary>
    /// Approves a pending decision.
    /// </summary>
    public void Approve(Decision decision, string? reason = null)
    {
        if (decision.Status != DecisionStatus.Pending)
        {
            LogInvalidStatus(logger, decision.Id, decision.Status);
            return;
        }

        decision.Type = DecisionType.Approve;
        decision.Status = DecisionStatus.Applied;
        decision.Reason = reason;

        LogApproved(logger, decision.Id, decision.DecidedBy, reason ?? "No reason provided");
    }

    /// <summary>
    /// Rejects a pending decision.
    /// </summary>
    public void Reject(Decision decision, string reason)
    {
        if (decision.Status != DecisionStatus.Pending)
        {
            LogInvalidStatus(logger, decision.Id, decision.Status);
            return;
        }

        decision.Type = DecisionType.Reject;
        decision.Status = DecisionStatus.Applied;
        decision.Reason = reason;

        LogRejected(logger, decision.Id, decision.DecidedBy, reason);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Creating approval decision for {EntityType} {EntityId} by {User}")]
    private static partial void LogCreatingDecision(ILogger logger, string entityType, Guid entityId, string user);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Cannot modify decision {Id} — status is {Status}")]
    private static partial void LogInvalidStatus(ILogger logger, Guid id, DecisionStatus status);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Decision {Id} approved by {User}: {Reason}")]
    private static partial void LogApproved(ILogger logger, Guid id, string user, string reason);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Decision {Id} rejected by {User}: {Reason}")]
    private static partial void LogRejected(ILogger logger, Guid id, string user, string reason);
}
