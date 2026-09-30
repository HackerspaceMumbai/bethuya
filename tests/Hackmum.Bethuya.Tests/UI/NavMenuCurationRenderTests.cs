using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Shared.Layout;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

public class NavMenuCurationRenderTests
{
    private static readonly Guid EventId = Guid.Parse("01a0ec0b-abc8-7678-86fb-62d826018cd6");

    [Test]
    public async Task ImportsRouteWithEventId_OversubscribedEvent_ShowsCurationLinkForCurator()
    {
        var curationApi = CreateApi(capacity: 100, registrations: 106);
        using var ctx = CreateContext(curationApi, BethuyaRoles.Curator);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo($"imports?eventId={EventId}");

        var cut = ctx.RenderComponent<NavMenu>();
        cut.WaitForState(() => cut.FindAll("[data-test='nav-curation-link']").Count == 1, TimeSpan.FromSeconds(5));

        await Assert.That(cut.Find("[data-test='nav-curation-link']").GetAttribute("href")).IsEqualTo($"curation/{EventId}");
        await curationApi.Received(1).GetAvailabilityAsync(EventId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EventRoute_WithinCapacity_HidesCurationLinkAndExplainsWhy()
    {
        var curationApi = CreateApi(capacity: 100, registrations: 40);
        using var ctx = CreateContext(curationApi, BethuyaRoles.Admin);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo($"events/{EventId}/registrations");

        var cut = ctx.RenderComponent<NavMenu>();
        cut.WaitForState(() => cut.FindAll("[data-test='nav-curation-not-needed']").Count == 1, TimeSpan.FromSeconds(5));

        await Assert.That(cut.FindAll("[data-test='nav-curation-link']")).IsEmpty();
        await Assert.That(cut.Find("[data-test='nav-curation-not-needed']").TextContent).Contains("40 of 100");
    }

    [Test]
    public async Task EventRoute_AvailabilityCallFails_KeepsCurationLinkVisible()
    {
        var curationApi = Substitute.For<ICurationApi>();
        curationApi.GetAvailabilityAsync(EventId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("backend down"));
        using var ctx = CreateContext(curationApi, BethuyaRoles.Curator);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo($"events/{EventId}");

        var cut = ctx.RenderComponent<NavMenu>();
        cut.WaitForState(() => cut.FindAll("[data-test='nav-curation-link']").Count == 1, TimeSpan.FromSeconds(5));

        await Assert.That(cut.FindAll("[data-test='nav-curation-not-needed']")).IsEmpty();
    }

    [Test]
    public async Task EventRoute_OrganizerWithoutCuratorRole_DoesNotCheckAvailability()
    {
        var curationApi = CreateApi(capacity: 100, registrations: 106);
        using var ctx = CreateContext(curationApi, BethuyaRoles.Organizer);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo($"imports?eventId={EventId}");

        var cut = ctx.RenderComponent<NavMenu>();

        await Assert.That(cut.FindAll("[data-test='nav-curation-link']")).IsEmpty();
        await Assert.That(cut.Find("[data-test='nav-imports-link']").GetAttribute("href")).IsEqualTo($"imports?eventId={EventId}");
        await curationApi.DidNotReceive().GetAvailabilityAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static ICurationApi CreateApi(int capacity, int registrations)
    {
        var api = Substitute.For<ICurationApi>();
        api.GetAvailabilityAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CurationAvailabilityDto(EventId, capacity, registrations, registrations > capacity)));
        return api;
    }

    private static BunitCtx CreateContext(ICurationApi curationApi, params string[] roles)
    {
        var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddSingleton(curationApi);
        ctx.AddTestAuthorization().SetAuthorized("Dev User").SetRoles(roles);
        return ctx;
    }
}
