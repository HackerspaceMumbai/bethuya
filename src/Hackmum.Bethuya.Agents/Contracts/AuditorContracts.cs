using Hackmum.Bethuya.Core.Agents;
using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Agents.Contracts;

/// <summary>
/// Request for Auditor agent to query or append audit logs.
/// </summary>
/// <param name="EventId">Event identifier.</param>
/// <param name="Action">Action to perform (Query, Append).</param>
/// <param name="Data">Action-specific data.</param>
public sealed record AuditorRequest(
    Guid EventId,
    string Action,
    Dictionary<string, string>? Data) : IAgentRequest
{
    public string AgentName => "Auditor";
    public DataSensitivity Sensitivity => DataSensitivity.NonSensitive;
    public string? RequestedBy => null;
}

/// <summary>
/// Response from Auditor agent.
/// </summary>
/// <param name="Action">Action performed.</param>
/// <param name="Result">Action result.</param>
public sealed record AuditorResponse(
    string Action,
    string Result) : IAgentResponse
{
    public bool RequiresHumanApproval => false;
    public string? AgentReasoning => null;
    public DateTimeOffset GeneratedAt => DateTimeOffset.UtcNow;
}
