using Hackmum.Bethuya.Core.Agents;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Agents.Contracts;

/// <summary>
/// Curator operates on SENSITIVE data (attendee PII) — always routes to Foundry Local.
/// NEVER auto-accepts or auto-rejects. Outputs are recommendations only.
/// </summary>
public sealed record CuratorRequest(
    Event Event,
    IReadOnlyList<Registration> Registrations,
    FairnessBudget Budget,
    string? RequestedBy = null)
    : AgentRequest("Curator", DataSensitivity.Sensitive, RequestedBy);

public sealed record CuratorResponse(
    AttendanceProposal Proposal,
    WaitlistProposal Waitlist,
    CurationInsights Insights,
    bool RequiresHumanApproval = true,
    string? AgentReasoning = null)
    : AgentResponse(RequiresHumanApproval, AgentReasoning);
