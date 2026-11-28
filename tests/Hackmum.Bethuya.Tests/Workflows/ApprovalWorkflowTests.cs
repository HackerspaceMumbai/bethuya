using Hackmum.Bethuya.Agents.Workflows;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hackmum.Bethuya.Tests.Workflows;

public class ApprovalWorkflowTests
{
    private readonly ApprovalWorkflow _workflow;

    public ApprovalWorkflowTests()
    {
        var logger = NullLoggerFactory.Instance.CreateLogger<ApprovalWorkflow>();
        _workflow = new ApprovalWorkflow(logger);
    }

    [Test]
    public async Task CreateDecision_ReturnsPendingDecision()
    {
        var decision = _workflow.CreateDecision("Agenda", Guid.NewGuid(), "admin@hackmum.org");

        await Assert.That(decision.Status).IsEqualTo(DecisionStatus.Pending);
        await Assert.That(decision.EntityType).IsEqualTo("Agenda");
    }

    [Test]
    public async Task Approve_SetStatusToApplied()
    {
        var decision = _workflow.CreateDecision("Agenda", Guid.NewGuid(), "admin@hackmum.org");
        _workflow.Approve(decision, "Looks good");

        await Assert.That(decision.Status).IsEqualTo(DecisionStatus.Applied);
        await Assert.That(decision.Type).IsEqualTo(DecisionType.Approve);
        await Assert.That(decision.Reason).IsEqualTo("Looks good");
    }

    [Test]
    public async Task Reject_SetStatusToApplied()
    {
        var decision = _workflow.CreateDecision("Proposal", Guid.NewGuid(), "admin@hackmum.org");
        _workflow.Reject(decision, "Needs revision");

        await Assert.That(decision.Status).IsEqualTo(DecisionStatus.Applied);
        await Assert.That(decision.Type).IsEqualTo(DecisionType.Reject);
        await Assert.That(decision.Reason).IsEqualTo("Needs revision");
    }

    [Test]
    public async Task Approve_AlreadyApplied_DoesNotChange()
    {
        var decision = _workflow.CreateDecision("Agenda", Guid.NewGuid(), "admin@hackmum.org");
        _workflow.Approve(decision, "First approval");

        // Try to approve again — should not change
        _workflow.Approve(decision, "Second approval");

        await Assert.That(decision.Reason).IsEqualTo("First approval");
    }

    [Test]
    public async Task Reject_AlreadyApplied_DoesNotChange()
    {
        var decision = _workflow.CreateDecision("Agenda", Guid.NewGuid(), "admin@hackmum.org");
        _workflow.Reject(decision, "Rejected");

        // Try to reject again
        _workflow.Reject(decision, "Rejected again");

        await Assert.That(decision.Reason).IsEqualTo("Rejected");
    }

    [Test]
    public async Task CreateDecision_WithDiff_StoresDiff()
    {
        var diff = "- Old title\n+ New title";
        var decision = _workflow.CreateDecision("Event", Guid.NewGuid(), "admin@hackmum.org", diff);

        await Assert.That(decision.Diff).IsEqualTo(diff);
    }
}
