using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit.TestDoubles;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

// Alias Bunit.TestContext to avoid collision with TUnit's own TestContext type.
using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

public class CurationViewRenderTests
{
    [Test]
    public async Task Render_ShowsReadOnlyFairnessSummary_AndNoTargetInputs()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var eventId = Guid.NewGuid();
        var curationApi = Substitute.For<ICurationApi>();
        curationApi.GetDashboardAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateDashboard(eventId)));
        ctx.Services.AddSingleton(curationApi);
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization();

        var cut = ctx.RenderComponent<global::Bethuya.Hybrid.Web.Components.Pages.CurationView>(parameters =>
            parameters.Add(p => p.EventId, eventId));

        cut.WaitForAssertion(() =>
        {
            if (!cut.Markup.Contains("curation-fairness-targets-summary", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected read-only fairness summary.");

            if (!cut.Markup.Contains("curation-fairness-settings-link", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected event setup link.");

            if (cut.Markup.Contains("geo-target-input", StringComparison.Ordinal))
                throw new InvalidOperationException("Curation must not expose editable target inputs.");

            if (cut.Markup.Contains("save-fairness-targets-btn", StringComparison.Ordinal))
                throw new InvalidOperationException("Curation must not expose target save controls.");
        });
    }

    private static CurationDashboardDto CreateDashboard(Guid eventId) => new(
        EventId: eventId,
        Capacity: 100,
        Applicants: 12,
        Targets: new EventFairnessTargetsDto(0.35, 0.25, 0.25, true, 0.15, 5),
        Dimensions:
        [
            new FairnessDimensionProgressDto("geo", 0.2, 0.35, 0.15, 3, false, null)
        ],
        Registrants:
        [
            new CurationRegistrantDto(
                RegistrationId: Guid.NewGuid(),
                FullName: "Asha Kulkarni",
                Email: "asha@example.com",
                Status: "Pending",
                Interests: ["community", "workshop"],
                Impact: new ImpactPreviewDto(
                    DeltaPercentByDimension: new Dictionary<string, double> { ["geo"] = 0.12 },
                    Explanation: "Improves geo diversity toward target.",
                    IsSuppressed: false))
        ],
        CurationInsights:
        [
            "Geo diversity is below target."
        ]);
}
