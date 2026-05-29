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
    private static readonly string[] ExpectedFairnessBudgetLabels =
    [
        "Core",
        "Diversity",
        "Cohort Health",
        "Gender",
        "First Timer",
        "Equity",
        "Geography",
        "Language",
        "Education",
        "Inclusion",
        "Organization",
        "Reliability"
    ];

    [Test]
    public void Css_DefinesBoundedIndependentCurationScrollPanes()
    {
        var cssPath = FindRepositoryFile(
            "src",
            "Bethuya.Hybrid",
            "Bethuya.Hybrid.Web",
            "Components",
            "Pages",
            "CurationView.razor.css");
        var css = File.ReadAllText(cssPath);

        if (!css.Contains(".workbench-grid", StringComparison.Ordinal)
            || !css.Contains("position: fixed;", StringComparison.Ordinal)
            || !css.Contains("top: calc(var(--curation-topbar-height) + var(--curation-workbench-top-buffer));", StringComparison.Ordinal)
            || !css.Contains("bottom: var(--curation-tray-height);", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected workbench to be fixed between the top and bottom rails.");
        }

        if (!css.Contains("overflow: hidden;", StringComparison.Ordinal))
            throw new InvalidOperationException("Expected curation shell to prevent page-level workbench scrolling.");

        if (!css.Contains(".queue-list", StringComparison.Ordinal)
            || !css.Contains("overflow-y: auto;", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected registrant queue to own its vertical scroll.");
        }

        if (!css.Contains(".detail-stack", StringComparison.Ordinal)
            || !css.Contains("overflow-y: auto;", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected profile detail pane to own its vertical scroll.");
        }

        if (!css.Contains(".selected-hero", StringComparison.Ordinal)
            || !css.Contains("grid-template-columns: minmax(0, 1fr);", StringComparison.Ordinal)
            || !css.Contains(".selected-hero-main", StringComparison.Ordinal)
            || !css.Contains("grid-template-columns: auto minmax(0, 1fr);", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected selected profile header to keep the avatar and identity aligned side-by-side.");
        }

        if (!css.Contains(".attendance-bar", StringComparison.Ordinal)
            || !css.Contains("grid-template-columns: auto minmax(0, 1fr) minmax(7rem, auto) minmax(5rem, auto);", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected attendance etiquette to render as a compact horizontal reliability bar.");
        }

        if (!css.Contains(".metric-clusters", StringComparison.Ordinal)
            || !css.Contains("grid-template-areas:", StringComparison.Ordinal)
            || !css.Contains("\"core core\"", StringComparison.Ordinal)
            || !css.Contains("\"diversity cohort\"", StringComparison.Ordinal)
            || !css.Contains("width: 8.25rem;", StringComparison.Ordinal)
            || !css.Contains("height: 3.85rem;", StringComparison.Ordinal)
            || !css.Contains("flex: 0 0 8.25rem;", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected fairness budget chips to use stable fixed geometry across selections.");
        }
    }

    private static string FindRepositoryFile(params string[] pathSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. pathSegments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find repository file.", Path.Combine(pathSegments));
    }

    [Test]
    public async Task Render_ShowsCurationWorkbench_AndKeepsRegistrantSummaryPrivacySafe()
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
            if (!cut.Markup.Contains("Curation Intelligence", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected redesigned curation title.");

            if (!cut.Markup.Contains("curation-registrant-list", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected registrant queue.");

            if (!cut.Markup.Contains("core-gender-chip", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected aggregate gender core chip.");

            foreach (var label in ExpectedFairnessBudgetLabels)
            {
                if (!cut.Markup.Contains(label, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Expected fairness budget label '{label}'.");
            }

            if (cut.Markup.Contains("metric-delta", StringComparison.Ordinal))
                throw new InvalidOperationException("Fairness budget deltas should not appear before a registrant is selected.");

            if (!cut.Markup.Contains("Choose someone from the queue", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected neutral detail state before a registrant is selected.");

            if (!cut.Markup.Contains("curation-approve-btn", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected sticky decision tray.");

            var markup = cut.Markup.ToLowerInvariant();
            if (markup.Contains("male", StringComparison.Ordinal)
                || markup.Contains("female", StringComparison.Ordinal)
                || markup.Contains("gender identity", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Curation UI leaked per-registrant gender data.");
            }
        });
    }

    [Test]
    public async Task Render_ShowsImpactSuppressionMessage_WhenPreviewIsSuppressed()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var eventId = Guid.NewGuid();
        var curationApi = Substitute.For<ICurationApi>();
        curationApi.GetDashboardAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateDashboard(eventId, impactSuppressed: true)));
        ctx.Services.AddSingleton(curationApi);
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization();

        var cut = ctx.RenderComponent<global::Bethuya.Hybrid.Web.Components.Pages.CurationView>(parameters =>
            parameters.Add(p => p.EventId, eventId));

        cut.WaitForElement("[data-test='curation-registrant-card']").Click();

        cut.WaitForAssertion(() =>
        {
            if (!cut.Markup.Contains("impact-suppressed", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected suppression marker when impact preview is hidden.");
        });
    }

    [Test]
    public async Task Render_UsesSubduedImpactLine_WhenNoPositiveFairnessDelta()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var eventId = Guid.NewGuid();
        var curationApi = Substitute.For<ICurationApi>();
        curationApi.GetDashboardAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateDashboard(eventId, impactSuppressed: true)));
        ctx.Services.AddSingleton(curationApi);
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization();

        var cut = ctx.RenderComponent<global::Bethuya.Hybrid.Web.Components.Pages.CurationView>(parameters =>
            parameters.Add(p => p.EventId, eventId));

        cut.WaitForAssertion(() =>
        {
            if (!cut.Markup.Contains("impact-line subdued", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected no-positive-delta queue copy to use subdued styling.");

            if (!cut.Markup.Contains("No positive fairness delta from approving this registrant.", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected no-positive-delta queue copy.");
        });
    }

    [Test]
    public async Task Render_DoesNotCallNoHistoryPositiveCandidateStandout()
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
            if (cut.Markup.Contains("Standout", StringComparison.Ordinal))
                throw new InvalidOperationException("No-history candidates must not be labeled standout.");

            if (!cut.Markup.Contains("Strong new", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected no-history positive candidate to use the strong-new label.");
        });
    }

    [Test]
    public async Task ClickRegistrant_ShowsProjectionActiveMicrointeraction()
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

        cut.WaitForElement("[data-test='curation-registrant-card']").Click();

        cut.WaitForAssertion(() =>
        {
            if (!cut.Markup.Contains("Projection live", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected selected queue card to expose projection-active feedback.");

            if (!cut.Markup.Contains("Impact projection active for Asha Kulkarni", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected projection banner to update for clicked registrant.");

            if (!cut.Markup.Contains("metric-delta positive", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected selected registrant to reveal positive fairness budget delta.");

            if (!cut.Markup.Contains("metric-chip positive affected", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected affected fairness budget chips to glow after selection.");

            if (!cut.Markup.Contains("metric-chip warning unaffected", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected unaffected fairness budget chips to dim after selection.");
        });
    }

    [Test]
    public async Task Render_CallsOrganizerMarkedReturningCandidateStandout()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var eventId = Guid.NewGuid();
        var curationApi = Substitute.For<ICurationApi>();
        curationApi.GetDashboardAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateDashboard(eventId, returningStandout: true)));
        ctx.Services.AddSingleton(curationApi);
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization();

        var cut = ctx.RenderComponent<global::Bethuya.Hybrid.Web.Components.Pages.CurationView>(parameters =>
            parameters.Add(p => p.EventId, eventId));

        cut.WaitForAssertion(() =>
        {
            if (!cut.Markup.Contains("Standout", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected organizer-marked returning candidate to be labeled standout.");
        });
    }

    private static CurationDashboardDto CreateDashboard(Guid eventId, bool impactSuppressed = false, bool returningStandout = false) => new(
        EventId: eventId,
        EventTitle: "Mumbai AI & Robotics Summit",
        Capacity: 100,
        Applicants: 12,
        Targets: new EventFairnessTargetsDto(0.35, 0.25, 0.25, true, 0.15, 5),
        GenderProgress: new FairnessDimensionProgressDto("gender", 0.4, 0.4, 0, 0, false, null),
        Dimensions:
        [
            new FairnessDimensionProgressDto("Geo diversity", 0.28, 0.35, 0.07, 2, false, "Need 2 more outside dominant geo bucket to meet target."),
            new FairnessDimensionProgressDto("Language diversity (Marathi/Konkani)", 0.33, 0.25, 0, 0, false, null),
            new FairnessDimensionProgressDto("Education diversity", 0.19, 0.25, 0.06, 1, false, "Need 1 more underrepresented education attendee to meet target.")
        ],
        Registrants:
        [
            new CurationRegistrantDto(
                RegistrationId: Guid.NewGuid(),
                FullName: "Asha Kulkarni",
                Email: "asha@example.com",
                Status: "Pending",
                RegisteredAt: DateTimeOffset.UtcNow.AddDays(-2),
                Bio: "I want to contribute to community robotics projects and learn from other builders.",
                Interests: ["community", "workshop", "robotics"],
                Profile: new CurationProfileSummaryDto(
                    Headline: "Independent builder",
                    Organization: "Example Labs",
                    HistoryLabel: returningStandout ? "2 prior approvals across community events" : "No prior Bethuya attendance history",
                    IsFirstTimer: !returningStandout,
                    PastAcceptedCount: returningStandout ? 2 : 0,
                    PastAttendedCount: returningStandout ? 2 : 0,
                    HasOrganizerStandoutContribution: returningStandout,
                    Tags: returningStandout ? ["2 attended", "community", "robotics"] : ["First timer", "community", "robotics"]),
                Reliability: new CurationReliabilityDto(
                    HasHistory: returningStandout,
                    Score: returningStandout ? 100 : 0,
                    Label: returningStandout ? "Excellent" : "Unscored",
                    Summary: returningStandout ? "Attended all 2 prior approved events." : "No prior attendance or RSVP data linked yet."),
                Intent: new CurationIntentInsightDto(
                    Summary: "I want to contribute to community robotics projects and learn from other builders.",
                    Specificity: "Medium",
                    Evidence: "High",
                    Authenticity: "High",
                    Signals: ["Builder intent", "Fairness lift detected"],
                    Interpretation: "Specific examples and hands-on motivation suggest a contributing attendee profile."),
                Recommendation: new CurationRecommendationDto(
                    Label: "Strong review candidate",
                    Tone: "positive",
                    Summary: "Concrete intent signal detected. Review alongside fairness impact before making a human decision.",
                    Highlights: ["Geo +12.0%", "No prior RSVP history"]),
                Impact: new ImpactPreviewDto(
                    DeltaPercentByDimension: impactSuppressed
                        ? new Dictionary<string, double>()
                        : new Dictionary<string, double> { ["geo"] = 0.12, ["education"] = 0.04 },
                    Explanation: impactSuppressed
                        ? "Impact preview hidden until at least 5 attendees are selected."
                        : "Improves geo diversity toward target.",
                    IsSuppressed: impactSuppressed))
        ],
        CurationInsights:
        [
            "Geo diversity is below target."
        ]);
}
