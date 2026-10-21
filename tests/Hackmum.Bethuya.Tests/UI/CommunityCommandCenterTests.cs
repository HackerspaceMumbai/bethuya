using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Shared.Layout;
using Bethuya.Hybrid.Shared.Models.CommandCenter;
using Bethuya.Hybrid.Shared.Models.Participation;
using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Security.Claims;

using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

public sealed class CommunityCommandCenterTests
{
    private static readonly TimeProvider EventPressureClock =
        new FixedTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    private static readonly string[] V9Sections =
    [
        "strategic-insight",
        "immediate-decisions",
        "programs-guilds",
        "active-event-workspaces",
        "leadership-signals",
        "member-growth-journeys",
        "chapter-recommendations",
        "chapter-health"
    ];

    [Test]
    public async Task DeterministicProvider_ReturnsNineSectionProjection()
    {
        var service = new DeterministicCommunityCommandCenterService(EventPressureClock);

        var result = await service.GetAsync(CommunityRole.EventOrganizer);

        await Assert.That(result.Insight.Observation).Contains("Hacktoberfest");
        await Assert.That(result.Decisions).IsNotEmpty();
        await Assert.That(result.Programs).IsNotEmpty();
        await Assert.That(result.EventWorkspaces.Any(workspace => workspace.IsHero)).IsTrue();
        await Assert.That(result.LeadershipSignals).IsNotEmpty();
        await Assert.That(result.GrowthJourneys).IsNotEmpty();
        await Assert.That(result.ChapterRecommendations).IsNotEmpty();
        await Assert.That(result.ChapterHealth).IsNotEmpty();
        await Assert.That(result.EventWorkspaces[0].Title).Contains("Hacktoberfest Mumbai");
        await Assert.That(result.EventWorkspaces[0].DateLabel).Contains("2026");
    }

    [Test]
    public async Task DeterministicProvider_AdministratorInsight_UsesMentorshipNarrative()
    {
        var service = new DeterministicCommunityCommandCenterService(EventPressureClock);

        var result = await service.GetAsync(CommunityRole.CommunityAdministrator);

        await Assert.That(result.Insight.Observation).Contains("Open Source Mentorship Circle");
        await Assert.That(result.Insight.Risk).Contains("mentorship bottleneck");
        await Assert.That(result.Insight.RecommendedAction).Contains("mentor");
    }

    [Test]
    public async Task DeterministicProvider_RoleReordersDecisionsWithoutRemovingSections()
    {
        var service = new DeterministicCommunityCommandCenterService(EventPressureClock);

        var volunteer = await service.GetAsync(CommunityRole.VolunteerLead);
        var mentor = await service.GetAsync(CommunityRole.MentorshipLead);

        await Assert.That(volunteer.Decisions[0].Kind).IsEqualTo(DecisionKind.VolunteerAssignment);
        await Assert.That(mentor.Decisions[0].Kind).IsEqualTo(DecisionKind.MentorNomination);
        await Assert.That(volunteer.Programs.Count).IsEqualTo(mentor.Programs.Count);
        await Assert.That(volunteer.GrowthJourneys.Count).IsEqualTo(mentor.GrowthJourneys.Count);
    }

    [Test]
    public async Task DeterministicProvider_LeadershipSignalsExposeLedgerEvidence()
    {
        var service = new DeterministicCommunityCommandCenterService(EventPressureClock);

        var result = await service.GetAsync(CommunityRole.CommunityAdministrator);

        await Assert.That(result.LeadershipSignals.All(signal => signal.Evidence.Count > 0)).IsTrue();
        await Assert.That(result.LeadershipSignals.Any(signal => signal.Urgency == SignalUrgency.Intervention)).IsTrue();
    }

    [Test]
    public async Task DeterministicProvider_GrowthJourneysHaveStableMemberIdsAndStages()
    {
        var service = new DeterministicCommunityCommandCenterService(EventPressureClock);

        var result = await service.GetAsync(CommunityRole.CommunityAdministrator);

        await Assert.That(result.GrowthJourneys.Select(journey => journey.MemberId).Distinct().Count())
            .IsEqualTo(result.GrowthJourneys.Count);
        await Assert.That(result.GrowthJourneys.All(journey =>
            journey.Stages.Any(stage => stage.Status == JourneyStageStatus.Recommended))).IsTrue();
        await Assert.That(result.GrowthJourneys.All(journey => journey.LedgerTimeline.Count > 0)).IsTrue();
    }

