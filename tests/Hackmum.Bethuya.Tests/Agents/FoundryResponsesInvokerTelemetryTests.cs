using System.Diagnostics;
using Hackmum.Bethuya.Backend.Agents;
using Hackmum.Bethuya.Core.Models;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Tests.Agents;

public sealed class FoundryResponsesInvokerTelemetryTests
{
    [Test]
    public async Task InvokePlannerAsync_AddsExpectedGenAiAndMcpTags_ToCurrentActivity()
    {
        var responsesApi = new FakePlannerResponsesApi();
        var options = Options.Create(new PlannerInvokerOptions { Model = "planner-chat" });
        var invoker = new FoundryResponsesInvoker(responsesApi, options);

        using var activity = new Activity("planner-invocation-test");
        activity.Start();

        _ = await invoker.InvokePlannerAsync(
            new PlannerInvocationInput(
                EventId: Guid.CreateVersion7(),
                Title: "Observability Test Event",
                Date: "2026-08-01",
                Timezone: "Asia/Kolkata",
                Location: "Mumbai",
                Capacity: 120,
                Constraints: null,
                PriorEventsContext: null,
                HumanEditedMarkdown: null),
            conversationId: "pc_telemetry",
            workItemId: "work-observability",
            traceParent: activity.Id,
            correlationId: activity.TraceId.ToString(),
            ct: CancellationToken.None);

        await Assert.That(activity.GetTagItem("gen_ai.system")).IsEqualTo("foundry");
        await Assert.That(activity.GetTagItem("gen_ai.request.model")).IsEqualTo("planner-chat");
        await Assert.That(activity.GetTagItem("gen_ai.operation.name")).IsEqualTo("planner.schedule_draft");
        await Assert.That(activity.GetTagItem("mcp.server.identity")).IsEqualTo("planner-hosted");
        await Assert.That(activity.GetTagItem("mcp.tool.name")).IsEqualTo("planner.responses");
        await Assert.That(activity.GetTagItem("bethuya.agent.name")).IsEqualTo("planner-hosted");
    }

    private sealed class FakePlannerResponsesApi : IPlannerResponsesApi
    {
        public Task<PlannerResponsesApiResponse> CreateResponseAsync(
            PlannerResponsesApiRequest request,
            string? traceParent = null,
            string? correlationId = null,
            CancellationToken ct = default)
        {
            var agenda = new PlanningAgendaJson
            {
                AgendaVersion = "1.0",
                Event = new PlanningAgendaEvent
                {
                    EventId = request.Input.EventId.ToString(),
                    Title = request.Input.Title,
                    Date = request.Input.Date,
                    Timezone = request.Input.Timezone,
                    Location = request.Input.Location
                },
                Objectives = ["Objective 1"],
                Constraints = [],
                Agenda = new PlanningAgendaBody
                {
                    TotalDurationMinutes = 60,
                    Blocks =
                    [
                        new()
                        {
                            BlockId = "b1",
                            Start = "10:00",
                            End = "11:00",
                            Title = "Session",
                            Description = "Description",
                            Format = "talk",
                            Speakers = [new() { Name = "Speaker 1", Role = "speaker" }],
                            Tags = ["tag"]
                        }
                    ]
                },
                Rationale = new PlanningAgendaRationale
                {
                    KeyTradeoffs = ["tradeoff"],
                    InclusionNotes = ["note"]
                },
                Risks = new PlanningAgendaRisks
                {
                    Items = [new() { Risk = "risk", Mitigation = "mitigation" }]
                },
                NextActions = new PlanningAgendaNextActions
                {
                    Items = [new() { Owner = "human", Action = "Review" }]
                }
            };

            return Task.FromResult(new PlannerResponsesApiResponse(
                ResponseId: "resp_123",
                MarkdownAgenda: """
                    ## Timeline
                    | Start | End | Title |
                    | --- | --- | --- |
                    | 10:00 | 11:00 | Session |
                    """,
                AgendaJson: agenda,
                AgentName: "planner-hosted",
                AgentVersion: "v1"));
        }
    }
}
