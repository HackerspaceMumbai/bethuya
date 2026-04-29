using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.Agents.Implementations;
using Hackmum.Bethuya.Agents.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hackmum.Bethuya.Agents.Extensions;

public static class AgentServiceExtensions
{
    /// <summary>
    /// Registers all Bethuya domain agents and the approval workflow.
    /// </summary>
    public static IHostApplicationBuilder AddBethuyaAgents(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IAgent<PlannerRequest, PlannerResponse>, PlannerAgent>();
        builder.Services.AddScoped<IAgent<CuratorRequest, CuratorResponse>, CuratorAgent>();
        builder.Services.AddScoped<IAgent<FacilitatorRequest, FacilitatorResponse>, FacilitatorAgent>();
        builder.Services.AddScoped<IAgent<ReporterRequest, ReporterResponse>, ReporterAgent>();
        builder.Services.AddScoped<IAgent<OrchestratorRequest, OrchestratorResponse>, OrchestratorAgent>();
        builder.Services.AddScoped<IAgent<ScoutRequest, ScoutResponse>, ScoutAgent>();
        builder.Services.AddScoped<IAgent<AuditorRequest, AuditorResponse>, AuditorAgent>();
        builder.Services.AddScoped<ApprovalWorkflow>();

        return builder;
    }
}
