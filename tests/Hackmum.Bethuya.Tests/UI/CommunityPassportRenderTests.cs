using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit.TestDoubles;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

// Alias Bunit.TestContext to avoid collision with TUnit's own TestContext type.
using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

/// <summary>
/// Render smoke-tests for CommunityPassport.razor.
///
/// Rationale: this page contains BB form-field wrapper components (BbFormFieldSelect,
/// BbButton) that do not support unknown HTML attributes. A render test catches parameter
/// validation errors (e.g. data-test placed on a BB component instead of a wrapper div)
/// that only surface at runtime inside a full Blazor render host — not during build.
/// </summary>
public class CommunityPassportRenderTests
{
    [Test]
    public async Task Render_DoesNotThrow_WhenPassportLoads()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var api = Substitute.For<ICommunityPassportApi>();
        api.GetPassportAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(BuildPassportFixture()));
        ctx.Services.AddSingleton(api);
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization();

        // Must not throw InvalidOperationException due to unknown component parameters
        // (e.g. data-test on BbFormFieldSelect / BbButton).
        var cut = ctx.RenderComponent<global::Bethuya.Hybrid.Shared.Pages.CommunityPassport>();

        cut.WaitForAssertion(() =>
        {
            if (!cut.Markup.Contains("Community Passport", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected Community Passport heading to be rendered.");

            // Privacy section with the BbFormFieldSelect wrapper must render its data-test on the div
            cut.Find("[data-test='passport-visibility-select']");
            cut.Find("[data-test='passport-save-privacy-btn']");
        });
    }

    [Test]
    public async Task Render_ShowsLoadingState_Initially()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var tcs = new TaskCompletionSource<CommunityPassportDto>();
        var api = Substitute.For<ICommunityPassportApi>();
        api.GetPassportAsync(Arg.Any<CancellationToken>()).Returns(tcs.Task);
        ctx.Services.AddSingleton(api);
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization();

        var cut = ctx.RenderComponent<global::Bethuya.Hybrid.Shared.Pages.CommunityPassport>();

        // Must show loading alert before API resolves
        cut.Find("[data-test='community-passport-loading']");

        // Unblock so the component finalises cleanly
        tcs.SetResult(BuildPassportFixture());
    }

    private static CommunityPassportDto BuildPassportFixture() => new(
        DisplayName: "Test Member",
        Email: "test@example.com",
        OccupationStatus: "Engineer",
        CompanyName: "Bethuya",
        EducationInstitute: null,
        CurrentTier: "Contributor",
        Metrics: new PassportMetricsDto(3, 2, 1, 4, 5),
        Privacy: new PassportPrivacyDto("CommunityOnly", true, true),
        Residency: new PassportResidencyDto("Asia-Pacific", "SovereignRegion", "PDPA"),
        LinkedIdentities:
        [
            new PassportIdentityDto("GitHub", "gh-subject", "testmember", null, true, DateTimeOffset.UtcNow)
        ],
        Timeline:
        [
            new PassportTimelineEntryDto(
                Guid.NewGuid(), "Hackerspace Meetup", "Attended",
                DateTimeOffset.UtcNow.AddMonths(-1), "Attended event")
        ]);
}
