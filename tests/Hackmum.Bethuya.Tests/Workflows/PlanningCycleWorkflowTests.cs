using System.Text.Json;
using System.Diagnostics;
using Hackmum.Bethuya.Backend.Agents;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hackmum.Bethuya.Tests.Workflows;

public sealed class PlanningCycleWorkflowTests
{
    [Test]
    public async Task PlanningCycleCreation_PersistsConversationId()
    {
        await using var db = CreateDbContext();
        var evt = await SeedEventAsync(db);
        var service = new PlanningCycleService(db, new FakeAgentInvoker());

        var cycle = await service.StartCycleAsync(evt.Id, new StartPlanningCycleRequest());

        await Assert.That(cycle.ConversationId).IsNotNull();
        await Assert.That(cycle.ConversationId).StartsWith("pc_");
    }

    [Test]
    public async Task TwoCyclesForSameEvent_UseDifferentConversationIds()
    {
        await using var db = CreateDbContext();
        var evt = await SeedEventAsync(db);
        var service = new PlanningCycleService(db, new FakeAgentInvoker());

        var first = await service.StartCycleAsync(evt.Id, new StartPlanningCycleRequest());
        first = await service.GenerateDraftAsync(first.CycleId, new GeneratePlannerDraftRequest(WorkItemId: "work-1"));
        _ = await service.ApproveDraftAsync(first.CycleId, new ApprovePlannerDraftRequest(first.ActiveDraft!.DraftId, first.ActiveDraft.MarkdownAgenda, "tester"));
        _ = await service.PublishAsync(first.CycleId, new PublishPlanningCycleRequest(first.ActiveDraft.DraftId, "tester"));

        var second = await service.StartCycleAsync(evt.Id, new StartPlanningCycleRequest(ForceNewCycle: true));

        await Assert.That(first.ConversationId).IsNotEqualTo(second.ConversationId);
    }

