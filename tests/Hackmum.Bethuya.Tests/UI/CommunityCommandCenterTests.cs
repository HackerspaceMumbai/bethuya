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
    public async Task DeterministicProvider_ModeChangesOperationalIntelligence()
    {
        var service = new DeterministicCommunityCommandCenterService(EventPressureClock);

        var strategic = await service.GetAsync(
            CommunityRole.EventOrganizer,
            CommandCenterMode.Strategic);
        var eventOperations = await service.GetAsync(
            CommunityRole.EventOrganizer,
            CommandCenterMode.Event);

        await Assert.That(strategic.Snapshot[0].Label).IsEqualTo("Community health");
        await Assert.That(strategic.Insight.Headline).Contains("Community");
        await Assert.That(strategic.Momentum[0].JourneyTo).IsEqualTo("Volunteer");

        await Assert.That(eventOperations.Snapshot[0].Label).IsEqualTo("Event readiness");
        await Assert.That(eventOperations.Insight.Headline).Contains("execution");
        await Assert.That(eventOperations.Momentum[0].JourneyTo).IsEqualTo("Event lead");
        await Assert.That(eventOperations.Reviews[0].Label).IsEqualTo("Waitlist decisions");
        await Assert.That(eventOperations.UpcomingEvents[0].ReadinessLabel).IsEqualTo("82% ready");
        await Assert.That(eventOperations.UpcomingEvents[0].CapacityLabel).IsEqualTo("150 / 160");
        await Assert.That(eventOperations.UpcomingEvents[0].WaitlistLabel).IsEqualTo("18 waiting");
        await Assert.That(eventOperations.UpcomingEvents[0].VolunteerCoverageLabel).IsEqualTo("12 / 14");
        await Assert.That(eventOperations.UpcomingEvents[0].RiskLabel).IsEqualTo("Volunteer gap");
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
    public async Task Home_ModeSwitchReordersPrimaryWork()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddSingleton(EventPressureClock);
        ctx.Services.AddSingleton<ICommunityCommandCenterService, DeterministicCommunityCommandCenterService>();

        var cut = ctx.RenderComponent<Home>();
        cut.WaitForElement("[data-test='mode-layout-event']");

        AssertAppearsBefore(cut.Markup, "attention-queue", "pending-event-approvals");
        AssertAppearsBefore(cut.Markup, "pending-event-approvals", "upcoming-events");
        AssertAppearsBefore(cut.Markup, "upcoming-events", "weekly-insight");
        await Assert.That(cut.Find("[data-test='upcoming-events']").GetAttribute("data-expanded"))
            .IsEqualTo("true");
        await Assert.That(cut.FindAll("[data-test='pending-approvals']").Count).IsEqualTo(0);

        await cut.Find("[data-test='mode-strategic'] button").ClickAsync(new());
        cut.WaitForElement("[data-test='mode-layout-strategic']");

        AssertAppearsBefore(cut.Markup, "weekly-insight", "people-momentum");
        AssertAppearsBefore(cut.Markup, "people-momentum", "attention-queue");
        await Assert.That(cut.Find("[data-test='upcoming-events']").GetAttribute("data-expanded"))
            .IsEqualTo("false");
        await Assert.That(cut.FindAll("[data-test='pending-approvals']").Count).IsEqualTo(1);
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

    private static void AssertAppearsBefore(string markup, string firstDataTest, string secondDataTest)
    {
        var firstIndex = markup.IndexOf($"data-test=\"{firstDataTest}\"", StringComparison.Ordinal);
        var secondIndex = markup.IndexOf($"data-test=\"{secondDataTest}\"", StringComparison.Ordinal);

        if (firstIndex < 0 || secondIndex < 0 || firstIndex >= secondIndex)
        {
            throw new InvalidOperationException(
                $"Expected '{firstDataTest}' to render before '{secondDataTest}'.");
        }
    }
}
