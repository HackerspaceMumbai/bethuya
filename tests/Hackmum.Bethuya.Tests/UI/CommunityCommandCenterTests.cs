using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Shared.Models.CommandCenter;
using Bethuya.Hybrid.Shared.Models.Participation;
using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
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

    [Test]
    public async Task DeterministicProvider_EventPressure_SelectsExplainableEventMode()
    {
        var service = new DeterministicCommunityCommandCenterService(EventPressureClock);

        var result = await service.GetAsync(CommunityRole.EventOrganizer);

        await Assert.That(result.RecommendedMode).IsEqualTo(CommandCenterMode.Event);
        await Assert.That(result.ModeReason).Contains("16 days away");
        await Assert.That(result.AttentionItems[0].Category).IsEqualTo("Event operations");
        await Assert.That(result.Touchpoints[0].DateLabel).IsEqualTo("17 Oct");
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
        await Assert.That(strategic.ModeQuestion).IsEqualTo("How is the community evolving?");
        await Assert.That(strategic.Insight.Headline).Contains("Community");
        await Assert.That(strategic.PeopleToWatch[0].JourneyTo).IsEqualTo("Organizer candidate");

        await Assert.That(eventOperations.Snapshot[0].Label).IsEqualTo("Event readiness");
        await Assert.That(eventOperations.ModeQuestion).IsEqualTo("Can this event succeed?");
        await Assert.That(eventOperations.Insight.Headline).Contains("execution");
        await Assert.That(eventOperations.PeopleToWatch[0].JourneyTo).IsEqualTo("Event lead");
        await Assert.That(eventOperations.Reviews[0].Label).IsEqualTo("Waitlist decisions");
        await Assert.That(eventOperations.Touchpoints[0].ReadinessLabel).IsEqualTo("82% ready");
        await Assert.That(eventOperations.Touchpoints[0].Metrics.Single(metric => metric.Label == "Capacity").Value)
            .IsEqualTo("150 / 160");
        await Assert.That(eventOperations.Touchpoints[0].Metrics.Single(metric => metric.Label == "Waitlist").Value)
            .IsEqualTo("18 waiting");
        await Assert.That(eventOperations.Touchpoints[0].Metrics.Single(metric => metric.Label == "Volunteers").Value)
            .IsEqualTo("12 / 14");
        await Assert.That(eventOperations.Touchpoints[0].RiskLabel).IsEqualTo("Volunteer gap");
    }

    [Test]
    public async Task DeterministicProvider_TouchpointsSpanMoreThanEvents()
    {
        var service = new DeterministicCommunityCommandCenterService(EventPressureClock);

        var result = await service.GetAsync(CommunityRole.EventOrganizer);

        await Assert.That(result.Touchpoints.Any(touchpoint => touchpoint.Kind != TouchpointKind.Event)).IsTrue();
        var mentorship = result.Touchpoints.Single(touchpoint => touchpoint.Kind == TouchpointKind.MentorshipSession);
        await Assert.That(mentorship.Metrics.Select(metric => metric.Label))
            .IsEquivalentTo(["Pairings", "Mentees waiting", "Mentors"]);
        await Assert.That(mentorship.Metrics.Single(metric => metric.Label == "Pairings").Value)
            .IsEqualTo("6 / 8");
        await Assert.That(mentorship.Metrics.Single(metric => metric.Label == "Mentees waiting").Value)
            .IsEqualTo("2");
        await Assert.That(mentorship.Metrics.Single(metric => metric.Label == "Mentors").Value)
            .IsEqualTo("6 / 8");
        await Assert.That(mentorship.Metrics.Any(metric => metric.Label == "Volunteers")).IsFalse();
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
    public async Task DeterministicProvider_PeopleToWatchStayAwarenessOnly()
    {
        var service = new DeterministicCommunityCommandCenterService(EventPressureClock);

        var result = await service.GetAsync(CommunityRole.CommunityAdministrator);

        await Assert.That(result.PeopleToWatch).IsNotEmpty();
        await Assert.That(result.PeopleToWatch.All(person => !string.IsNullOrWhiteSpace(person.WatchReason))).IsTrue();
    }

    [Test]
    [Arguments(CommunityRole.CommunityAdministrator, "Retention risk")]
    [Arguments(CommunityRole.EventOrganizer, "Capacity risk")]
    [Arguments(CommunityRole.VolunteerLead, "Coverage gap")]
    [Arguments(CommunityRole.MentorshipLead, "Mentor supply")]
    public async Task DeterministicProvider_EmphasisesOperatingRole(CommunityRole role, string expectedCategory)
    {
        var service = new DeterministicCommunityCommandCenterService(
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero)));

        var result = await service.GetAsync(role, CommandCenterMode.Strategic);

        await Assert.That(result.AttentionItems[0].Category).IsEqualTo(expectedCategory);
    }

    [Test]
    [Arguments("dev-persona-priya", "Priya", CommunityRole.VolunteerLead, "Volunteer lead", "Coverage gap")]
    [Arguments("dev-persona-rohan", "Rohan", CommunityRole.EventOrganizer, "Event organizer", "Capacity risk")]
    [Arguments("dev-persona-maya", "Maya", CommunityRole.MentorshipLead, "Mentorship lead", "Mentor supply")]
    [Arguments("dev-persona-vikram", "Vikram", CommunityRole.CommunityAdministrator, "Community administrator", "Retention risk")]
    public async Task Home_OperatingPersonasShareOneCommandCenterWithRoleAwareContent(
        string subject,
        string name,
        CommunityRole expectedRole,
        string expectedLabel,
        string expectedEmphasis)
    {
        using var ctx = CreateCommandCenterContext();
        var principal = CreatePrincipal(subject, name, BethuyaRoles.Attendee);

        var cut = RenderHome(ctx, principal);
        cut.WaitForElement("[data-test='command-center-audience']");
        await cut.Find("[data-test='mode-strategic'] button").ClickAsync(new());
        cut.WaitForElement("[data-test='mode-layout-strategic']");

        await Assert.That(cut.Find("[data-test='command-center-audience']").GetAttribute("data-audience"))
            .IsEqualTo(expectedRole.ToString());
        await Assert.That(cut.Find("[data-test='command-center-audience']").TextContent).Contains(expectedLabel);
        await Assert.That(cut.Find("[data-test='attention-queue']").TextContent).Contains(expectedEmphasis);

        // One shared command center: every operating persona renders the identical module set.
        await Assert.That(cut.FindAll("[data-test='community-snapshot']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='weekly-insight']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='people-to-watch']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='human-review-queue']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='upcoming-touchpoints']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test='quick-actions']").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("[data-test^='role-']").Count).IsEqualTo(0);
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

        // Participation never surfaces operations modules.
        await Assert.That(cut.FindAll("[data-test='community-command-center']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='community-snapshot']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='attention-queue']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='human-review-queue']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='pending-approvals']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='upcoming-deadlines']").Count).IsEqualTo(0);
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

        await Assert.That(cut.FindAll("[data-test='community-snapshot']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='attention-queue']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='quick-navigation']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Home_WithoutAuthenticationCascade_ShowsSignInRequiredState()
    {
        using var ctx = CreateCommandCenterContext();

        var cut = ctx.RenderComponent<Home>();
        cut.WaitForElement("[data-test='command-center-auth-required']");

        await Assert.That(cut.FindAll("[data-test='loading-state']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='community-snapshot']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Home_AuthenticationStateBecomesAnonymous_ClearsExistingProjection()
    {
        using var ctx = CreateCommandCenterContext();
        var cut = RenderHome(ctx, CreatePrincipal("dev-persona-vikram", "Vikram", BethuyaRoles.Admin));
        cut.WaitForElement("[data-test='community-snapshot']");

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Value, Task.FromResult(new AuthenticationState(new ClaimsPrincipal())))
            .AddChildContent<Home>());
        cut.WaitForElement("[data-test='command-center-auth-required']");

        await Assert.That(cut.FindAll("[data-test='community-snapshot']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='attention-queue']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='command-center-audience']").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Home_AuthenticationStatePending_ClearsExistingProjectionBeforeResolution()
    {
        using var ctx = CreateCommandCenterContext();
        var cut = RenderHome(ctx, CreatePrincipal("dev-persona-vikram", "Vikram", BethuyaRoles.Admin));
        cut.WaitForElement("[data-test='community-snapshot']");
        var pendingState = new TaskCompletionSource<AuthenticationState>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Value, pendingState.Task)
            .AddChildContent<Home>());

        await Assert.That(cut.FindAll("[data-test='community-snapshot']").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[data-test='attention-queue']").Count).IsEqualTo(0);
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
        cut.WaitForElement("[data-test='community-snapshot']");

        delayedParticipation.SetResult(staleParticipation);
        cut.WaitForAssertion(() =>
        {
            if (cut.FindAll("[data-test='community-participation']").Count != 0)
            {
                throw new InvalidOperationException("The stale participation load replaced the current operations surface.");
            }
        });
        await Assert.That(cut.FindAll("[data-test='community-snapshot']").Count).IsEqualTo(1);
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
    public async Task Home_ModeSwitchReordersPrimaryWork()
    {
        using var ctx = CreateCommandCenterContext();
        var cut = RenderHome(ctx, CreatePrincipal("dev-persona-vikram", "Vikram", BethuyaRoles.Admin));
        cut.WaitForElement("[data-test='mode-layout-event']");

        AssertAppearsBefore(cut.Markup, "attention-queue", "pending-event-approvals");
        AssertAppearsBefore(cut.Markup, "pending-event-approvals", "upcoming-touchpoints");
        AssertAppearsBefore(cut.Markup, "upcoming-touchpoints", "weekly-insight");
        await Assert.That(cut.Find("[data-test='upcoming-touchpoints']").GetAttribute("data-expanded"))
            .IsEqualTo("true");
        await Assert.That(cut.FindAll("[data-test='pending-approvals']").Count).IsEqualTo(0);

        await cut.Find("[data-test='mode-strategic'] button").ClickAsync(new());
        cut.WaitForElement("[data-test='mode-layout-strategic']");

        AssertAppearsBefore(cut.Markup, "weekly-insight", "people-to-watch");
        AssertAppearsBefore(cut.Markup, "people-to-watch", "attention-queue");
        await Assert.That(cut.Find("[data-test='upcoming-touchpoints']").GetAttribute("data-expanded"))
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
