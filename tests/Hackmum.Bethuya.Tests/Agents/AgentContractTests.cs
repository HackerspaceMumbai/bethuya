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

    [Test]
    public async Task PlannerCuratorReporterResponses_SupportSharedRecommendationEvidenceSchema()
    {
        RecommendationEnvelope recommendation = new(
            SchemaVersion: "1.0",
            RecommendationKind: "test-kind",
            Audience: "organizer",
            Headline: "Test recommendation headline",
            Summary: "Test recommendation summary",
            Actions:
            [
                new RecommendationAction(
                    ActionKey: "action-1",
                    Title: "Action title",
                    Rationale: "Action rationale",
                    Priority: "high")
            ],
            Evidence:
            [
                new RecommendationEvidence(
                    EvidenceKey: "evidence-1",
                    Observation: "Observation detail",
                    Source: "test-suite",
                    MetricValue: 42,
                    MetricUnit: "count",
                    Confidence: "high")
            ]);

        var evt = new Event { Title = "Test", CreatedBy = "test@test.com", Capacity = 50 };
        var agenda = new Agenda { EventId = evt.Id };
        var report = new EventReport { EventId = evt.Id };
        var proposal = new AttendanceProposal { EventId = evt.Id };
        var waitlist = new WaitlistProposal { EventId = evt.Id };
        var insights = new CurationInsights();

        var plannerResponse = new PlannerResponse(agenda, Recommendation: recommendation);
        var curatorResponse = new CuratorResponse(proposal, waitlist, insights, Recommendation: recommendation);
        var reporterResponse = new ReporterResponse(report, Recommendation: recommendation);

        await Assert.That(plannerResponse.Recommendation).IsNotNull();
        await Assert.That(curatorResponse.Recommendation).IsNotNull();
        await Assert.That(reporterResponse.Recommendation).IsNotNull();
        await Assert.That(plannerResponse.Recommendation!.Evidence.Count).IsEqualTo(1);
        await Assert.That(curatorResponse.Recommendation!.Actions[0].ActionKey).IsEqualTo("action-1");
        await Assert.That(reporterResponse.Recommendation!.SchemaVersion).IsEqualTo("1.0");
    }
}
