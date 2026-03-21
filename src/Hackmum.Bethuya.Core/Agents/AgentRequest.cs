using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Agents;

public abstract record AgentRequest(
    string AgentName,
    DataSensitivity Sensitivity = DataSensitivity.NonSensitive,
    string? RequestedBy = null) : IAgentRequest;
