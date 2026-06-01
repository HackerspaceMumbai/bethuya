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
        "HIGH IMPACT",
        "Stable",
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

        if (!css.Contains(".curation-shell", StringComparison.Ordinal)
            || !css.Contains("display: grid;", StringComparison.Ordinal)
            || !css.Contains("grid-template-rows: auto minmax(0, 1fr);", StringComparison.Ordinal)
            || !css.Contains(".workbench-grid", StringComparison.Ordinal)
            || !css.Contains("height: 100%;", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected curation shell to size the workbench from the real top rail height instead of a hard-coded offset.");
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
            || !css.Contains("grid-template-columns: auto minmax(0, 1fr) minmax(22rem, 1fr);", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected attendance etiquette to render as an interpreted reliability bar.");
        }

        if (!css.Contains(".fairness-layout-stack", StringComparison.Ordinal)
            || !css.Contains(".fairness-chip-stack", StringComparison.Ordinal)
            || !css.Contains(".metric-cluster", StringComparison.Ordinal)
            || !css.Contains(".fairness-status-summary", StringComparison.Ordinal)
            || !css.Contains(".high-impact-layout-row", StringComparison.Ordinal)
            || !css.Contains(".high-impact-left", StringComparison.Ordinal)
            || !css.Contains("width: 8.25rem;", StringComparison.Ordinal)
            || !css.Contains("height: 2.8rem;", StringComparison.Ordinal)
            || !css.Contains("flex: 0 0 8.25rem;", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected fairness budget to use a structured stacked layout while keeping the chip geometry stable.");
        }

        if (!css.Contains(".queue-item", StringComparison.Ordinal)
            || !css.Contains("display: flex;", StringComparison.Ordinal)
            || !css.Contains("flex-direction: column;", StringComparison.Ordinal)
            || !css.Contains("min-height: 10.2rem;", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected queue cards to keep the prior non-collapsing flex layout.");
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

            var fairnessSummary = cut.Find("[data-test='fairness-status-summary']");
            if (!fairnessSummary.TextContent.Contains("Fairness status: On track", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected fairness budget to surface a one-line on-track summary before any projection is active.");

            cut.Find("[data-test='high-impact-chip-section']");
            cut.Find("[data-test='stable-chip-section']");
            cut.Find("[data-test='high-impact-layout-row']");
            cut.Find("[data-test='high-impact-action-block']");
            cut.Find("[data-test='generate-curation-proposal-btn']");

            if (cut.Markup.Contains("metric-delta", StringComparison.Ordinal))
                throw new InvalidOperationException("Fairness budget deltas should not appear before a registrant is selected.");

            if (!cut.Markup.Contains("Choose someone from the queue", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected neutral detail state before a registrant is selected.");

            if (!cut.Markup.Contains("curation-approve-btn", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected sticky decision tray.");

            var firstCard = cut.Find("[data-test='curation-registrant-card']");
            if (!firstCard.InnerHtml.Contains("⭐ Strong new", StringComparison.Ordinal)
                || !firstCard.InnerHtml.Contains("High intent", StringComparison.Ordinal)
                || !firstCard.InnerHtml.Contains("+2", StringComparison.Ordinal)
                || firstCard.InnerHtml.Contains("+ Geo boost", StringComparison.Ordinal)
                || cut.Markup.Contains("Unscored", StringComparison.Ordinal)
                || cut.Markup.Contains("Needs review", StringComparison.Ordinal)
                || cut.Markup.Contains("ago", StringComparison.Ordinal)
                || !cut.Markup.Contains("High Signal", StringComparison.Ordinal)
                || cut.Markup.Contains("PRIORITY", StringComparison.Ordinal)
                || cut.Markup.Contains("BALANCED", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected queue cards to keep only the top two decision tags, show overflow, and remove recency metadata.");
            }

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
    public async Task SuggestStartingCohort_GenerateFlow_ShowsBannerAndSuggestedQueueBadges()
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

        cut.Find("[data-test='generate-curation-proposal-btn'] button").Click();
        cut.WaitForElement("[data-test='cohort-suggestion-modal']");
        cut.Find("[data-test='generate-suggestions-btn']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-test='suggested-cohort-banner']");

            if (!cut.Markup.Contains("Suggested cohort ready", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected generated suggestions to surface a ready banner.");
            }

            var badges = cut.FindAll("[data-test='queue-suggested-badge']");
            if (badges.Count == 0)
            {
                throw new InvalidOperationException("Expected generated cohort to mark suggested registrants in queue cards.");
            }

            var cards = cut.FindAll("[data-test='curation-registrant-card']");
            if (cards.Count == 0)
            {
                throw new InvalidOperationException("Expected queue to show suggested registrants after generating a cohort.");
            }

            if (cards.Any(card => card.QuerySelector("[data-test='queue-suggested-badge']") is null))
            {
                throw new InvalidOperationException("Expected generated cohort mode to auto-filter queue cards to suggested registrants.");
            }
        }, timeout: TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task SuggestStartingCohort_CanAddAndClearSuggestionsFromActionTray()
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
        cut.WaitForElement("[data-test='toggle-suggestion-btn']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-test='suggested-cohort-banner']");
            cut.Find("[data-test='suggestion-review-panel']");
        });

        cut.Find("[data-test='clear-suggestions-btn']").Click();

        cut.WaitForAssertion(() =>
        {
            if (cut.FindAll("[data-test='suggested-cohort-banner']").Count != 0)
            {
                throw new InvalidOperationException("Expected clear suggestions to reset the cohort-ready banner.");
            }

            if (cut.FindAll("[data-test='suggestion-review-panel']").Count != 0)
            {
                throw new InvalidOperationException("Expected clear suggestions to remove suggestion review details.");
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
    public async Task Render_HidesRedundantQueueImpactLineCopy()
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
            if (cut.Markup.Contains("Significant fairness delta detected.", StringComparison.Ordinal)
                || cut.Markup.Contains("No positive fairness delta from approving this registrant.", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected queue cards to remove redundant fairness-delta line copy.");
            }
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
    public async Task Render_AttendanceEtiquette_UsesDecisionSupportReliabilityStates()
    {
        using var unknownCtx = new BunitCtx();
        unknownCtx.JSInterop.Mode = JSRuntimeMode.Loose;

        var unknownEventId = Guid.NewGuid();
        var unknownApi = Substitute.For<ICurationApi>();
        unknownApi.GetDashboardAsync(unknownEventId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateDashboard(unknownEventId)));
        unknownCtx.Services.AddSingleton(unknownApi);
        unknownCtx.Services.AddBlazorBlueprintComponents();
        unknownCtx.AddTestAuthorization();

        var unknownCut = unknownCtx.RenderComponent<global::Bethuya.Hybrid.Web.Components.Pages.CurationView>(parameters =>
            parameters.Add(p => p.EventId, unknownEventId));
        unknownCut.WaitForElement("[data-test='curation-registrant-card']").Click();

        unknownCut.WaitForAssertion(() =>
        {
            if (!unknownCut.Markup.Contains("⚪ No history — rely on intent and impact", StringComparison.Ordinal)
                || !unknownCut.Markup.Contains("No attendance history — use intent and impact", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected unknown attendance state to guide review back to intent quality.");
            }
        });

        using var mediumCtx = new BunitCtx();
        mediumCtx.JSInterop.Mode = JSRuntimeMode.Loose;

        var mediumEventId = Guid.NewGuid();
        var mediumApi = Substitute.For<ICurationApi>();
        mediumApi.GetDashboardAsync(mediumEventId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateDashboard(mediumEventId, returningStandout: true, reliabilityScore: 72)));
        mediumCtx.Services.AddSingleton(mediumApi);
        mediumCtx.Services.AddBlazorBlueprintComponents();
        mediumCtx.AddTestAuthorization();

        var mediumCut = mediumCtx.RenderComponent<global::Bethuya.Hybrid.Web.Components.Pages.CurationView>(parameters =>
            parameters.Add(p => p.EventId, mediumEventId));
        mediumCut.WaitForElement("[data-test='curation-registrant-card']").Click();

        mediumCut.WaitForAssertion(() =>
        {
            if (!mediumCut.Markup.Contains("⚠️ Review carefully — moderate reliability", StringComparison.Ordinal)
                || !mediumCut.Markup.Contains("⚠️ Review carefully — moderate attendance reliability", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected medium attendance state to prompt a closer review.");
            }
        });

        using var highCtx = new BunitCtx();
        highCtx.JSInterop.Mode = JSRuntimeMode.Loose;

        var highEventId = Guid.NewGuid();
        var highApi = Substitute.For<ICurationApi>();
        highApi.GetDashboardAsync(highEventId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateDashboard(highEventId, returningStandout: true, reliabilityScore: 100)));
        highCtx.Services.AddSingleton(highApi);
        highCtx.Services.AddBlazorBlueprintComponents();
        highCtx.AddTestAuthorization();

        var highCut = highCtx.RenderComponent<global::Bethuya.Hybrid.Web.Components.Pages.CurationView>(parameters =>
            parameters.Add(p => p.EventId, highEventId));
        highCut.WaitForElement("[data-test='curation-registrant-card']").Click();

        highCut.WaitForAssertion(() =>
        {
            if (!highCut.Markup.Contains("✅ Reliable — low attendance risk", StringComparison.Ordinal)
                || !highCut.Markup.Contains("Attendance pattern supports approval confidence", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected high attendance state to surface approval-supporting guidance.");
            }
        });
    }

    [Test]
    public async Task Render_IntentInsight_UsesReadableMetricsAndSingleSummary()
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
            if (!cut.Markup.Contains("Clear and focused", StringComparison.Ordinal)
                || !cut.Markup.Contains("Some examples provided", StringComparison.Ordinal)
                || !cut.Markup.Contains("Reasonably genuine response", StringComparison.Ordinal)
                || !cut.Markup.Contains("✅ Strong intent", StringComparison.Ordinal)
                || !cut.Markup.Contains("no concerns", StringComparison.OrdinalIgnoreCase)
                || cut.Markup.Contains("Intent signal present", StringComparison.Ordinal)
                || cut.Markup.Contains("No major intent concerns", StringComparison.Ordinal)
                || cut.Markup.Contains("Specific examples and hands-on motivation", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected intent insight to keep readable metrics and a single concise interpretation.");
            }
        });
    }

    [Test]
    public async Task Render_IntentInsight_UsesMetricSpecificInterpretationWhenSignalIsWeak()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var eventId = Guid.NewGuid();
        var dashboard = CreateDashboard(eventId);
        // Override intent with low authenticity signal
        var original = dashboard.Registrants[0];
        var weakIntent = original with
        {
            Intent = original.Intent with { Authenticity = "Low" }
        };
        var modifiedDashboard = dashboard with { Registrants = [weakIntent] };

        var curationApi = Substitute.For<ICurationApi>();
        curationApi.GetDashboardAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(modifiedDashboard));
        ctx.Services.AddSingleton(curationApi);
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization();

        var cut = ctx.RenderComponent<global::Bethuya.Hybrid.Web.Components.Pages.CurationView>(parameters =>
            parameters.Add(p => p.EventId, eventId));
        cut.WaitForElement("[data-test='curation-registrant-card']").Click();

        cut.WaitForAssertion(() =>
        {
            if (!cut.Markup.Contains("⚠️ Review intent — authenticity signal is weak", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected metric-specific intent interpretation when authenticity is Low.");
        });
    }

    [Test]
    public async Task Render_Assessment_ShowsShortSignalLinesInsteadOfParagraph()
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
            var assessmentLines = cut.FindAll(".assessment-line");
            if (assessmentLines.Count != 3
                || !cut.Markup.Contains("+ Strong intent", StringComparison.Ordinal)
                || !cut.Markup.Contains("+ Fairness gain (Geo)", StringComparison.Ordinal)
                || !cut.Markup.Contains("⚠️ Org concentration risk", StringComparison.Ordinal)
                || cut.Markup.Contains("Concrete intent signal detected. Review alongside fairness impact before making a human decision.", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected assessment copy to render as short signal lines instead of a paragraph.");
            }
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

            if (!cut.Markup.Contains("HIGH IMPACT", StringComparison.Ordinal)
                || !cut.Markup.Contains("Stable", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected fairness budget to separate high-impact and stable chips.");
            }

            var fairnessBudget = cut.Find("[data-test='fairness-dimensions']");
            var fairnessSummary = cut.Find("[data-test='fairness-status-summary']");
            if (!fairnessSummary.TextContent.Contains("Fairness status: Minor imbalance", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected fairness budget summary to call out minor imbalance once a risky projection is active.");

            var highImpactRow = cut.Find("[data-test='high-impact-layout-row']");
            if (!highImpactRow.TextContent.Contains("Suggest Starting Cohort", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected fairness action button to align in the high-impact row.");
            }

            var actionBlock = cut.Find("[data-test='high-impact-action-block']");
            var hintText = actionBlock.QuerySelector(".fairness-actions-message");
            if (hintText is null || !hintText.TextContent.Contains("No proposal run in this session yet", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected proposal status text to be anchored under the high-impact action button.");
            }

            var highlightedChips = fairnessBudget.QuerySelectorAll(".metric-cluster.high-impact-cluster .metric-chip.highlighted");
            if (highlightedChips.Length != 2)
            {
                throw new InvalidOperationException("Expected fairness budget to highlight only the strongest positive and strongest negative dimensions.");
            }

            var highlightedDeltas = fairnessBudget.QuerySelectorAll(".metric-cluster.high-impact-cluster .metric-delta");
            if (highlightedDeltas.Length != 2
                || !cut.Markup.Contains("+12.0%", StringComparison.Ordinal)
                || !cut.Markup.Contains("-3.0%", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected highlighted fairness chips to surface only the top deltas.");
            }

            var stableDeltas = fairnessBudget.QuerySelectorAll(".metric-cluster.stable-cluster .metric-delta");
            if (stableDeltas.Length != 0)
            {
                throw new InvalidOperationException("Expected stable fairness chips to hide their delta labels.");
            }

            if (!cut.Markup.Contains("metric-chip positive highlighted", StringComparison.Ordinal)
                || !cut.Markup.Contains("metric-chip warning highlighted", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected both uplift and risk fairness chips to be highlighted after selection.");
            }

            var positiveNegativeDeltas = fairnessBudget
                .QuerySelectorAll(".metric-cluster.high-impact-cluster .metric-delta.positive")
                .Where(delta => delta.TextContent.Contains('-'))
                .ToList();
            var warningNegativeDeltas = fairnessBudget
                .QuerySelectorAll(".metric-cluster.high-impact-cluster .metric-delta.warning")
                .Where(delta => delta.TextContent.Contains('-'))
                .ToList();

            if (positiveNegativeDeltas.Count != 0
                || warningNegativeDeltas.Count == 0)
            {
                throw new InvalidOperationException("Expected negative fairness deltas to render with warning styling instead of positive styling.");
            }

            if (!warningNegativeDeltas.Any(delta => delta.TextContent.Contains('↓'))
                || !fairnessBudget.QuerySelectorAll(".metric-cluster.high-impact-cluster .metric-delta.positive")
                    .Any(delta => delta.TextContent.Contains('↑')))
            {
                throw new InvalidOperationException("Expected fairness chip deltas to surface directional indicators alongside the delta text.");
            }

            if (!cut.Markup.Contains("metric-chip neutral deemphasized", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected non-priority fairness budget chips to stay neutral after selection.");

            if (cut.Markup.Contains("Closing gap", StringComparison.Ordinal)
                || cut.Markup.Contains("Still below target", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected legacy fairness chip copy to be removed.");
            }

            if (cut.Markup.Contains("Improves fairness balance", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected intent interpretation callouts to stay answer-quality-only.");
            }

            if (!cut.Markup.Contains("impact-group-improvements", StringComparison.Ordinal)
                || !cut.Markup.Contains("impact-group-risks", StringComparison.Ordinal)
                || !cut.Markup.Contains("impact-group-neutral", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Impact Preview fairness section to group rows into Improvements, Risks, and Neutral.");
            }

            if (!cut.Markup.Contains("impact-neutral-summary", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected unchanged fairness dimensions to be collapsed into the neutral summary.");
            }

            var impactGroups = cut.Find("[data-test='impact-fairness-groups']").TextContent;
            if (!impactGroups.Contains("IMPROVEMENTS", StringComparison.Ordinal)
                || !impactGroups.Contains("RISKS", StringComparison.Ordinal)
                || !impactGroups.Contains("NEUTRAL", StringComparison.Ordinal)
                || !impactGroups.Contains("Geography", StringComparison.Ordinal)
                || !impactGroups.Contains("+12.0%", StringComparison.Ordinal)
                || !impactGroups.Contains("Gender", StringComparison.Ordinal)
                || !impactGroups.Contains("-3.0%", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Impact Preview to show only meaningful fairness changes and collapse the rest.");
            }

            if (cut.Markup.Contains("class=\"neutral\">+0.0%", StringComparison.Ordinal)
                || cut.Markup.Contains("class=\"neutral\">-0.0%", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected unchanged fairness dimensions to avoid per-row delta values.");
            }

            var recommendationLabelCount = cut.Markup.Split("Strong review candidate", StringSplitOptions.None).Length - 1;
            if (recommendationLabelCount != 1)
            {
                throw new InvalidOperationException("Expected recommendation classification phrase to appear only once in the selected detail surfaces.");
            }

            if (!cut.Markup.Contains("Contributing", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected intent panel badge to show dynamic persona classification.");
            }

            if (cut.Markup.Contains("Guidance:", StringComparison.Ordinal)
                || !cut.Markup.Contains("✅ Approve — improves fairness with no major risks", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected sticky action bar to show single-line decision guidance for positive candidates.");
            }

            if (cut.Markup.Contains(">Projection<", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected impact header pill to avoid static projection copy.");
            }

            if (!cut.Markup.Contains("Fairness lift", StringComparison.Ordinal)
                && !cut.Markup.Contains("Risk drift", StringComparison.Ordinal)
                && !cut.Markup.Contains("Neutral shift", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected impact header pill to show dynamic impact-state wording.");
            }

            if (cut.Markup.Contains("Advisory:", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected sticky action bar label to be renamed from Advisory to Guidance.");
            }
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

    [Test]
    public async Task Render_ImpactPreview_ShowsTopTwoRisksAndCollapsesRemaining()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var eventId = Guid.NewGuid();
        var curationApi = Substitute.For<ICurationApi>();
        curationApi.GetDashboardAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateDashboard(
                eventId,
                returningStandout: true,
                impactDeltas: new Dictionary<string, double>
                {
                    ["geo"] = 0.089,
                    ["socioeconomic"] = -0.111,
                    ["language"] = -0.069,
                    ["gender"] = -0.031,
                    ["education"] = -0.025
                })));
        ctx.Services.AddSingleton(curationApi);
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization();

        var cut = ctx.RenderComponent<global::Bethuya.Hybrid.Web.Components.Pages.CurationView>(parameters =>
            parameters.Add(p => p.EventId, eventId));
        cut.WaitForElement("[data-test='curation-registrant-card']").Click();

        cut.WaitForAssertion(() =>
        {
            var risks = cut.Find("[data-test='impact-group-risks']");
            var visibleRiskLines = risks.QuerySelectorAll(".impact-line-item").Length;
            if (visibleRiskLines != 2)
            {
                throw new InvalidOperationException("Expected Impact Preview to show only top two risk rows.");
            }

            var collapsed = cut.Find("[data-test='impact-risks-collapsed']");
            if (!collapsed.TextContent.Contains("more risks", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected remaining risk rows to collapse behind a +N more risks summary.");
            }

            var improvements = cut.Find("[data-test='impact-group-improvements']");
            if (improvements.QuerySelector("[data-test='impact-risks-collapsed']") is not null)
            {
                throw new InvalidOperationException("Expected improvements group to remain fully visible without collapse.");
            }
        });
    }

    private static CurationDashboardDto CreateDashboard(
        Guid eventId,
        bool impactSuppressed = false,
        bool returningStandout = false,
        int? reliabilityScore = null,
        IReadOnlyDictionary<string, double>? impactDeltas = null) => new(
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
                    GitHubRepoCount: 36,
                    IsGitHubLinked: true,
                    IsLinkedInVerified: true,
                    MemberSinceYear: 2021,
                    Tags: returningStandout ? ["2 attended", "community", "robotics"] : ["First timer", "community", "robotics"]),
                Reliability: new CurationReliabilityDto(
                    HasHistory: returningStandout,
                    Score: returningStandout ? reliabilityScore ?? 100 : 0,
                    Label: returningStandout
                        ? (reliabilityScore ?? 100) >= 85 ? "Excellent" : "Mixed"
                        : "Unscored",
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
                    Highlights: ["Geo +12.0%", "No prior RSVP history"],
                    AssessmentText: "+ Strong intent\n+ Fairness gain (Geo)\n⚠️ Org concentration risk"),
                Impact: new ImpactPreviewDto(
                    DeltaPercentByDimension: impactSuppressed
                        ? new Dictionary<string, double>()
                        : impactDeltas is null
                            ? new Dictionary<string, double> { ["geo"] = 0.12, ["education"] = 0.04, ["gender"] = -0.03 }
                            : impactDeltas.ToDictionary(pair => pair.Key, pair => pair.Value),
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
