using Hackmum.Bethuya.Agents.MCP.Contracts;
using Hackmum.Bethuya.Core.Agents;
using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Agents.Messages;

/// <summary>
/// Query sent by Planner to Scout to gather speaker availability information.
/// </summary>
public sealed record GetSpeakerAvailabilityQuery(
    Guid EventId,
    DateOnly EventStartDate,
    DateOnly EventEndDate,
    List<string> RequestedSpeakerIds,
    string? RequestedBy = null) : IAgentRequest
{
    public string AgentName => "Scout";
    public DataSensitivity Sensitivity => DataSensitivity.NonSensitive;
}

/// <summary>
/// Response from Scout containing aggregated speaker availability information.
/// </summary>
public sealed record SpeakerAvailabilityResponse(
    Guid EventId,
    List<SpeakerAvailability> AvailableSpeakers,
    List<string> UnavailableSpeakers,
    DateTimeOffset RetrievedAt) : IAgentResponse
{
    public bool RequiresHumanApproval => false;
    public string? AgentReasoning => null;
    public DateTimeOffset GeneratedAt => RetrievedAt;
}
