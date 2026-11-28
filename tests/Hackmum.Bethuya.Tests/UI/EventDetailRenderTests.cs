using System.Net;
using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Refit;

using BunitCtx = Bunit.BunitContext;

namespace Hackmum.Bethuya.Tests.UI;

public class EventDetailRenderTests
{
    [Test]
    public async Task Reload_DeniedPlanningCycle_ClearsDraftFromEarlierLoad()
    {
        await using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddAuthorization();
        ctx.Services.AddBlazorBlueprintComponents();

        var eventId = Guid.CreateVersion7();
        var eventApi = Substitute.For<IEventApi>();
        eventApi.GetByIdAsync(eventId, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("event read not needed"));
        var planningCycleApi = Substitute.For<IPlanningCycleApi>();
        planningCycleApi.GetActiveCycleAsync(eventId, Arg.Any<CancellationToken>()).Returns(CreateCycleWithDraft(eventId));
        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(planningCycleApi);

        var cut = ctx.Render<EventDetail>(parameters => parameters.Add(page => page.EventId, eventId));
        cut.WaitForState(() => cut.FindAll("[data-test='session-card']").Count == 1, TimeSpan.FromSeconds(5));
        await Assert.That(cut.Markup).Contains("status-badge-proposed-by-agent");

        var denied = await ApiException.Create(
            new HttpRequestMessage(HttpMethod.Get, $"https://backend/api/planning-cycles/events/{eventId}/active"),
            HttpMethod.Get,
            new HttpResponseMessage(HttpStatusCode.Forbidden),
            new RefitSettings());
        planningCycleApi.GetActiveCycleAsync(eventId, Arg.Any<CancellationToken>()).ThrowsAsync(denied);

        cut.Render(parameters => parameters.Add(page => page.EventId, eventId));
        cut.WaitForState(() => cut.FindAll("[data-test='session-card']").Count == 0, TimeSpan.FromSeconds(5));

        await Assert.That(cut.Markup).DoesNotContain("status-badge-proposed-by-agent");
        await Assert.That(cut.Markup).DoesNotContain("Opening keynote");
    }

    private static PlanningCycleDto CreateCycleWithDraft(Guid eventId) => new(
        Guid.CreateVersion7(),
        eventId,
        "pc_test",
        "Drafting",
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        null,
        new PlannerDraftDto(
            Guid.CreateVersion7(),
            "work-1",
            "| 10:00 | 11:00 | Opening keynote |",
            new PlanningAgendaJsonDto
            {
                AgendaVersion = "1.0",
                Event = new PlanningAgendaEventDto
                {
                    EventId = eventId.ToString(),
                    Title = "Planner Event",
                    Date = "2026-12-05",
                    Timezone = "Asia/Kolkata"
                },
                Agenda = new PlanningAgendaBodyDto
                {
                    TotalDurationMinutes = 60,
                    Blocks =
                    [
                        new PlanningAgendaBlockDto
                        {
                            BlockId = "b1",
                            Start = "10:00",
                            End = "11:00",
                            Title = "Opening keynote",
                            Description = "Welcome talk",
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
            "test-v1"));
}
