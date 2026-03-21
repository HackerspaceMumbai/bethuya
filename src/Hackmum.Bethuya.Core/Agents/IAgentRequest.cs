using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Agents;

public interface IAgentRequest
{
    string AgentName { get; }
    DataSensitivity Sensitivity { get; }
    string? RequestedBy { get; }
}
