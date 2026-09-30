using Bethuya.Hybrid.Shared.Models.CommandCenter;
using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

public sealed class CommunityCommandCenterTests
{
    private static readonly TimeProvider EventPressureClock =
        new FixedTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task DeterministicProvider_EventPressure_SelectsExplainableEventMode()
    {
        var service = new DeterministicCommunityCommandCenterService(EventPressureClock);

        var result = await service.GetAsync(CommunityRole.EventOrganizer);

        await Assert.That(result.RecommendedMode).IsEqualTo(CommandCenterMode.Event);
        await Assert.That(result.ModeReason).Contains("16 days away");
        await Assert.That(result.AttentionItems[0].Category).IsEqualTo("Event operations");
        await Assert.That(result.UpcomingEvents[0].DateLabel).IsEqualTo("17 Oct");
    }

    [Test]
    public async Task DeterministicProvider_DistantEvent_SelectsStrategicMode()
    {
        var service = new DeterministicCommunityCommandCenterService(
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero)));

        var result = await service.GetAsync(CommunityRole.EventOrganizer);

        await Assert.That(result.RecommendedMode).IsEqualTo(CommandCenterMode.Strategic);
        await Assert.That(result.ModeReason).Contains("strategic community priorities");
    }

    [Test]
    public async Task DeterministicProvider_StrategicOverride_PreservesCriticalWarnings()
    {
        var service = new DeterministicCommunityCommandCenterService(EventPressureClock);

        var result = await service.GetAsync(CommunityRole.CommunityAdministrator, CommandCenterMode.Strategic);

        await Assert.That(result.EffectiveMode).IsEqualTo(CommandCenterMode.Strategic);
        await Assert.That(result.AttentionItems.Any(item => item.Severity == AttentionSeverity.Critical)).IsTrue();
    }

    [Test]
    [Arguments(CommunityRole.CommunityAdministrator, "Community health")]
    [Arguments(CommunityRole.EventOrganizer, "Event operations")]
    [Arguments(CommunityRole.VolunteerLead, "Volunteer network")]
    [Arguments(CommunityRole.MentorshipLead, "Mentorship")]
    public async Task DeterministicProvider_PrioritizesSelectedRole(CommunityRole role, string expectedCategory)
    {
        var service = new DeterministicCommunityCommandCenterService(EventPressureClock);

        var result = await service.GetAsync(role, CommandCenterMode.Strategic);

        await Assert.That(result.AttentionItems[0].Category).IsEqualTo(expectedCategory);
    }

    [Test]
    public async Task Home_RendersCommandCenterAndSupportsRoleSelection()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddSingleton(EventPressureClock);
        ctx.Services.AddSingleton<ICommunityCommandCenterService, DeterministicCommunityCommandCenterService>();

        var cut = ctx.RenderComponent<Home>();

        cut.WaitForAssertion(() =>
        {
            if (!cut.Markup.Contains("community-command-center", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Command center did not render.");
            }
        });

        await Assert.That(cut.Find("[data-test='role-community-administrator'] button").GetAttribute("aria-pressed"))
            .IsEqualTo("true");
        await cut.Find("[data-test='role-volunteer-lead'] button").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            if (!cut.Markup.Contains("Volunteer network", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Volunteer priorities did not render.");
            }
        });
        await Assert.That(cut.Find("[data-test='role-volunteer-lead'] button").GetAttribute("aria-pressed"))
            .IsEqualTo("true");
    }

    [Test]
    public async Task WorkspacePreview_RendersMeaningfulDestination()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
            .NavigateTo("/community-graph");

        var cut = ctx.RenderComponent<WorkspacePreview>();

        await Assert.That(cut.Markup).Contains("Community Graph");
        await Assert.That(cut.Markup).Contains("What is available now");
        await Assert.That(cut.Markup).Contains("What comes next");
    }

    [Test]
    public async Task WorkspacePreview_IgnoresQueryAndFragmentWhenResolvingRoute()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
            .NavigateTo("/Volunteers?ref=home#coverage");

        var cut = ctx.RenderComponent<WorkspacePreview>();

        await Assert.That(cut.Markup).Contains("Volunteers");
        await Assert.That(cut.Markup).Contains("Contribution network");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
