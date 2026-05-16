namespace Hackmum.Bethuya.Backend.Agents;

/// <summary>
/// Reserved invoker for future Option B delegation via A2A.
/// </summary>
public sealed class FoundryA2AInvoker : IAgentInvoker
{
    public Task<PlannerInvocationResult> InvokePlannerAsync(
        PlannerInvocationInput input,
        string conversationId,
        string workItemId,
        string? traceParent,
        string? correlationId,
        CancellationToken ct = default) =>
        throw new NotSupportedException("FoundryA2AInvoker is reserved for Option B and is not enabled in Option A.");
}

