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
    [Test]
    public async Task DeterministicProvider_EventPressure_SelectsExplainableEventMode()
    {
        var service = new DeterministicCommunityCommandCenterService();

        var result = await service.GetAsync(CommunityRole.EventOrganizer);

        await Assert.That(result.RecommendedMode).IsEqualTo(CommandCenterMode.Event);
        await Assert.That(result.ModeReason).Contains("Hacktoberfest Mumbai");
        await Assert.That(result.AttentionItems[0].Category).IsEqualTo("Event operations");
    }

    [Test]
    public async Task DeterministicProvider_StrategicOverride_PreservesCriticalWarnings()
    {
        var service = new DeterministicCommunityCommandCenterService();

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
        var service = new DeterministicCommunityCommandCenterService();

        var result = await service.GetAsync(role, CommandCenterMode.Strategic);

        await Assert.That(result.AttentionItems[0].Category).IsEqualTo(expectedCategory);
    }

    [Test]
    public async Task Home_RendersCommandCenterAndSupportsRoleSelection()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
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
}
