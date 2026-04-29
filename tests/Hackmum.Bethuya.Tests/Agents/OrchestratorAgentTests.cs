using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.Agents.Implementations;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Tests.Agents.Builders;
using NSubstitute;
using TUnit.Core;

namespace Hackmum.Bethuya.Tests.Agents;

/// <summary>
/// Phase 1: Orchestrator Agent acceptance criteria tests.
/// Tests existing Orchestrator implementation for Phase 1 requirements.
/// </summary>
public sealed class OrchestratorAgentTests
{
    // Phase 1: Tests marked [Ignore] until full integration with DB context

    [Test]
    // [Ignore - Phase 1 stub]
    public async Task SpawnAgent_WithValidRequest_ReturnsSpawnedStatus()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var request = new SpawnAgentRequest(
            EventId: eventId,
            AgentType: "Planner",
            Context: "Theme: GenAI");

        var orchestrator = CreateOrchestratorStub();

        // Act
        var result = await orchestrator.SpawnAgentAsync(request);

        // Assert
        await Assert.That(result.Status).IsEqualTo("Spawned");
        await Assert.That(result.AgentName).IsEqualTo("Planner");
        await Assert.That(result.EventId).IsEqualTo(eventId);
    }

    [Test]
    // [Ignore - Phase 1 stub]
    public async Task AdvanceWorkflow_FromPlanningToCuration_TransitionsSuccessfully()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var orchestrator = CreateOrchestratorStub();

        // Simulate approval for planning phase
        var approvalState = ApprovalStateBuilder.CreateApprovalState()
            .WithEventId(eventId)
            .WithPhase(WorkflowPhase.Planning.ToString())
            .WithStatus(ApprovalStatus.Approved)
            .Build();

        var request = new AdvanceWorkflowRequest(
            EventId: eventId,
            TargetPhase: WorkflowPhase.Curation);

        // Act
        var response = await orchestrator.AdvanceWorkflowAsync(request);

        // Assert
        await Assert.That(response.EventId).IsEqualTo(eventId);
        await Assert.That(response.CurrentPhase).IsEqualTo(WorkflowPhase.Curation);
        await Assert.That(response.Status).IsEqualTo(WorkflowStatus.InProgress);
    }

    [Test]
    // [Ignore - Phase 1 stub]
    public async Task AdvanceWorkflow_WithoutApproval_ReturnsPendingApprovalStatus()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var orchestrator = CreateOrchestratorStub();

        var request = new AdvanceWorkflowRequest(
            EventId: eventId,
            TargetPhase: WorkflowPhase.Curation);

        // Act
        var response = await orchestrator.AdvanceWorkflowAsync(request);

        // Assert
        await Assert.That(response.Status).IsEqualTo(WorkflowStatus.PendingApproval);
        await Assert.That(response.RequiresApproval).IsTrue();
    }

    [Test]
    // [Ignore - Phase 1 stub]
    public async Task WorkflowState_LogsToDatabase_AfterPhaseTransition()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var orchestrator = CreateOrchestratorStub();

        await orchestrator.SpawnAgentAsync(new SpawnAgentRequest(
            EventId: eventId,
            AgentType: "Planner",
            Context: "Theme: GenAI"));

        var request = new AdvanceWorkflowRequest(
            EventId: eventId,
            TargetPhase: WorkflowPhase.Curation);

        // Act
        await orchestrator.AdvanceWorkflowAsync(request);

        // Assert — Verify workflow state updated in DB
        // Requires DB context integration
        // Stub: Tank will implement assertion logic
    }

    [Test]
    // [Ignore - Phase 1 stub]
    public async Task GetWorkflowStatus_WithInitializedWorkflow_ReturnsCurrentPhase()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var orchestrator = CreateOrchestratorStub();

        var request = new OrchestratorRequest(
            EventId: eventId,
            WorkflowType: "EventPlanning");

        // Act
        var response = await orchestrator.DraftAsync(request);

        // Assert
        await Assert.That(response.CurrentPhase).IsEqualTo(WorkflowPhase.Planning);
        await Assert.That(response.Status).IsEqualTo(WorkflowStatus.InProgress);
    }

    [Test]
    // [Ignore - Phase 1 stub]
    public async Task EnforceSequencing_NoCurationBeforePlanningApproval_ReturnsPendingApproval()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var orchestrator = CreateOrchestratorStub();

        // Initialize workflow in Planning phase
        await orchestrator.DraftAsync(new OrchestratorRequest(
            EventId: eventId,
            WorkflowType: "EventPlanning"));

        // Attempt to advance to Curation without approval
        var request = new AdvanceWorkflowRequest(
            EventId: eventId,
            TargetPhase: WorkflowPhase.Curation);

        // Act
        var response = await orchestrator.AdvanceWorkflowAsync(request);

        // Assert
        await Assert.That(response.Status).IsEqualTo(WorkflowStatus.PendingApproval);
        await Assert.That(response.RequiresApproval).IsTrue();
    }

    [Test]
    // [Ignore - Phase 1 stub]
    public async Task AuditLog_RecordsAgentSpawn_WithEventIdAndAgentType()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var orchestrator = CreateOrchestratorStub();

        var request = new SpawnAgentRequest(
            EventId: eventId,
            AgentType: "Planner",
            Context: "Theme: GenAI");

        // Act
        await orchestrator.SpawnAgentAsync(request);

        // Assert — Verify audit log entry created
        // Requires DB context integration
        // Stub: Tank will implement assertion logic
    }

    [Test]
    // [Ignore - Phase 1 stub]
    public async Task AuditLog_RecordsWorkflowAdvancement_WithPhaseTransition()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var orchestrator = CreateOrchestratorStub();

        // Simulate approval
        var approvalState = ApprovalStateBuilder.CreateApprovalState()
            .WithEventId(eventId)
            .WithPhase(WorkflowPhase.Planning.ToString())
            .WithStatus(ApprovalStatus.Approved)
            .Build();

        var request = new AdvanceWorkflowRequest(
            EventId: eventId,
            TargetPhase: WorkflowPhase.Curation);

        // Act
        await orchestrator.AdvanceWorkflowAsync(request);

        // Assert — Verify audit log entry created for phase transition
        // Stub: Tank will implement assertion logic
    }

    // Stub factory — Tank/Trinity will replace with full DB integration in Phase 2

    private static OrchestratorAgent CreateOrchestratorStub()
    {
        throw new NotImplementedException("Phase 1: Full DB integration pending — Phase 2");
    }
}
