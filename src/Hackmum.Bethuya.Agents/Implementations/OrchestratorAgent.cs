using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.AI.Routing;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Hackmum.Bethuya.Agents.Implementations;

/// <summary>
/// Orchestrator Agent — coordinates workflow phases (planning → curation → reporting).
/// Spawns domain agents and manages approval gates.
/// Uses Azure OpenAI (non-sensitive orchestration logic).
/// </summary>
public sealed partial class OrchestratorAgent(
    IAIRouter router,
    ILogger<OrchestratorAgent> logger,
    BethuyaDbContext dbContext)
    : AgentBase<OrchestratorRequest, OrchestratorResponse>(router, logger)
{
    public override string Name => "Orchestrator";

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Spawning {AgentType} for event {EventId}")]
    private static partial void LogSpawningAgent(ILogger logger, string agentType, Guid eventId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Advancing workflow for event {EventId} to phase {TargetPhase}")]
    private static partial void LogAdvancingWorkflow(ILogger logger, Guid eventId, WorkflowPhase targetPhase);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Cannot advance from {CurrentPhase} to {TargetPhase} without approval")]
    private static partial void LogMissingApproval(ILogger logger, WorkflowPhase currentPhase, WorkflowPhase targetPhase);

    protected override IList<ChatMessage> BuildPrompt(OrchestratorRequest request)
    {
        var systemPrompt = """
            You are the Orchestrator Agent for Bethuya event management.
            Your job is to coordinate the event workflow across three phases:
            1. Planning (Planner Agent drafts agenda)
            2. Curation (Curator Agent selects attendees)
            3. Reporting (Reporter Agent summarizes outcomes)
            
            Each phase requires human approval before advancing to the next.
            You must track workflow state and ensure proper sequencing.
            """;

        var userPrompt = $"""
            Event ID: {request.EventId}
            Workflow Type: {request.WorkflowType}
            
            Determine the current workflow phase and next action required.
            """;

        return
        [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ];
    }

    protected override OrchestratorResponse ParseResponse(ChatResponse response, OrchestratorRequest request)
    {
        // Check if workflow state exists
        var workflowState = dbContext.WorkflowStates
            .FirstOrDefault(w => w.EventId == request.EventId);

        if (workflowState is null)
        {
            // Initialize workflow
            workflowState = new WorkflowState
            {
                EventId = request.EventId,
                CurrentPhase = WorkflowPhase.Planning,
                Status = WorkflowStatus.InProgress,
                LastUpdated = DateTime.UtcNow
            };

            dbContext.WorkflowStates.Add(workflowState);
            dbContext.SaveChanges();

            // Log to audit
            LogAuditEntry(request.EventId, "WorkflowInitialized", Name, "Started planning phase");
        }

        return new OrchestratorResponse(
            EventId: request.EventId,
            CurrentPhase: workflowState.CurrentPhase,
            Status: workflowState.Status,
            Message: $"Workflow is in {workflowState.CurrentPhase} phase with status {workflowState.Status}.");
    }

    public async Task<SpawnAgentResponse> SpawnAgentAsync(SpawnAgentRequest request, CancellationToken ct = default)
    {
        LogSpawningAgent(logger, request.AgentType, request.EventId);

        // Log to audit
        await LogAuditEntryAsync(request.EventId, "AgentSpawned", Name, $"Spawned {request.AgentType} agent");

        return new SpawnAgentResponse(
            AgentName: request.AgentType,
            EventId: request.EventId,
            Status: "Spawned");
    }

    public async Task<AdvanceWorkflowResponse> AdvanceWorkflowAsync(
        AdvanceWorkflowRequest request,
        CancellationToken ct = default)
    {
        LogAdvancingWorkflow(logger, request.EventId, request.TargetPhase);

        var workflowState = await dbContext.WorkflowStates
            .FirstOrDefaultAsync(w => w.EventId == request.EventId, ct);

        if (workflowState is null)
        {
            throw new InvalidOperationException($"Workflow state not found for event {request.EventId}");
        }

        // Check if current phase has approval
        var hasApproval = await HasApprovalAsync(request.EventId, workflowState.CurrentPhase, ct);

        if (!hasApproval && workflowState.CurrentPhase != request.TargetPhase)
        {
            LogMissingApproval(logger, workflowState.CurrentPhase, request.TargetPhase);

            return new AdvanceWorkflowResponse(
                EventId: request.EventId,
                CurrentPhase: workflowState.CurrentPhase,
                Status: WorkflowStatus.PendingApproval,
                RequiresApproval: true);
        }

        // Update workflow state
        workflowState.CurrentPhase = request.TargetPhase;
        workflowState.Status = WorkflowStatus.InProgress;
        workflowState.LastUpdated = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        // Log to audit
        await LogAuditEntryAsync(request.EventId, "WorkflowAdvanced", Name, $"Advanced to {request.TargetPhase}");

        return new AdvanceWorkflowResponse(
            EventId: request.EventId,
            CurrentPhase: request.TargetPhase,
            Status: WorkflowStatus.InProgress,
            RequiresApproval: request.TargetPhase != WorkflowPhase.Completed);
    }

    private async Task<bool> HasApprovalAsync(Guid eventId, WorkflowPhase phase, CancellationToken ct)
    {
        var approval = await dbContext.ApprovalStates
            .Where(a => a.EventId == eventId && a.WorkflowPhase == phase.ToString())
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return approval?.Status == ApprovalStatus.Approved;
    }

    private void LogAuditEntry(Guid eventId, string action, string actor, string reason)
    {
        var entry = new AuditLog
        {
            EventId = eventId,
            Action = action,
            Actor = actor,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        };

        dbContext.AuditLogs.Add(entry);
        dbContext.SaveChanges();
    }

    private async Task LogAuditEntryAsync(Guid eventId, string action, string actor, string reason)
    {
        var entry = new AuditLog
        {
            EventId = eventId,
            Action = action,
            Actor = actor,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        };

        dbContext.AuditLogs.Add(entry);
        await dbContext.SaveChangesAsync();
    }
}