    [Test]
    public async Task PlannerInvocation_ProducesHybridConsistentOutput()
    {
        await using var db = CreateDbContext();
        var evt = await SeedEventAsync(db);
        var service = new PlanningCycleService(db, new FakeAgentInvoker());

        var cycle = await service.StartCycleAsync(evt.Id, new StartPlanningCycleRequest());
        cycle = await service.GenerateDraftAsync(cycle.CycleId, new GeneratePlannerDraftRequest(WorkItemId: "work-hybrid"));

        await Assert.That(cycle.ActiveDraft).IsNotNull();
        await Assert.That(string.IsNullOrWhiteSpace(cycle.ActiveDraft!.MarkdownAgenda)).IsFalse();
        await Assert.That(cycle.ActiveDraft.AgendaJson.Event.Title).IsEqualTo(evt.Title);
        await Assert.That(cycle.ActiveDraft.AgendaJson.Event.Date).IsEqualTo(evt.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        await Assert.That(cycle.ActiveDraft.AgendaJson.Event.Timezone).IsEqualTo("Asia/Kolkata");
        await Assert.That(cycle.ActiveDraft.AgendaJson.Agenda.Blocks.Count).IsEqualTo(3);
        await Assert.That(cycle.ActiveDraft.AgendaJson.Agenda.Blocks[0].Title).IsEqualTo("Welcome");
        await Assert.That(cycle.ActiveDraft.MarkdownAgenda).Contains("| 09:30 | 10:00 |");
        await Assert.That(cycle.ActiveDraft.MarkdownAgenda).Contains("Welcome");
    }

    [Test]
    public async Task PublishingFinalSchedule_ClosesCycleAndBlocksFurtherPlannerInvocations()
    {
        await using var db = CreateDbContext();
        var evt = await SeedEventAsync(db);
        var service = new PlanningCycleService(db, new FakeAgentInvoker());

        var cycle = await service.StartCycleAsync(evt.Id, new StartPlanningCycleRequest());
        cycle = await service.GenerateDraftAsync(cycle.CycleId, new GeneratePlannerDraftRequest(WorkItemId: "work-publish"));
        cycle = await service.ApproveDraftAsync(cycle.CycleId, new ApprovePlannerDraftRequest(cycle.ActiveDraft!.DraftId, cycle.ActiveDraft.MarkdownAgenda, "tester"));
        _ = await service.PublishAsync(cycle.CycleId, new PublishPlanningCycleRequest(cycle.ActiveDraft!.DraftId, "tester"));

        var refreshed = await service.GetActiveCycleAsync(evt.Id);
        await Assert.That(refreshed!.Status).IsEqualTo(PlanningCycleStatus.Published);

        Assert.Throws<InvalidOperationException>(() =>
            service.GenerateDraftAsync(cycle.CycleId, new GeneratePlannerDraftRequest(WorkItemId: "work-after-publish"))
                .GetAwaiter()
                .GetResult());
    }

    [Test]
    public async Task PostPublishChange_CreatesNewCycleWithNewConversationId()
    {
        await using var db = CreateDbContext();
        var evt = await SeedEventAsync(db);
        var service = new PlanningCycleService(db, new FakeAgentInvoker());

        var cycle = await service.StartCycleAsync(evt.Id, new StartPlanningCycleRequest());
        cycle = await service.GenerateDraftAsync(cycle.CycleId, new GeneratePlannerDraftRequest(WorkItemId: "work-before-new"));
        cycle = await service.ApproveDraftAsync(cycle.CycleId, new ApprovePlannerDraftRequest(cycle.ActiveDraft!.DraftId, cycle.ActiveDraft.MarkdownAgenda, "tester"));
        _ = await service.PublishAsync(cycle.CycleId, new PublishPlanningCycleRequest(cycle.ActiveDraft!.DraftId, "tester"));

        var newCycle = await service.StartCycleAsync(evt.Id, new StartPlanningCycleRequest(ForceNewCycle: true));
        await Assert.That(newCycle.CycleId).IsNotEqualTo(cycle.CycleId);
        await Assert.That(newCycle.ConversationId).IsNotEqualTo(cycle.ConversationId);
    }

    [Test]
    public async Task GenerateDraftAsync_MissingTraceParent_PersistsBoundedAuditPlaceholder()
    {
        await using var db = CreateDbContext();
        var evt = await SeedEventAsync(db);
        var service = new PlanningCycleService(db, new FakeAgentInvoker());

        var cycle = await service.StartCycleAsync(evt.Id, new StartPlanningCycleRequest());

        var previousActivity = Activity.Current;
        Activity.Current = null;

        try
        {
            _ = await service.GenerateDraftAsync(cycle.CycleId, new GeneratePlannerDraftRequest(WorkItemId: "work-missing-trace"));
        }
        finally
        {
            Activity.Current = previousActivity;
        }

        var audit = await db.PlannerInvocationAudits.SingleAsync(a => a.WorkItemId == "work-missing-trace");
        await Assert.That(audit.TraceParent).StartsWith("missing-traceparent:");
        await Assert.That(audit.TraceParent.Length).IsLessThanOrEqualTo(200);
        await Assert.That(audit.CorrelationId).IsNotNull();
        await Assert.That(audit.CorrelationId!.Length).IsLessThanOrEqualTo(200);
    }

    [Test]
    public async Task GenerateDraftAsync_LongTraceParent_TruncatesPersistedTraceMetadata()
    {
        await using var db = CreateDbContext();
        var evt = await SeedEventAsync(db);
        var service = new PlanningCycleService(db, new FakeAgentInvoker());

        var cycle = await service.StartCycleAsync(evt.Id, new StartPlanningCycleRequest());

        using var activity = new Activity("planning-cycle-test");
        activity.SetIdFormat(ActivityIdFormat.Hierarchical);
        activity.SetParentId(new string('p', 240));
        activity.Start();

        _ = await service.GenerateDraftAsync(cycle.CycleId, new GeneratePlannerDraftRequest(WorkItemId: "work-long-trace"));

        var draft = await db.PlannerDrafts.SingleAsync(d => d.WorkItemId == "work-long-trace");
        var audit = await db.PlannerInvocationAudits.SingleAsync(a => a.WorkItemId == "work-long-trace");

        await Assert.That(draft.TraceParent).IsNotNull();
        await Assert.That(draft.TraceParent!.Length).IsLessThanOrEqualTo(200);
        await Assert.That(audit.TraceParent.Length).IsLessThanOrEqualTo(200);
    }

    [Test]
    public async Task GenerateDraftAsync_LongInvokerMetadata_TruncatesPersistedProviderFields()
    {
        await using var db = CreateDbContext();
        var evt = await SeedEventAsync(db);
        var service = new PlanningCycleService(
            db,
            new FakeAgentInvoker(
                responseId: new string('r', 260),
                agentName: new string('n', 260),
                agentVersionTag: new string('v', 260)));

        var cycle = await service.StartCycleAsync(evt.Id, new StartPlanningCycleRequest());

        _ = await service.GenerateDraftAsync(cycle.CycleId, new GeneratePlannerDraftRequest(WorkItemId: "work-long-provider-metadata"));

        var draft = await db.PlannerDrafts.SingleAsync(d => d.WorkItemId == "work-long-provider-metadata");
        var audit = await db.PlannerInvocationAudits.SingleAsync(a => a.WorkItemId == "work-long-provider-metadata");

        await Assert.That(draft.ResponseId).IsNotNull();
        await Assert.That(draft.ResponseId!.Length).IsLessThanOrEqualTo(200);
        await Assert.That(draft.AgentName).IsNotNull();
        await Assert.That(draft.AgentName!.Length).IsLessThanOrEqualTo(200);
        await Assert.That(draft.AgentVersionTag).IsNotNull();
        await Assert.That(draft.AgentVersionTag!.Length).IsLessThanOrEqualTo(200);
        await Assert.That(audit.ResponseId.Length).IsLessThanOrEqualTo(200);
        await Assert.That(audit.AgentName).IsNotNull();
        await Assert.That(audit.AgentName!.Length).IsLessThanOrEqualTo(200);
        await Assert.That(audit.AgentVersionTag.Length).IsLessThanOrEqualTo(200);
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"planning-cycle-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new BethuyaDbContext(options);
    }

    private static async Task<Event> SeedEventAsync(BethuyaDbContext db)
    {
        var evt = new Event
        {
            Title = "Planner Workflow Test Event",
            Description = "Test fixture event",
            Type = EventType.Meetup,
            Capacity = 120,
            StartDate = new DateTimeOffset(2026, 6, 30, 9, 30, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 6, 30, 13, 0, 0, TimeSpan.Zero),
            Location = "Mumbai",
            CreatedBy = "tester"
        };

        db.Events.Add(evt);
        await db.SaveChangesAsync();
        return evt;
    }

    private sealed class FakeAgentInvoker(
        string? responseId = null,
        string? agentName = null,
        string? agentVersionTag = null) : IAgentInvoker
    {
        public Task<PlannerInvocationResult> InvokePlannerAsync(
            PlannerInvocationInput input,
            string conversationId,
            string workItemId,
            string? traceParent,
            string? correlationId,
            CancellationToken ct = default)
        {
            var agenda = new PlanningAgendaJson
            {
                AgendaVersion = "1.0",
                Event = new PlanningAgendaEvent
                {
                    EventId = input.EventId.ToString(),
                    Title = input.Title,
                    Date = input.Date,
                    Timezone = input.Timezone,
                    Location = input.Location
                },
                Objectives = ["Objective 1"],
                Constraints = input.Constraints is null ? [] : [input.Constraints],
                Agenda = new PlanningAgendaBody
                {
                    TotalDurationMinutes = 180,
                    Blocks =
                    [
                        new()
                        {
                            BlockId = "b1",
                            Start = "09:30",
                            End = "10:00",
                            Title = "Welcome",
                            Description = "Kickoff",
                            Format = "other",
                            Speakers = [new() { Name = "Host", Role = "host" }],
                            Tags = ["opening"]
                        },
                        new()
                        {
                            BlockId = "b2",
                            Start = "10:00",
                            End = "11:00",
                            Title = "Keynote",
                            Description = "Theme keynote",
                            Format = "talk",
                            Speakers = [new() { Name = "Speaker 1", Role = "speaker" }],
                            Tags = ["keynote"]
                        },
                        new()
                        {
                            BlockId = "b3",
                            Start = "11:00",
                            End = "12:30",
                            Title = "Workshop",
                            Description = "Hands-on",
                            Format = "workshop",
                            Speakers = [new() { Name = "Facilitator", Role = "facilitator" }],
                            Tags = ["workshop"]
                        }
                    ]
                },
                Rationale = new PlanningAgendaRationale
                {
                    KeyTradeoffs = ["Depth vs breadth"],
                    InclusionNotes = ["No sensitive trait inference"]
                },
                Risks = new PlanningAgendaRisks
                {
                    Items = [new() { Risk = "Overrun", Mitigation = "Timeboxes" }]
                },
                NextActions = new PlanningAgendaNextActions
                {
                    Items = [new() { Owner = "human", Action = "Review draft" }]
                }
            };

            var markdown = """
                # Planner Workflow Test Event — 2026-06-30 (Asia/Kolkata)

                ## Timeline
                | Start | End | Title |
                | --- | --- | --- |
                | 09:30 | 10:00 | Welcome |
                | 10:00 | 11:00 | Keynote |
                | 11:00 | 12:30 | Workshop |
                """;

            return Task.FromResult(new PlannerInvocationResult(
                MarkdownAgenda: markdown,
                AgendaJson: agenda,
                ResponseId: responseId ?? $"resp-{workItemId}",
                AgentName: agentName ?? "planner-hosted",
                AgentVersionTag: agentVersionTag ?? "test-v1"));
        }
    }
}

