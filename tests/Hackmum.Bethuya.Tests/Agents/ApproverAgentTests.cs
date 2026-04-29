using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Tests.Agents.Builders;

namespace Hackmum.Bethuya.Tests.Agents;

/// <summary>
/// Phase 1: Approver Agent acceptance criteria tests.
/// Test stubs for Tank/Trinity to implement against in Phase 2.
/// </summary>
public sealed class ApproverAgentTests
{
    // Phase 1: All tests marked [Ignore] until Approver Agent implementation

    [Test]
    [Ignore("Awaiting Approver implementation — Phase 1")]
    public async Task PresentForApproval_WithAgendaDraft_ReturnsApprovalForm()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var draft = new AgendaDraft
        {
            EventId = eventId,
            Sessions = new[]
            {
                new SessionSlot { Title = "Opening Keynote", StartTime = "10:00", Duration = 30 },
                new SessionSlot { Title = "Workshop A", StartTime = "10:45", Duration = 60 }
            }
        };

        // Act & Assert — Tank/Trinity will implement
        await Assert.That(true).IsTrue();
    }

    [Test]
    [Ignore("Awaiting Approver implementation — Phase 1")]
    public async Task RecordApproval_WithApprovedDecision_UpdatesApprovalState()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var approval = ApprovalStateBuilder.CreateApprovalState()
            .WithEventId(eventId)
            .WithPhase(WorkflowPhase.Planning.ToString())
            .WithStatus(ApprovalStatus.Approved)
            .WithApprover("Organizer Alice")
            .WithApprovedAt(DateTime.UtcNow)
            .Build();

        // Act & Assert — Tank/Trinity will implement
        await Assert.That(approval.Status).IsEqualTo(ApprovalStatus.Approved);
        await Assert.That(approval.Approver).IsEqualTo("Organizer Alice");
    }

    [Test]
    [Ignore("Awaiting Approver implementation — Phase 1")]
    public async Task RecordApproval_WithRejectedDecision_AllowsResubmissionToAgent()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var rejection = ApprovalStateBuilder.CreateApprovalState()
            .WithEventId(eventId)
            .WithPhase(WorkflowPhase.Planning.ToString())
            .WithStatus(ApprovalStatus.Rejected)
            .WithApprover("Organizer Bob")
            .WithEdits("Please revise agenda — needs more DEI balance")
            .Build();

        // Act & Assert — Tank/Trinity will implement
        await Assert.That(rejection.Status).IsEqualTo(ApprovalStatus.Rejected);
        await Assert.That(rejection.Edits).Contains("DEI balance");
    }

    [Test]
    [Ignore("Awaiting Approver implementation — Phase 1")]
    public async Task EmitApprovalSignal_ToOrchestrator_AdvancesWorkflow()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var approval = ApprovalStateBuilder.CreateApprovalState()
            .WithEventId(eventId)
            .WithPhase(WorkflowPhase.Planning.ToString())
            .WithStatus(ApprovalStatus.Approved)
            .Build();

        // Act & Assert — Tank/Trinity will implement Orchestrator integration
        await Assert.That(approval.Status).IsEqualTo(ApprovalStatus.Approved);
    }

    [Test]
    [Ignore("Awaiting Approver implementation — Phase 1")]
    public async Task LogDecision_ToAuditLog_CreatesAuditTrail()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var approval = ApprovalStateBuilder.CreateApprovalState()
            .WithEventId(eventId)
            .WithPhase(WorkflowPhase.Planning.ToString())
            .WithStatus(ApprovalStatus.Approved)
            .WithApprover("Organizer Alice")
            .Build();

        // Act & Assert — Tank/Trinity will implement AuditLog integration
        await Assert.That(approval.Approver).IsEqualTo("Organizer Alice");
    }

    [Test]
    [Ignore("Awaiting Approver implementation — Phase 1")]
    public async Task CaptureHumanEdits_WithModifiedDraft_StoresDiff()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var originalDraft = new AgendaDraft
        {
            EventId = eventId,
            Sessions = new[]
            {
                new SessionSlot { Title = "Opening", StartTime = "10:00", Duration = 30 }
            }
        };
        var editedDraft = new AgendaDraft
        {
            EventId = eventId,
            Sessions = new[]
            {
                new SessionSlot { Title = "Opening Keynote", StartTime = "10:00", Duration = 45 }
            }
        };

        // Act & Assert — Tank/Trinity will implement diff generation
        await Assert.That(editedDraft.Sessions[0].Title).Contains("Keynote");
        await Assert.That(editedDraft.Sessions[0].Duration).IsEqualTo(45);
    }

    [Test]
    [Ignore("Awaiting Approver implementation — Phase 1")]
    public async Task RenderApprovalUI_ForHumanReview_UsesInteractiveServer()
    {
        // Arrange
        var eventId = EventDataBuilder.CreateEventId();
        var draft = new AgendaDraft
        {
            EventId = eventId,
            Sessions = new[]
            {
                new SessionSlot { Title = "Opening", StartTime = "10:00", Duration = 30 }
            }
        };

        // Act & Assert — Tank/Trinity will implement Blazor UI with InteractiveServer
        await Assert.That(draft.EventId).IsEqualTo(eventId);
    }
}

// Phase 1 placeholder types — Tank/Trinity will implement in src/

public sealed class AgendaDraft
{
    public Guid EventId { get; init; }
    public SessionSlot[] Sessions { get; init; } = Array.Empty<SessionSlot>();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public sealed class SessionSlot
{
    public required string Title { get; init; }
    public required string StartTime { get; init; }
    public int Duration { get; init; }
}
