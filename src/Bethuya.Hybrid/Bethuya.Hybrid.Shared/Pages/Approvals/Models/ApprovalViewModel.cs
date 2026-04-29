namespace Bethuya.Hybrid.Shared.Pages.Approvals.Models;

public sealed class ApprovalViewModel
{
    public required Guid EventId { get; init; }
    public required string WorkflowPhase { get; init; }
    public required string Status { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? Approver { get; init; }
    public string? Edits { get; init; }
}

public sealed class PlanDraftViewModel
{
    public required Guid EventId { get; init; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public List<SessionDraftViewModel> Sessions { get; set; } = [];
    public DateTime CreatedAt { get; init; }
    public string? AgentReasoning { get; init; }
}

public sealed class SessionDraftViewModel
{
    public required string Id { get; init; }
    public required string Title { get; set; }
    public string Speaker { get; set; } = "";
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Description { get; set; } = "";
}

public sealed class CurationProposalViewModel
{
    public required Guid EventId { get; init; }
    public int ProposedAttendees { get; init; }
    public int WaitlistCount { get; init; }
    public float FirstTimePercentage { get; init; }
    public float UnderrepresentedPercentage { get; init; }
    public string? FairnessBudgetSummary { get; init; }
    public string? AgentReasoning { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class ReportDraftViewModel
{
    public required Guid EventId { get; init; }
    public required string Title { get; set; }
    public required string Summary { get; set; }
    public List<string> Highlights { get; set; } = [];
    public List<string> ActionItems { get; set; } = [];
    public DateTime CreatedAt { get; init; }
    public string? AgentReasoning { get; init; }
}

public sealed class ApprovalActionRequest
{
    public required Guid EventId { get; init; }
    public required string WorkflowPhase { get; init; }
    public required bool Approved { get; init; }
    public string? Edits { get; init; }
    public string? Reason { get; init; }
}
