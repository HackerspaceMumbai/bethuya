using System.Net;
using System.Reflection;
using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Refit;

using BunitCtx = Bunit.BunitContext;

namespace Hackmum.Bethuya.Tests.UI;

public class EventDetailRenderTests
{
    private static readonly Guid EventId = Guid.NewGuid();

    [Test]
    public async Task DraftWithAi_CallsPlannerWithoutArtificialDelay()
    {
        await using var ctx = CreateContext(out var planningApi, activeCycle: null);
        var startSource = new TaskCompletionSource<PlanningCycleDto>();
        var cycle = BuildCycle("Draft", withDraft: false);
        planningApi.StartCycleAsync(EventId, Arg.Any<StartPlanningCycleDto>(), Arg.Any<CancellationToken>())
            .Returns(_ => startSource.Task);
        planningApi.GenerateDraftAsync(cycle.CycleId, Arg.Any<GeneratePlannerDraftDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cycle with { ActiveDraft = BuildDraft() }));

        var cut = RenderEventDetail(ctx);
        var draft = await StartOnRendererAsync(cut, "DraftWithAiAsync", null, null);

        // The planner request starts in the synchronous part of the handler, not after a fixed delay.
        await planningApi.Received(1).StartCycleAsync(EventId, Arg.Any<StartPlanningCycleDto>(), Arg.Any<CancellationToken>());

