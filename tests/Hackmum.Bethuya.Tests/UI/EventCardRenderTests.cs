using Bethuya.Hybrid.Shared.Components.Dashboard;
using BlazorBlueprint.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

public class EventCardRenderTests
{
    [Test]
    public async Task EventCard_WithRegistrationCounts_ShowsLifecycleMilestones()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<EventCard>(p => p.Add(c => c.Event, new EventViewModel
        {
            Id = Guid.CreateVersion7(),
            Title = "Dev Days Mumbai",
            Status = "RegistrationOpen",
            Registrations = new RegistrationCounts(Registered: 106, Pending: 0, Approved: 106, CheckedIn: 49)
        }));

        await Assert.That(cut.Find("[data-test='count-registered']").TextContent).IsEqualTo("106 registered");
        await Assert.That(cut.Find("[data-test='count-pending']").TextContent).IsEqualTo("0 pending");
        await Assert.That(cut.Find("[data-test='count-approved']").TextContent).IsEqualTo("106 approved");
        await Assert.That(cut.Find("[data-test='count-checked-in']").TextContent).IsEqualTo("49 checked in");
    }

    [Test]
    public async Task EventCard_WithoutRegistrationCounts_HidesCounts()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<EventCard>(p => p.Add(c => c.Event, new EventViewModel
        {
            Id = Guid.CreateVersion7(),
            Title = "Public meetup",
            Status = "RegistrationOpen"
        }));

        await Assert.That(cut.FindAll("[data-test='registration-counts']")).IsEmpty();
    }
}
