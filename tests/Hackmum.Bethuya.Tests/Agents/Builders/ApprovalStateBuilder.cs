using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Tests.Agents.Builders;

/// <summary>
/// Fluent builder for approval state test data.
/// </summary>
public sealed class ApprovalStateBuilder
{
    private Guid _eventId = Guid.CreateVersion7();
    private string _workflowPhase = "Planning";
    private ApprovalStatus _status = ApprovalStatus.Pending;
    private string? _approver;
    private DateTime? _approvedAt;
    private string? _edits;

    public static ApprovalStateBuilder CreateApprovalState() => new();

    public ApprovalStateBuilder WithEventId(Guid eventId)
    {
        _eventId = eventId;
        return this;
    }

    public ApprovalStateBuilder WithPhase(string phase)
    {
        _workflowPhase = phase;
        return this;
    }

    public ApprovalStateBuilder WithStatus(ApprovalStatus status)
    {
        _status = status;
        return this;
    }

    public ApprovalStateBuilder WithApprover(string approver)
    {
        _approver = approver;
        return this;
    }

    public ApprovalStateBuilder WithApprovedAt(DateTime approvedAt)
    {
        _approvedAt = approvedAt;
        return this;
    }

    public ApprovalStateBuilder WithEdits(string edits)
    {
        _edits = edits;
        return this;
    }

    public ApprovalState Build()
    {
        return new ApprovalState
        {
            EventId = _eventId,
            WorkflowPhase = _workflowPhase,
            Status = _status,
            Approver = _approver,
            ApprovedAt = _approvedAt,
            Edits = _edits
        };
    }
}