    [Test]
    public async Task ParticipationProvider_RollsUpcomingActivitiesIntoNextYear()
    {
        var service = new DeterministicCommunityParticipationService(
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero)));

        var result = await service.GetAsync(CommunityRole.EventParticipant, "Anish");

        await Assert.That(result.UpcomingActivities).IsNotEmpty();
        await Assert.That(result.UpcomingActivities.All(activity => activity.DateLabel.Contains("2027", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(result.OnboardingSteps[0].Detail).Contains("17 Oct 2027");
    }

    [Test]
    [Arguments(16)]
    [Arguments(17)]
    public async Task ParticipationProvider_DoesNotShowPastWelcomeCallAsUpcoming(int day)
    {
        var service = new DeterministicCommunityParticipationService(
            new FixedTimeProvider(new DateTimeOffset(2026, 10, day, 12, 0, 0, TimeSpan.Zero)));

        var result = await service.GetAsync(CommunityRole.EventParticipant, "Anish");

        await Assert.That(result.UpcomingActivities.Select(activity => activity.Title))
            .IsEquivalentTo(["Hacktoberfest Mumbai"]);
    }

    [Test]
    [Arguments("dev-persona-priya", "Priya", CommunityRole.VolunteerLead, "Volunteer lead")]
    [Arguments("dev-persona-rohan", "Rohan", CommunityRole.EventOrganizer, "Event organizer")]
    [Arguments("dev-persona-maya", "Maya", CommunityRole.MentorshipLead, "Mentorship lead")]
    [Arguments("dev-persona-vikram", "Vikram", CommunityRole.CommunityAdministrator, "Community administrator")]
    public async Task Home_OperatingPersonasShareOneV9CommandCenter(
        string subject,
        string name,
        CommunityRole expectedRole,
        string expectedLabel)
    {
        using var ctx = CreateCommandCenterContext();
        var principal = CreatePrincipal(subject, name, BethuyaRoles.Attendee);

        var cut = RenderHome(ctx, principal);
        cut.WaitForElement("[data-test='command-center-audience']");

        await Assert.That(cut.Find("[data-test='command-center-audience']").GetAttribute("data-audience"))
            .IsEqualTo(expectedRole.ToString());
        await Assert.That(cut.Find("[data-test='command-center-audience']").TextContent).Contains(expectedLabel);

        foreach (var section in V9Sections)
        {
            await Assert.That(cut.FindAll($"[data-test='{section}']").Count).IsEqualTo(1);
        }

        AssertAppearsBefore(cut.Markup, "strategic-insight", "immediate-decisions");
        AssertAppearsBefore(cut.Markup, "immediate-decisions", "command-context");
        AssertAppearsBefore(cut.Markup, "command-context", "programs-guilds");
        AssertAppearsBefore(cut.Markup, "programs-guilds", "active-event-workspaces");
        AssertAppearsBefore(cut.Markup, "active-event-workspaces", "leadership-signals");
        AssertAppearsBefore(cut.Markup, "leadership-signals", "member-growth-journeys");
        AssertAppearsBefore(cut.Markup, "member-growth-journeys", "chapter-recommendations");
        AssertAppearsBefore(cut.Markup, "chapter-recommendations", "chapter-health");

        await Assert.That(cut.Find("[data-test='event-readiness']").TextContent).Contains("READY");
        await Assert.That(cut.Find("[data-test='signal-journey']").TextContent).Contains("Next stage");
        await Assert.That(cut.FindAll("[data-test='journey-pathway']").Count).IsGreaterThan(0);
        await Assert.That(cut.Find("[data-test='decision-evidence']").TextContent).Contains("Evidence");
        await Assert.That(cut.FindAll("[data-test='community-snapshot']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='mode-explanation']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='quick-actions']").Count).IsEqualTo(0);
        await Assert.That(cut.Markup).DoesNotContain("Approve All");
        await Assert.That(cut.Markup).DoesNotContain("Resolve All");
        await Assert.That(cut.Find("[data-test='governance-no-batch']").TextContent)
            .Contains("human-in-the-loop");
    }

    [Test]
    public async Task Home_ResolvingDecision_RemovesOnlyThatCard()
    {
        using var ctx = CreateCommandCenterContext();
        var cut = RenderHome(ctx, CreatePrincipal("dev-persona-vikram", "Vikram", BethuyaRoles.Admin));
        cut.WaitForElement("[data-test='decision-card']");

        var before = cut.FindAll("[data-test='decision-card']").Count;
        await Assert.That(before).IsGreaterThan(1);

        var firstId = cut.Find("[data-test='decision-card']").GetAttribute("data-decision-id");
        await cut.Find("[data-test='decision-primary'] button").ClickAsync(new());
        cut.WaitForAssertion(() =>
        {
            if (cut.FindAll($"[data-decision-id='{firstId}']").Count != 0)
            {
                throw new InvalidOperationException("Resolved decision remained in the queue.");
            }
        }, TimeSpan.FromSeconds(3));

        await Assert.That(cut.FindAll("[data-test='decision-card']").Count).IsEqualTo(before - 1);
        await Assert.That(cut.FindAll("[data-test='journey-row']").Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Home_ViewEvidence_OpensDrawerWithoutUnmountingJourneyRows()
    {
        using var ctx = CreateCommandCenterContext();
        ctx.JSInterop.SetupVoid("bethuyaEvidenceDrawer.open", _ => true);
        ctx.JSInterop.SetupVoid("bethuyaEvidenceDrawer.close", _ => true);
        ctx.JSInterop.SetupVoid("bethuyaEvidenceDrawer.focusSelector", _ => true);

        var cut = RenderHome(ctx, CreatePrincipal("dev-persona-vikram", "Vikram", BethuyaRoles.Admin));
        cut.WaitForElement("[data-test='journey-row']");
        var rowCount = cut.FindAll("[data-test='journey-row']").Count;

        await cut.Find("[data-test='view-evidence'] button").ClickAsync(new());
        cut.WaitForElement("[data-test='growth-journey-evidence-drawer']");

        await Assert.That(cut.Find("[data-test='growth-journey-evidence-drawer']").HasAttribute("open") ||
                          cut.FindAll("[data-test='growth-journey-evidence-drawer']").Count == 1).IsTrue();
        await Assert.That(cut.FindAll("[data-test='journey-row']").Count).IsEqualTo(rowCount);
        await Assert.That(cut.Markup).Contains("Ledger timeline");
    }

    [Test]
    [Arguments("dev-persona-anish", "Anish", CommunityRole.EventParticipant, "Event participant", "onboarding")]
    [Arguments("dev-persona-farah", "Farah", CommunityRole.EmergingContributor, "Emerging contributor", "journey")]
    public async Task Home_ParticipationPersonasRenderJourneySurfaceWithoutOperations(
        string subject,
        string name,
        CommunityRole expectedRole,
        string expectedLabel,
        string expectedMode)
    {
        using var ctx = CreateCommandCenterContext();

        var cut = RenderHome(ctx, CreatePrincipal(subject, name, BethuyaRoles.Attendee));
        cut.WaitForElement("[data-test='community-participation']");

        await Assert.That(ClaimsCommandCenterAudienceResolver.GetSurface(expectedRole))
            .IsEqualTo(CommandCenterSurface.Participation);
        await Assert.That(cut.Find("[data-test='community-participation']").GetAttribute("data-participation-mode"))
            .IsEqualTo(expectedMode);
        await Assert.That(cut.Markup).Contains(expectedLabel);

        await Assert.That(cut.FindAll("[data-test='community-command-center']").Count).IsEqualTo(0);
        foreach (var section in V9Sections)
        {
            await Assert.That(cut.FindAll($"[data-test='{section}']").Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Home_EmergingContributorSeesEarnedParticipationSections()
    {
        using var ctx = CreateCommandCenterContext();

        var cut = RenderHome(ctx, CreatePrincipal("dev-persona-farah", "Farah", BethuyaRoles.Attendee));
        cut.WaitForElement("[data-test='community-participation']");

        await Assert.That(cut.FindAll("[data-test='community-passport']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='participation-timeline']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='contribution-history']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='recommended-opportunities']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='earned-volunteer']").Count).IsEqualTo(1);
    }

    [Test]
    public async Task Home_UnauthenticatedPrincipal_DoesNotExposeCommandCenterProjection()
    {
        using var ctx = CreateCommandCenterContext();

        var cut = RenderHome(ctx, new ClaimsPrincipal());
        cut.WaitForElement("[data-test='command-center-auth-required']");

        await Assert.That(cut.FindAll("[data-test='strategic-insight']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='immediate-decisions']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Home_WithoutAuthenticationCascade_ShowsSignInRequiredState()
    {
        using var ctx = CreateCommandCenterContext();

        var cut = ctx.RenderComponent<Home>();
        cut.WaitForElement("[data-test='command-center-auth-required']");

        await Assert.That(cut.FindAll("[data-test='loading-state']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='strategic-insight']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Home_AuthenticationStateBecomesAnonymous_ClearsExistingProjection()
    {
        using var ctx = CreateCommandCenterContext();
        var cut = RenderHome(ctx, CreatePrincipal("dev-persona-vikram", "Vikram", BethuyaRoles.Admin));
        cut.WaitForElement("[data-test='strategic-insight']");

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Value, Task.FromResult(new AuthenticationState(new ClaimsPrincipal())))
            .AddChildContent<Home>());
        cut.WaitForElement("[data-test='command-center-auth-required']");

        await Assert.That(cut.FindAll("[data-test='strategic-insight']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='immediate-decisions']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='command-center-audience']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Home_AuthenticationStatePending_ClearsExistingProjectionBeforeResolution()
    {
        using var ctx = CreateCommandCenterContext();
        var cut = RenderHome(ctx, CreatePrincipal("dev-persona-vikram", "Vikram", BethuyaRoles.Admin));
        cut.WaitForElement("[data-test='strategic-insight']");
        var pendingState = new TaskCompletionSource<AuthenticationState>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Value, pendingState.Task)
            .AddChildContent<Home>());

        await Assert.That(cut.FindAll("[data-test='strategic-insight']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='immediate-decisions']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='command-center-audience']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='loading-state']").Count).IsEqualTo(1);

        pendingState.SetResult(new AuthenticationState(new ClaimsPrincipal()));
        cut.WaitForElement("[data-test='command-center-auth-required']");
    }

    [Test]
    public async Task Home_OlderParticipationLoadCannotOverwriteNewOperationsPrincipal()
    {
        using var ctx = CreateCommandCenterContext();
        var staleParticipation = await new DeterministicCommunityParticipationService(EventPressureClock)
            .GetAsync(CommunityRole.EmergingContributor, "Farah");
        var delayedParticipation = new TaskCompletionSource<CommunityParticipationHome>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var participationService = Substitute.For<ICommunityParticipationService>();
        participationService.GetAsync(
                Arg.Any<CommunityRole>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(delayedParticipation.Task);
        ctx.Services.AddSingleton(participationService);

        var cut = RenderHome(ctx, CreatePrincipal("dev-persona-farah", "Farah", BethuyaRoles.Attendee));
        cut.WaitForElement("[data-test='loading-state']");

        cut.SetParametersAndRender(parameters => parameters
            .Add(
                component => component.Value,
                Task.FromResult(new AuthenticationState(
                    CreatePrincipal("dev-persona-vikram", "Vikram", BethuyaRoles.Admin))))
            .AddChildContent<Home>());
        cut.WaitForElement("[data-test='strategic-insight']");

        delayedParticipation.SetResult(staleParticipation);
        cut.WaitForAssertion(() =>
        {
            if (cut.FindAll("[data-test='community-participation']").Count != 0)
            {
                throw new InvalidOperationException("The stale participation load replaced the current operations surface.");
            }
        });
        await Assert.That(cut.FindAll("[data-test='strategic-insight']").Count).IsEqualTo(1);
        await Assert.That(cut.Find("[data-test='command-center-audience']").TextContent).Contains("Community administrator");
    }

    [Test]
    public async Task AudienceResolver_DoesNotTrustDevelopmentSubjectFromProductionScheme()
    {
        var resolver = new ClaimsCommandCenterAudienceResolver();
        var principal = CreatePrincipal(
            "dev-persona-vikram",
            "Collision",
            BethuyaRoles.Attendee,
            authenticationType: "oidc");

        var audience = resolver.Resolve(principal);

        await Assert.That(audience.Role).IsEqualTo(CommunityRole.CommunityMember);
    }

    [Test]
    [Arguments(BethuyaRoles.Admin, CommunityRole.CommunityAdministrator)]
    [Arguments(BethuyaRoles.Organizer, CommunityRole.EventOrganizer)]
    [Arguments(BethuyaRoles.Curator, CommunityRole.CommunityMember)]
    [Arguments(BethuyaRoles.Attendee, CommunityRole.CommunityMember)]
    public async Task AudienceResolver_UsesSafeRoleFallback(string role, CommunityRole expectedRole)
    {
        var resolver = new ClaimsCommandCenterAudienceResolver();
        var principal = CreatePrincipal("production-user", "Production User", role, "oidc");

        var audience = resolver.Resolve(principal);

        await Assert.That(audience.Role).IsEqualTo(expectedRole);
    }

    [Test]
    public async Task ProgramsPage_RendersProducedOutcomesFromSameProjection()
    {
        using var ctx = CreateCommandCenterContext();
        ctx.Services.AddAuthorizationCore();
        ctx.AddTestAuthorization().SetAuthorized("Vikram").SetRoles(BethuyaRoles.Organizer, BethuyaRoles.Admin);

        var cut = ctx.RenderComponent<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(component => component.Value, Task.FromResult(new AuthenticationState(
                CreatePrincipal("dev-persona-vikram", "Vikram", BethuyaRoles.Organizer))))
            .AddChildContent<Programs>());
        cut.WaitForElement("[data-test='program-card']");

        await Assert.That(cut.Find("[data-test='programs-page']")).IsNotNull();
        await Assert.That(cut.Markup).Contains("Open Source Mentorship Circle");
        await Assert.That(cut.Markup).Contains("Future Tech Leads");
        await Assert.That(cut.Markup).DoesNotContain("Sessions held");
    }

    [Test]
    public async Task NavMenu_OrganizerSeesCommunityInsightsOrganizerAndPathFinder()
    {
        using var ctx = CreateNavContext(BethuyaRoles.Organizer);

        var cut = ctx.RenderComponent<NavMenu>();
        cut.WaitForElement("[data-test='nav-section-organizer']");

        await Assert.That(cut.FindAll("[data-test='nav-section-community']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='nav-section-insights']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='nav-section-my-space']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='nav-programs-link']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='nav-community-passports-link']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='nav-command-center-link']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='nav-path-finder-link']").Count).IsEqualTo(1);
        await Assert.That(cut.Find("[data-test='nav-path-finder-link']").GetAttribute("href")).IsEqualTo("opportunities");
        await Assert.That(cut.FindAll("[data-test='nav-imports-link']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='nav-passport-link']").Count).IsEqualTo(1);
        await Assert.That(cut.Find("[data-test='nav-events-link']").TextContent).Contains("Events & Gatherings");
        AssertAppearsBefore(cut.Markup, "nav-participation-ledger-link", "nav-relationship-topology-link");
        AssertAppearsBefore(cut.Markup, "nav-relationship-topology-link", "nav-community-graph-link");
    }

    [Test]
    public async Task NavMenu_MemberDoesNotSeeImportsDirectoryOrPathFinder()
    {
        using var ctx = CreateNavContext(BethuyaRoles.Attendee);

        var cut = ctx.RenderComponent<NavMenu>();
        cut.WaitForElement("[data-test='nav-section-my-space']");

        await Assert.That(cut.FindAll("[data-test='nav-section-organizer']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='nav-imports-link']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='nav-community-passports-link']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='nav-programs-link']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='nav-path-finder-link']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='nav-passport-link']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='nav-participation-ledger-link']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='nav-section-insights']").Count).IsEqualTo(1);
    }

    private static BunitCtx CreateCommandCenterContext()
    {
        var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddSingleton(EventPressureClock);
        ctx.Services.AddSingleton<ICommunityCommandCenterService, DeterministicCommunityCommandCenterService>();
        ctx.Services.AddSingleton<ICommunityParticipationService, DeterministicCommunityParticipationService>();
        ctx.Services.AddSingleton<ICommandCenterAudienceResolver, ClaimsCommandCenterAudienceResolver>();
        return ctx;
    }

    private static BunitCtx CreateNavContext(string role)
    {
        var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddAuthorizationCore();
        var auth = ctx.AddTestAuthorization();
        auth.SetAuthorized("Tester");
        auth.SetRoles(role);
        return ctx;
    }

    private static IRenderedComponent<CascadingValue<Task<AuthenticationState>>> RenderHome(
        BunitCtx ctx,
        ClaimsPrincipal principal) =>
        ctx.RenderComponent<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(component => component.Value, Task.FromResult(new AuthenticationState(principal)))
            .AddChildContent<Home>());

    private static ClaimsPrincipal CreatePrincipal(
        string subject,
        string name,
        string role,
        string authenticationType = "Development")
    {
        List<Claim> claims =
        [
            new("sub", subject),
            new("name", name),
            new("role", role)
        ];
        if (authenticationType == "Development")
        {
            claims.Add(new Claim(
                "bethuya:development-persona",
                name,
                ClaimValueTypes.String,
                "Bethuya.Development"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType, "name", "role"));
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
