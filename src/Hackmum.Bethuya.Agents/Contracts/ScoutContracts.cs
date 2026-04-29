using Hackmum.Bethuya.Core.Agents;
using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Agents.Contracts;

/// <summary>
/// Request for Scout agent to gather external context.
/// </summary>
/// <param name="EventId">Event identifier.</param>
/// <param name="QueryType">Type of context to gather (Speaker, Venue, Community).</param>
/// <param name="Parameters">Query-specific parameters.</param>
public sealed record ScoutRequest(
    Guid EventId,
    string QueryType,
    Dictionary<string, string>? Parameters) : IAgentRequest
{
    public string AgentName => "Scout";
    public DataSensitivity Sensitivity => DataSensitivity.NonSensitive;
    public string? RequestedBy => null;
}

/// <summary>
/// Response from Scout agent with gathered context.
/// </summary>
/// <param name="QueryType">Type of context gathered.</param>
/// <param name="Data">Structured data from external sources.</param>
public sealed record ScoutResponse(
    string QueryType,
    Dictionary<string, object> Data) : IAgentResponse
{
    public bool RequiresHumanApproval => false;
    public string? AgentReasoning => null;
    public DateTimeOffset GeneratedAt => DateTimeOffset.UtcNow;
}