        startSource.SetResult(cycle);
        await draft;
    }

    [Test]
    public async Task PromptChip_SecondClickWhileDrafting_IsIgnored()
    {
        await using var ctx = CreateContext(out var planningApi, activeCycle: null);
        var startSource = new TaskCompletionSource<PlanningCycleDto>();
        var cycle = BuildCycle("Draft", withDraft: false);
        planningApi.StartCycleAsync(EventId, Arg.Any<StartPlanningCycleDto>(), Arg.Any<CancellationToken>())
            .Returns(_ => startSource.Task);
        planningApi.GenerateDraftAsync(cycle.CycleId, Arg.Any<GeneratePlannerDraftDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cycle with { ActiveDraft = BuildDraft() }));

        var cut = RenderEventDetail(ctx);
        var first = await StartOnRendererAsync(cut, "ChipShortenEventAsync");
        var second = await StartOnRendererAsync(cut, "ChipShortenEventAsync");
        startSource.SetResult(cycle);
        await Task.WhenAll(first, second);

        await planningApi.Received(1).StartCycleAsync(EventId, Arg.Any<StartPlanningCycleDto>(), Arg.Any<CancellationToken>());
        await planningApi.Received(1).GenerateDraftAsync(cycle.CycleId, Arg.Any<GeneratePlannerDraftDto>(), Arg.Any<CancellationToken>());
        var thread = cut.Find("[data-test='agent-console-thread']").TextContent;
        await Assert.That(thread.Split("Refine schedule: Shorten event").Length - 1).IsEqualTo(1);
    }

    [Test]
    public async Task Approve_SecondClickWhileApproving_IsIgnored()
    {
        var cycle = BuildCycle("Draft", withDraft: true);
        await using var ctx = CreateContext(out var planningApi, cycle);
        var approveSource = new TaskCompletionSource<PlanningCycleDto>();
        planningApi.ApproveDraftAsync(cycle.CycleId, Arg.Any<ApprovePlannerDraftDto>(), Arg.Any<CancellationToken>())
            .Returns(_ => approveSource.Task);

        var cut = RenderEventDetail(ctx);
        var first = await StartOnRendererAsync(cut, "ApproveScheduleAsync");
        var second = await StartOnRendererAsync(cut, "ApproveScheduleAsync");
        approveSource.SetResult(cycle with { Status = "ReadyToPublish" });
        await Task.WhenAll(first, second);

        await planningApi.Received(1).ApproveDraftAsync(cycle.CycleId, Arg.Any<ApprovePlannerDraftDto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Publish_SecondClickWhilePublishing_IsIgnored()
    {
        var cycle = BuildCycle("ReadyToPublish", withDraft: true);
        await using var ctx = CreateContext(out var planningApi, cycle);
        var publishSource = new TaskCompletionSource<PublishedScheduleDto>();
        planningApi.PublishAsync(cycle.CycleId, Arg.Any<PublishPlanningCycleDto>(), Arg.Any<CancellationToken>())
            .Returns(_ => publishSource.Task);

        var cut = RenderEventDetail(ctx);
        var first = await StartOnRendererAsync(cut, "PublishScheduleAsync");
        var second = await StartOnRendererAsync(cut, "PublishScheduleAsync");
        var draft = cycle.ActiveDraft!;
        publishSource.SetResult(new PublishedScheduleDto(
            Guid.NewGuid(), EventId, cycle.CycleId, draft.DraftId, draft.MarkdownAgenda, draft.AgendaJson, DateTimeOffset.UtcNow, "organizer"));
        await Task.WhenAll(first, second);

        await planningApi.Received(1).PublishAsync(cycle.CycleId, Arg.Any<PublishPlanningCycleDto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StartNewCycle_SecondClickWhileStarting_IsIgnored()
    {
        var cycle = BuildCycle("Published", withDraft: true);
        await using var ctx = CreateContext(out var planningApi, cycle);
        var startSource = new TaskCompletionSource<PlanningCycleDto>();
        planningApi.StartCycleAsync(EventId, Arg.Any<StartPlanningCycleDto>(), Arg.Any<CancellationToken>())
            .Returns(_ => startSource.Task);

        var cut = RenderEventDetail(ctx);
        var first = await StartOnRendererAsync(cut, "StartNewCycleAsync");
        var second = await StartOnRendererAsync(cut, "StartNewCycleAsync");
        startSource.SetResult(BuildCycle("Draft", withDraft: false));
        await Task.WhenAll(first, second);

        await planningApi.Received(1).StartCycleAsync(EventId, Arg.Any<StartPlanningCycleDto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SessionActions_HaveAccessibleNames()
    {
        await using var ctx = CreateContext(out _, BuildCycle("Draft", withDraft: true));

        var cut = RenderEventDetail(ctx);
        cut.WaitForElement("[data-test='session-card']");

        var labels = cut.FindAll(".session-actions button").Select(button => button.GetAttribute("aria-label")).ToList();
        await Assert.That(labels).Contains("Edit Community check-in");
        await Assert.That(labels).Contains("Move Community check-in earlier");
        await Assert.That(labels).Contains("Move Community check-in later");
        await Assert.That(labels).Contains("Delete Community check-in");
    }

    private static BunitCtx CreateContext(out IPlanningCycleApi planningApi, PlanningCycleDto? activeCycle)
    {
        var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddAuthorization().SetAuthorized("Organizer");

        planningApi = Substitute.For<IPlanningCycleApi>();
        if (activeCycle is null)
        {
            planningApi.GetActiveCycleAsync(EventId, Arg.Any<CancellationToken>())
                .ThrowsAsync(CreateNotFound());
        }
        else
        {
            planningApi.GetActiveCycleAsync(EventId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(activeCycle));
        }

        ctx.Services.AddSingleton(Substitute.For<IEventApi>());
        ctx.Services.AddSingleton(planningApi);
        return ctx;
    }

    private static IRenderedComponent<EventDetail> RenderEventDetail(BunitCtx ctx) =>
        ctx.Render<EventDetail>(parameters => parameters.Add(page => page.EventId, EventId));

    // Starts a handler on the renderer the way a dispatched click does and returns the
    // still-running task, so a second click can arrive before the first one finishes.
    private static async Task<Task> StartOnRendererAsync(IRenderedComponent<EventDetail> cut, string methodName, params object?[]? args)
    {
        var method = typeof(EventDetail).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        Task? pending = null;
        await cut.InvokeAsync(() => { pending = (Task)method.Invoke(cut.Instance, args is { Length: > 0 } ? args : null)!; });
        return pending!;
    }

    private static ApiException CreateNotFound() =>
        ApiException.Create(
            new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/planning-cycles"),
            HttpMethod.Get,
            new HttpResponseMessage(HttpStatusCode.NotFound),
            new RefitSettings()).GetAwaiter().GetResult();

    private static PlanningCycleDto BuildCycle(string status, bool withDraft) => new(
        Guid.NewGuid(),
        EventId,
        "pc_test",
        status,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        status == "Published" ? DateTimeOffset.UtcNow : null,
        withDraft ? BuildDraft() : null);

    private static PlannerDraftDto BuildDraft() => new(
        Guid.NewGuid(),
        "draft-test",
        "## Agenda\n- 09:30 Community check-in",
        new PlanningAgendaJsonDto
        {
            AgendaVersion = "1.0",
            Event = new PlanningAgendaEventDto
            {
                EventId = EventId.ToString(),
                Title = "Copilot Dev Days",
                Date = "2026-12-12",
                Timezone = "Asia/Kolkata"
            },
            Agenda = new PlanningAgendaBodyDto
            {
                TotalDurationMinutes = 60,
                Blocks =
                [
                    new PlanningAgendaBlockDto
                    {
                        BlockId = "blk-01",
                        Start = "09:30",
                        End = "10:00",
                        Title = "Community check-in",
                        Description = "Opening context.",
                        Format = "other"
                    },
                    new PlanningAgendaBlockDto
                    {
                        BlockId = "blk-02",
                        Start = "10:00",
                        End = "10:30",
                        Title = "Lightning talks",
                        Description = "Short talks.",
                        Format = "talk"
                    }
                ]
            },
            Rationale = new PlanningAgendaRationaleDto(),
            Risks = new PlanningAgendaRisksDto(),
            NextActions = new PlanningAgendaNextActionsDto()
        },
        false,
        null,
        null,
        DateTimeOffset.UtcNow,
        "v1");
}
