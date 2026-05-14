namespace Hackmum.Bethuya.Backend.Agents;

/// <summary>
/// Planner invoker settings.
/// </summary>
public sealed class PlannerInvokerOptions
{
    public const string SectionName = "PlannerInvoker";
    public string BaseUrl { get; set; } = "https+http://planner-hosted";
    public string AgentName { get; set; } = "planner-hosted";
    public string AgentVersionTag { get; set; } = "v1";
    public string Model { get; set; } = "planner-chat";
}

