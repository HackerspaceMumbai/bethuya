using Hackmum.Bethuya.Core.Agents;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Agents.Contracts;

public sealed record FacilitatorRequest(
    Event Event,
    Agenda Agenda,
    string? CurrentSessionTitle = null,
    string? RequestedBy = null)
    : AgentRequest("Facilitator", DataSensitivity.NonSensitive, RequestedBy);

public sealed record FacilitatorResponse(
    List<string> SuggestedPrompts,
    List<string> QASuggestions,
    string? SessionNotes = null,
    bool RequiresHumanApproval = true,
    string? AgentReasoning = null)
    : AgentResponse(RequiresHumanApproval, AgentReasoning);
