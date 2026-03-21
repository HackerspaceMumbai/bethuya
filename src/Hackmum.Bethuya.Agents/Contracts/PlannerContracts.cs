using Hackmum.Bethuya.Core.Agents;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Agents.Contracts;

public sealed record PlannerRequest(
    Event Event,
    string? Constraints = null,
    string? PriorEventsContext = null,
    string? RequestedBy = null)
    : AgentRequest("Planner", DataSensitivity.NonSensitive, RequestedBy);

public sealed record PlannerResponse(
    Agenda DraftAgenda,
    bool RequiresHumanApproval = true,
    string? AgentReasoning = null)
    : AgentResponse(RequiresHumanApproval, AgentReasoning);
