using Hackmum.Bethuya.Core.Agents;

namespace Hackmum.Bethuya.Agents.Base;

/// <summary>
/// Contract for all Bethuya domain agents.
/// Every agent follows the Draft → Review → Approve/Reject → Finalize pattern.
/// </summary>
public interface IAgent<in TRequest, TResponse>
    where TRequest : IAgentRequest
    where TResponse : IAgentResponse
{
    string Name { get; }
    Task<TResponse> DraftAsync(TRequest request, CancellationToken ct = default);
}
