using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Tests.Agents;

public class AgentContractTests
{
    [Test]
    public async Task PlannerRequest_HasCorrectSensitivity()
    {
        var evt = new Event { Title = "Test", CreatedBy = "test@test.com" };
        var request = new PlannerRequest(evt);

        await Assert.That(request.AgentName).IsEqualTo("Planner");
        await Assert.That(request.Sensitivity).IsEqualTo(DataSensitivity.NonSensitive);
    }

    [Test]
    public async Task CuratorRequest_IsSensitive()
    {
        var evt = new Event { Title = "Test", CreatedBy = "test@test.com", Capacity = 50 };
        var registrations = new List<Registration>();
        var budget = new FairnessBudget();

        var request = new CuratorRequest(evt, registrations, budget);

        await Assert.That(request.AgentName).IsEqualTo("Curator");
        await Assert.That(request.Sensitivity).IsEqualTo(DataSensitivity.Sensitive);
    }

    [Test]
    public async Task FacilitatorRequest_IsNonSensitive()
    {
        var evt = new Event { Title = "Test", CreatedBy = "test@test.com" };
        var agenda = new Agenda { EventId = evt.Id };

        var request = new FacilitatorRequest(evt, agenda);

        await Assert.That(request.AgentName).IsEqualTo("Facilitator");
        await Assert.That(request.Sensitivity).IsEqualTo(DataSensitivity.NonSensitive);
    }

    [Test]
    public async Task ReporterRequest_IsNonSensitive()
    {
        var evt = new Event { Title = "Test", CreatedBy = "test@test.com" };
        var request = new ReporterRequest(evt);

        await Assert.That(request.AgentName).IsEqualTo("Reporter");
        await Assert.That(request.Sensitivity).IsEqualTo(DataSensitivity.NonSensitive);
    }

    [Test]
    public async Task AllResponses_RequireHumanApproval()
    {
        var evt = new Event { Title = "Test", CreatedBy = "test@test.com" };
        var agenda = new Agenda { EventId = evt.Id };
        var report = new EventReport { EventId = evt.Id };

        var plannerResp = new PlannerResponse(agenda);
        var reporterResp = new ReporterResponse(report);

        await Assert.That(plannerResp.RequiresHumanApproval).IsTrue();
        await Assert.That(reporterResp.RequiresHumanApproval).IsTrue();
    }
}
