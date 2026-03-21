using Hackmum.Bethuya.Core.Agents;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Agents.Contracts;

public sealed record ReporterRequest(
    Event Event,
    string? SessionNotes = null,
    string? RequestedBy = null)
    : AgentRequest("Reporter", DataSensitivity.NonSensitive, RequestedBy);

public sealed record ReporterResponse(
    EventReport DraftReport,
    bool RequiresHumanApproval = true,
    string? AgentReasoning = null)
    : AgentResponse(RequiresHumanApproval, AgentReasoning);
