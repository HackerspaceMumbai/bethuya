using Bethuya.Hybrid.Shared.Layout;
using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Reflection;

using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

public class OnboardingNavigationRenderTests
{
    [Test]
    public async Task Home_IncompleteMandatoryProfile_RedirectsToMandatoryRegistration()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var eventApi = Substitute.For<IEventApi>();
        eventApi.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<EventDto>()));

        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(false, false, false, null, null)));

        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(profileApi);
        ctx.Services.AddSingleton<ICurrentUserService>(new StubCurrentUserService(isAuthenticated: true));

        var cut = ctx.RenderComponent<Home>();
        await Assert.That(cut.Markup).Contains("Finish setup before you explore Bethuya");

        var navigation = ctx.Services.GetRequiredService<NavigationManager>();
        cut.WaitForState(() => navigation.Uri.EndsWith("/registration/mandatory", StringComparison.Ordinal), TimeSpan.FromSeconds(5));

        await Assert.That(navigation.Uri).EndsWith("/registration/mandatory");
        await eventApi.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Home_MandatoryCompleteButSocialIncomplete_RedirectsToSocialRegistration()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var eventApi = Substitute.For<IEventApi>();
        eventApi.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<EventDto>()));

        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));

        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(profileApi);
        ctx.Services.AddSingleton<ICurrentUserService>(new StubCurrentUserService(isAuthenticated: true));

        var cut = ctx.RenderComponent<Home>();
        var navigation = ctx.Services.GetRequiredService<NavigationManager>();

        cut.WaitForState(() => navigation.Uri.EndsWith("/registration/social", StringComparison.Ordinal), TimeSpan.FromSeconds(5));

        await Assert.That(cut.Markup).Contains("Verify your GitHub and LinkedIn accounts.");
        await Assert.That(navigation.Uri).EndsWith("/registration/social");
        await eventApi.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProfileEntry_CompletedOnboarding_RedirectsToMandatoryEditFlow()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<Profile>();
        var navigation = ctx.Services.GetRequiredService<NavigationManager>();

        cut.WaitForState(() => navigation.Uri.EndsWith("/registration/mandatory", StringComparison.Ordinal), TimeSpan.FromSeconds(5));

        await Assert.That(cut.Markup).Contains("Opening your saved profile");
        await Assert.That(navigation.Uri).EndsWith("/registration/mandatory");
    }

    [Test]
    public async Task ProfileEntry_SocialIncompleteUser_RedirectsToSocialStep()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<Profile>();
        var navigation = ctx.Services.GetRequiredService<NavigationManager>();

        cut.WaitForState(() => navigation.Uri.EndsWith("/registration/social", StringComparison.Ordinal), TimeSpan.FromSeconds(5));

        await Assert.That(navigation.Uri).EndsWith("/registration/social");
    }

    [Test]
    public async Task NavMenu_AttendeeRole_HidesOrganizerTools()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Attendee").SetRoles(BethuyaRoles.Attendee);

        var cut = ctx.RenderComponent<NavMenu>();

        await Assert.That(cut.Markup).DoesNotContain("Organizer Tools");
        await Assert.That(cut.Markup).DoesNotContain("Agent Workflows");
        await Assert.That(cut.Markup).DoesNotContain("Attendee Curation");
    }

    [Test]
    public async Task NavMenu_AnonymousUser_HidesOrganizerTools()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetNotAuthorized();

        var cut = ctx.RenderComponent<NavMenu>();

        await Assert.That(cut.Markup).DoesNotContain("Organizer Tools");
        await Assert.That(cut.Markup).DoesNotContain("Agent Workflows");
        await Assert.That(cut.Markup).DoesNotContain("Attendee Curation");
    }

    [Test]
    public async Task NavMenu_OrganizerRole_ShowsOrganizerTools()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Organizer").SetRoles(BethuyaRoles.Organizer);

        var cut = ctx.RenderComponent<NavMenu>();

        await Assert.That(cut.Markup).Contains("Organizer Tools");
        await Assert.That(cut.Markup).Contains("Agent Workflows");
        await Assert.That(cut.Markup).DoesNotContain("Attendee Curation");
    }

    [Test]
    public async Task NavMenu_CuratorRole_ShowsCurationWithoutAgentWorkflows()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Curator").SetRoles(BethuyaRoles.Curator);

        var cut = ctx.RenderComponent<NavMenu>();

        await Assert.That(cut.Markup).Contains("Organizer Tools");
        await Assert.That(cut.Markup).Contains("Attendee Curation");
        await Assert.That(cut.Markup).DoesNotContain("Agent Workflows");
    }

    [Test]
    public async Task NavMenu_ProfileLink_TargetsProfileEntryRoute()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Attendee").SetRoles(BethuyaRoles.Attendee);

        var cut = ctx.RenderComponent<NavMenu>();
        var profileLink = cut.Find("[data-test='nav-profile-link']");

        await Assert.That(profileLink.GetAttribute("href")).IsEqualTo("profile");
    }

    [Test]
    public async Task OnboardingLayout_UsesFocusedShellWithoutSidebarNavigation()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User").SetRoles(BethuyaRoles.Admin, BethuyaRoles.Organizer, BethuyaRoles.Curator);

        var cut = ctx.RenderComponent<OnboardingLayout>(parameters => parameters
            .Add(p => p.Body, builder => builder.AddMarkupContent(0, "<div>Onboarding body</div>")));

        await Assert.That(cut.Markup).Contains("onboarding-shell");
        await Assert.That(cut.Markup).Contains("Secure profile setup");
        await Assert.That(cut.Markup).Contains("Bethuya onboarding");
        await Assert.That(cut.Markup).DoesNotContain("Dashboard");
        await Assert.That(cut.Markup).DoesNotContain("Organizer Tools");
    }

    [Test]
    public async Task OnboardingLayout_MandatoryRoute_HidesOrganizerNavigationAndKeepsPrimaryAction()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User").SetRoles(BethuyaRoles.Admin, BethuyaRoles.Organizer);
        ctx.Services.AddSingleton(Substitute.For<IProfileApi>());
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("https://localhost/registration/mandatory");

        var cut = RenderOnboardingLayoutWithBody<NewUserProfile>(ctx);
        var primaryAction = cut.Find("[data-test='save-profile-btn']");

        await Assert.That(cut.Markup).Contains("onboarding-layout");
        await Assert.That(cut.Markup).Contains("Bethuya onboarding");
        await Assert.That(cut.Markup).Contains("Secure profile setup");
        await Assert.That(cut.Markup).DoesNotContain("Dashboard");
        await Assert.That(cut.Markup).DoesNotContain("Organizer Tools");
        await Assert.That(cut.Markup).DoesNotContain("Agent Workflows");
        await Assert.That(cut.Markup).DoesNotContain("Attendee Curation");
        await Assert.That(primaryAction.GetAttribute("type")).IsEqualTo("submit");
        await Assert.That(primaryAction.TextContent).Contains("Save & Continue");
        await Assert.That(primaryAction.HasAttribute("disabled")).IsFalse();
    }

    [Test]
    public async Task OnboardingLayout_AideRoute_HidesOrganizerNavigationAndKeepsPrimaryAction()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User").SetRoles(BethuyaRoles.Admin, BethuyaRoles.Organizer);
        ctx.Services.AddSingleton(Substitute.For<IProfileApi>());
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("https://localhost/registration/aide");

        var cut = RenderOnboardingLayoutWithBody<AideProfile>(ctx);
        var primaryAction = cut.Find("[data-test='save-aide-btn']");

        await Assert.That(cut.Markup).Contains("onboarding-layout");
        await Assert.That(cut.Markup).Contains("Bethuya onboarding");
        await Assert.That(cut.Markup).Contains("Secure profile setup");
        await Assert.That(cut.Markup).DoesNotContain("Dashboard");
        await Assert.That(cut.Markup).DoesNotContain("Organizer Tools");
        await Assert.That(cut.Markup).DoesNotContain("Agent Workflows");
        await Assert.That(cut.Markup).DoesNotContain("Attendee Curation");
        await Assert.That(primaryAction.GetAttribute("type")).IsEqualTo("submit");
        await Assert.That(primaryAction.TextContent).Contains("Save & Finish");
        await Assert.That(primaryAction.HasAttribute("disabled")).IsFalse();
    }

    [Test]
    public async Task OnboardingLayout_SocialRoute_HidesOrganizerNavigationAndKeepsPrimaryAction()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User").SetRoles(BethuyaRoles.Admin, BethuyaRoles.Organizer);
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto(null, false, false, null, null, null, null)));
        ctx.Services.AddSingleton(profileApi);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("https://localhost/registration/social");

        var cut = RenderOnboardingLayoutWithBody<SocialProfileConnections>(ctx);
        var primaryAction = cut.Find("[data-test='save-social-btn']");

        await Assert.That(cut.Markup).Contains("onboarding-layout");
        await Assert.That(cut.Markup).Contains("Bethuya onboarding");
        await Assert.That(cut.Markup).Contains("Secure profile setup");
        await Assert.That(cut.Markup).DoesNotContain("Dashboard");
        await Assert.That(cut.Markup).DoesNotContain("Organizer Tools");
        await Assert.That(cut.Markup).DoesNotContain("Agent Workflows");
        await Assert.That(cut.Markup).DoesNotContain("Attendee Curation");
        await Assert.That(primaryAction.GetAttribute("type")).IsEqualTo("button");
        await Assert.That(primaryAction.TextContent).Contains("Save & Continue");
    }

    [Test]
    public async Task NewUserProfile_ValidSubmit_SavesMandatoryProfileAndNavigatesToSocial()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.SaveMandatoryProfileAsync(Arg.Any<SaveMandatoryProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<NewUserProfile>();

        SetModelProperty(cut.Instance, "_model", "FirstName", "Dev");
        SetModelProperty(cut.Instance, "_model", "LastName", "User");
        SetModelProperty(cut.Instance, "_model", "Email", "dev@bethuya.local");
        SetModelProperty(cut.Instance, "_model", "GovernmentPhotoIdType", "Passport");
        SetModelProperty(cut.Instance, "_model", "GovernmentIdLastFour", "1234");
        SetModelProperty(cut.Instance, "_model", "OccupationStatus", "Employee");
        SetModelProperty(cut.Instance, "_model", "CompanyName", "GitHub");
        cut.Render();

        await InvokeAsync(cut.Instance, "HandleValidSubmit");

        await profileApi.Received(1).SaveMandatoryProfileAsync(
            Arg.Is<SaveMandatoryProfileDto>(request =>
                request.FirstName == "Dev" &&
                request.LastName == "User" &&
                request.Email == "dev@bethuya.local" &&
                request.GovernmentPhotoIdType == "Passport" &&
                request.GovernmentIdLastFour == "1234" &&
                request.OccupationStatus == "Employee" &&
                request.CompanyName == "GitHub" &&
                request.EducationInstitute == null &&
                request.City == null &&
                request.Country == null),
            Arg.Any<CancellationToken>());

        var navigation = ctx.Services.GetRequiredService<NavigationManager>();
        cut.WaitForState(() => navigation.Uri.EndsWith("/registration/social", StringComparison.Ordinal), TimeSpan.FromSeconds(5));
        await Assert.That(navigation.Uri).EndsWith("/registration/social");
    }

    [Test]
    public async Task NewUserProfile_LoadingSavedData_DisablesSubmitUntilHydrated()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileSource = new TaskCompletionSource<MandatoryProfileDto>();
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetMandatoryProfileAsync(Arg.Any<CancellationToken>())
            .Returns(_ => profileSource.Task);
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<NewUserProfile>();
        var saveButton = cut.Find("[data-test='save-profile-btn']");

        await Assert.That(cut.Markup).Contains("Loading your saved profile details…");
        await Assert.That(saveButton.HasAttribute("disabled")).IsTrue();

        profileSource.SetResult(new MandatoryProfileDto(
            "Dev",
            "User",
            "dev@bethuya.local",
            "+91 99999 99999",
            "Passport",
            "1234",
            "Employee",
            "GitHub",
            null,
            "Mumbai",
            "Maharashtra",
            "400001",
            "India"));

        cut.WaitForAssertion(() =>
        {
            var hydratedButton = cut.Find("[data-test='save-profile-btn']");
            if (hydratedButton.HasAttribute("disabled"))
            {
                throw new InvalidOperationException("Expected the save action to re-enable after hydration.");
            }

            if (!string.Equals(GetModelProperty(cut.Instance, "_model", "FirstName"), "Dev", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the saved first name to hydrate.");
            }

            if (!string.Equals(GetModelProperty(cut.Instance, "_model", "CompanyName"), "GitHub", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the employee company name to hydrate.");
            }
        });
    }

    [Test]
    public async Task NewUserProfile_PrimaryAction_IsStyledAndProminent()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        ctx.Services.AddSingleton(Substitute.For<IProfileApi>());

        var cut = ctx.RenderComponent<NewUserProfile>();

        await Assert.That(cut.Markup).Contains("Step 1 of 3");
        await Assert.That(cut.Markup).Contains("profile-primary-button");
        await Assert.That(cut.Markup).Contains("You're almost done.");
        await Assert.That(cut.Markup).Contains("What happens next");
        await Assert.That(cut.Markup).Contains("Government-approved photo ID");
        await Assert.That(cut.Markup).Contains("government-issued photo ID that also serves as address proof");
        await Assert.That(cut.Markup).Contains("Enter the same location that appears on your selected government-issued photo ID address proof.");
        await Assert.That(cut.Markup).Contains("Verify your GitHub and LinkedIn accounts on the next step.");
        await Assert.That(cut.Markup).DoesNotContain("PAN Card");
    }

    [Test]
    public async Task SocialProfileConnections_QueryParameters_ShowConnectedAccounts()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Student", false, true, null, null, null, null)));
        ctx.Services.AddSingleton(profileApi);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("https://localhost/registration/social?linkedinMemberId=yrZCpj2Z12&linkedinProfileUrl=https%3A%2F%2Fwww.linkedin.com%2Fin%2Fdev-user&githubLogin=dev-user&githubProfileUrl=https%3A%2F%2Fgithub.com%2Fdev-user");

        var cut = ctx.RenderComponent<SocialProfileConnections>();

        await Assert.That(cut.Markup).Contains("Connected as");
        await Assert.That(cut.Markup).Contains("yrZCpj2Z12");
        await Assert.That(cut.Markup).Contains("dev-user");
        await Assert.That(cut.Markup).Contains("Reconnect LinkedIn");
        await Assert.That(cut.Markup).Contains("Reconnect GitHub");
        await Assert.That(cut.Markup).Contains("GitHub is required for students.");

        var linkedInUrlInput = cut.Find("[data-test='linkedin-profile-url-field'] input");
        await Assert.That(linkedInUrlInput.GetAttribute("value")).IsEqualTo("https://www.linkedin.com/in/dev-user");
        await Assert.That(linkedInUrlInput.HasAttribute("disabled")).IsTrue();
    }

    [Test]
    public async Task SocialProfileConnections_SocialError_ShowsProviderSpecificMessageBesideSocialCards()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Employee", true, false, null, null, null, null)));
        ctx.Services.AddSingleton(profileApi);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("https://localhost/registration/social?socialError=social-provider-not-configured&socialProvider=github");

        var cut = ctx.RenderComponent<SocialProfileConnections>();

        await Assert.That(cut.Markup).Contains("Social connection needs attention");
        await Assert.That(cut.Markup).Contains("GitHub is not configured for local onboarding yet.");
        await Assert.That(cut.Markup).Contains("social-connection-error");
        await Assert.That(cut.Markup).Contains("social-connection-action");
    }

    [Test]
    public async Task SocialProfileConnections_DisconnectedState_KeepsLinkedInFirst_DisablesLinkedInConnectUntilUrlExists_AndSignalsGitHubBelow()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Student", false, true, null, null, null, null)));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();
        var stack = cut.Find("[data-test='social-connection-stack']");
        var cards = cut.FindAll(".social-connection-card");
        var linkedInCard = cards[0];
        var gitHubCard = cards[1];
        var linkedInUrlInput = cut.Find("[data-test='linkedin-profile-url-field'] input");
        var linkedInButton = cut.Find("[data-test='connect-linkedin-btn'] button");

        await Assert.That(cards.Count).IsEqualTo(2);
        await Assert.That(cut.FindAll(".social-connection-card input[type='url']").Count).IsEqualTo(1);
        await Assert.That(cut.Markup).Contains("social-connection-meta--placeholder");
        await Assert.That(cut.Markup).Contains("Enter the exact public LinkedIn profile URL carefully before you connect.");
        await Assert.That(linkedInCard.TextContent).Contains("LinkedIn");
        await Assert.That(linkedInCard.TextContent).Contains("Public LinkedIn profile URL");
        await Assert.That(linkedInCard.TextContent).Contains("Connect LinkedIn");
        await Assert.That(gitHubCard.TextContent).Contains("GitHub");
        await Assert.That(gitHubCard.TextContent).Contains("Verified GitHub profile details appear here after you connect.");
        await Assert.That(gitHubCard.TextContent).Contains("Connect GitHub");
        await Assert.That(gitHubCard.TextContent).DoesNotContain("Public LinkedIn profile URL");
        await Assert.That(linkedInUrlInput.HasAttribute("disabled")).IsFalse();
        await Assert.That(linkedInButton.HasAttribute("disabled")).IsTrue();
        await Assert.That(stack.TextContent.Contains("GitHub", StringComparison.Ordinal) &&
                          stack.TextContent.Contains("below", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task SocialProfileConnections_LinkedInConnect_EnablesAfterNonEmptyUrlIsEntered()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Student", false, true, null, null, "dev-user", "https://github.com/dev-user")));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();
        var linkedInUrlInput = cut.Find("[data-test='linkedin-profile-url-field'] input");
        var linkedInButton = cut.Find("[data-test='connect-linkedin-btn'] button");

        await Assert.That(linkedInButton.HasAttribute("disabled")).IsTrue();

        linkedInUrlInput.Change("   ");
        await Assert.That(cut.Find("[data-test='connect-linkedin-btn'] button").HasAttribute("disabled")).IsTrue();

        linkedInUrlInput.Change("https://www.linkedin.com/in/future-speaker");
        await Assert.That(cut.Find("[data-test='connect-linkedin-btn'] button").HasAttribute("disabled")).IsFalse();
    }

    [Test]
    public async Task SocialProfileConnections_LoadingState_DisablesEditableControlsUntilSavedStateHydrates()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");

        var statusSource = new TaskCompletionSource<ProfileCompletionStatusDto>();
        var socialSource = new TaskCompletionSource<SocialProfileDto>();
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(_ => statusSource.Task);
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(_ => socialSource.Task);
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();
        var linkedInUrlInput = cut.Find("[data-test='linkedin-profile-url-field'] input");
        var linkedInButton = cut.Find("[data-test='connect-linkedin-btn'] button");
        var gitHubButton = cut.Find("[data-test='connect-github-btn'] button");
        var saveButton = cut.Find("[data-test='save-social-btn']");

        await Assert.That(cut.Markup).Contains("Loading your saved LinkedIn status…");
        await Assert.That(cut.Markup).Contains("Loading saved GitHub profile details…");
        await Assert.That(cut.Markup).Contains("Loading your saved social connections before you continue.");
        await Assert.That(linkedInUrlInput.HasAttribute("disabled")).IsTrue();
        await Assert.That(linkedInButton.HasAttribute("disabled")).IsTrue();
        await Assert.That(gitHubButton.HasAttribute("disabled")).IsTrue();
        await Assert.That(saveButton.HasAttribute("disabled")).IsTrue();

        statusSource.SetResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null));
        socialSource.SetResult(new SocialProfileDto("Student", false, true, null, null, "dev-user", "https://github.com/dev-user"));

        cut.WaitForAssertion(() =>
        {
            var hydratedInput = cut.Find("[data-test='linkedin-profile-url-field'] input");
            var hydratedGitHubButton = cut.Find("[data-test='connect-github-btn'] button");
            if (!cut.Markup.Contains("Connected as", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the hydrated UI to show a connected account.");
            }

            if (hydratedInput.HasAttribute("disabled"))
            {
                throw new InvalidOperationException("Expected the LinkedIn URL field to become editable after hydration.");
            }

            if (hydratedGitHubButton.HasAttribute("disabled"))
            {
                throw new InvalidOperationException("Expected the GitHub connect button to re-enable after hydration.");
            }
        });
    }

    [Test]
    public async Task SocialProfileConnections_LoadFailure_KeepsCardsTruthfulAndDisablesActions()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<SocialProfileDto>(new InvalidOperationException("boom")));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();
        var linkedInUrlInput = cut.Find("[data-test='linkedin-profile-url-field'] input");
        var linkedInButton = cut.Find("[data-test='connect-linkedin-btn'] button");
        var gitHubButton = cut.Find("[data-test='connect-github-btn'] button");
        var saveButton = cut.Find("[data-test='save-social-btn']");
        var linkedInStatus = cut.Find("[data-test='linkedin-load-error-status']");
        var loadError = cut.Find("[data-test='social-profile-error']");
        var gitHubMeta = cut.Find("[data-test='github-meta']");

        await Assert.That(loadError.TextContent).Contains("We couldn't load this onboarding step. Refresh the page before continuing.");
        await Assert.That(linkedInStatus.TextContent).Contains("We couldn't load your saved LinkedIn status.");
        await Assert.That(gitHubMeta.TextContent).Contains("Refresh this page to reload your saved GitHub profile details.");
        await Assert.That(linkedInUrlInput.HasAttribute("disabled")).IsTrue();
        await Assert.That(linkedInButton.HasAttribute("disabled")).IsTrue();
        await Assert.That(gitHubButton.HasAttribute("disabled")).IsTrue();
        await Assert.That(saveButton.HasAttribute("disabled")).IsTrue();
        await profileApi.DidNotReceive().SaveSocialProfileAsync(Arg.Any<SaveSocialProfileDto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SocialProfileConnections_LinkedInConnectedWithSavedUrl_LocksFieldButKeepsReconnectAvailable()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Employee", true, false, "yrZCpj2Z12", "https://www.linkedin.com/in/dev-user", "dev-user", "https://github.com/dev-user")));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();

        var linkedInUrlInput = cut.Find("[data-test='linkedin-profile-url-field'] input");
        var linkedInButton = cut.Find("[data-test='connect-linkedin-btn'] button");

        await Assert.That(linkedInUrlInput.HasAttribute("disabled")).IsTrue();
        await Assert.That(linkedInUrlInput.GetAttribute("value") ?? string.Empty).IsEqualTo("https://www.linkedin.com/in/dev-user");
        await Assert.That(linkedInButton.HasAttribute("disabled")).IsFalse();
        await Assert.That(linkedInButton.TextContent).Contains("Reconnect LinkedIn");
        await Assert.That(cut.Markup).Contains("Locked after LinkedIn verification on this step.");
    }

    [Test]
    public async Task SocialProfileConnections_LinkedInConnectedWhileGitHubPending_KeepsLinkedInReconnectSeparateFromGitHubCta()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Employee", true, false, "yrZCpj2Z12", "https://www.linkedin.com/in/dev-user", null, null)));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();
        var cards = cut.FindAll(".social-connection-card");
        var linkedInCard = cards[0];
        var gitHubCard = cards[1];

        await Assert.That(cards.Count).IsEqualTo(2);
        await Assert.That(cut.FindAll(".social-connection-details").Count).IsEqualTo(2);
        await Assert.That(cut.FindAll(".social-connection-action").Count).IsEqualTo(2);
        await Assert.That(cut.FindAll(".social-connection-feedback").Count).IsEqualTo(2);
        await Assert.That(linkedInCard.TextContent).Contains("Reconnect LinkedIn");
        await Assert.That(linkedInCard.TextContent).Contains("Public LinkedIn profile URL");
        await Assert.That(linkedInCard.TextContent).Contains("Locked after LinkedIn verification on this step.");
        await Assert.That(gitHubCard.TextContent).Contains("GitHub");
        await Assert.That(gitHubCard.TextContent).Contains("Not connected yet.");
        await Assert.That(gitHubCard.TextContent).Contains("Verified GitHub profile details appear here after you connect.");
        await Assert.That(gitHubCard.TextContent).Contains("Connect GitHub");
        await Assert.That(gitHubCard.TextContent).DoesNotContain("Public LinkedIn profile URL");
    }

    [Test]
    public async Task SocialProfileConnections_LinkedInScopeError_ShowsActionableProviderMessage()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Employee", true, false, null, null, null, null)));
        ctx.Services.AddSingleton(profileApi);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("https://localhost/registration/social?socialError=social-provider-scope-not-authorized&socialProvider=linkedin");

        var cut = ctx.RenderComponent<SocialProfileConnections>();

        await Assert.That(cut.Markup).Contains("LinkedIn needs attention");
        await Assert.That(cut.Markup).Contains("Enable the \"Sign in with LinkedIn using OpenID Connect\" product");
        await Assert.That(cut.Markup).Contains("https://localhost:7400/oauth/linkedin/callback");
    }

    [Test]
    public async Task SocialProfileConnections_EmployeeWithTypedLinkedInUrlButNoVerifiedMemberId_StillShowsRequiredError()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Employee", true, false, null, null, "dev-user", "https://github.com/dev-user")));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();
        SetModelProperty(cut.Instance, "_model", "LinkedInProfileUrl", "https://www.linkedin.com/in/dev-user/");

        await InvokeAsync(cut.Instance, "HandleContinue");

        await Assert.That(cut.Markup).Contains("LinkedIn is required for full-time employed applicants.");
        await profileApi.DidNotReceive().SaveSocialProfileAsync(Arg.Any<SaveSocialProfileDto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SocialProfileConnections_StudentWithGitHubOnly_CanContinueAndSave()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Student", false, true, null, null, "dev-user", "https://github.com/dev-user")));
        profileApi.SaveSocialProfileAsync(Arg.Any<SaveSocialProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, true, false, DateTimeOffset.UtcNow, null)));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();

        await InvokeAsync(cut.Instance, "HandleContinue");

        await profileApi.Received(1).SaveSocialProfileAsync(
            Arg.Is<SaveSocialProfileDto>(request =>
                request.LinkedInMemberId == null &&
                request.LinkedInProfileUrl == null &&
                request.GitHubLogin == "dev-user" &&
                request.GitHubProfileUrl == "https://github.com/dev-user"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SocialProfileConnections_OptionalLinkedInUrl_RemainsEditableAndSavesTypedValue()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Student", false, true, null, null, "dev-user", "https://github.com/dev-user")));
        profileApi.SaveSocialProfileAsync(Arg.Any<SaveSocialProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, true, false, DateTimeOffset.UtcNow, null)));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();
        var linkedInUrlInput = cut.Find("[data-test='linkedin-profile-url-field'] input");
        linkedInUrlInput.Change("https://www.linkedin.com/in/future-speaker");

        await InvokeAsync(cut.Instance, "HandleContinue");

        await profileApi.Received(1).SaveSocialProfileAsync(
            Arg.Is<SaveSocialProfileDto>(request =>
                request.LinkedInMemberId == null &&
                request.LinkedInProfileUrl == "https://www.linkedin.com/in/future-speaker" &&
                request.GitHubLogin == "dev-user" &&
                request.GitHubProfileUrl == "https://github.com/dev-user"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SocialProfileConnections_LinkedInUrl_IsPreservedWhenStartingSocialConnect()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Student", false, true, null, null, null, null)));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();
        var linkedInUrlInput = cut.Find("[data-test='linkedin-profile-url-field'] input");
        linkedInUrlInput.Change("https://www.linkedin.com/in/future-speaker");

        cut.Find("[data-test='connect-github-btn'] button").Click();

        var navigation = ctx.Services.GetRequiredService<NavigationManager>();
        await Assert.That(navigation.Uri).Contains("returnUrl=%2Fregistration%2Fsocial%3FlinkedinProfileUrl%3Dhttps%253A%252F%252Fwww.linkedin.com%252Fin%252Ffuture-speaker");
    }

    [Test]
    public async Task SocialProfileConnections_LinkedInConnect_StartsOnlyAfterUrlIsPresent()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Employee", true, false, null, null, null, null)));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();
        var navigation = ctx.Services.GetRequiredService<NavigationManager>();
        var startingUri = navigation.Uri;
        var linkedInUrlInput = cut.Find("[data-test='linkedin-profile-url-field'] input");

        linkedInUrlInput.Change("https://www.linkedin.com/in/dev-user");
        cut.Find("[data-test='connect-linkedin-btn'] button").Click();

        await Assert.That(navigation.Uri).IsNotEqualTo(startingUri);
        await Assert.That(navigation.Uri).Contains("/authentication/social/linkedin/start");
        await Assert.That(navigation.Uri).Contains("linkedinProfileUrl%3Dhttps%253A%252F%252Fwww.linkedin.com%252Fin%252Fdev-user");
    }

    [Test]
    public async Task SocialProfileConnections_EmployeeWithoutLinkedIn_ShowsRequiredError()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Employee", true, false, null, null, "dev-user", "https://github.com/dev-user")));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();

        await InvokeAsync(cut.Instance, "HandleContinue");

        await Assert.That(cut.Markup).Contains("LinkedIn is required for full-time employed applicants.");
        await profileApi.DidNotReceive().SaveSocialProfileAsync(Arg.Any<SaveSocialProfileDto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SocialProfileConnections_ValidSubmit_SavesVerifiedProfilesAndNavigatesToAide()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, false, false, DateTimeOffset.UtcNow, null)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Employee", true, false, "yrZCpj2Z12", "https://www.linkedin.com/in/dev-user", "dev-user", "https://github.com/dev-user")));
        profileApi.SaveSocialProfileAsync(Arg.Any<SaveSocialProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, true, false, DateTimeOffset.UtcNow, null)));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();

        await InvokeAsync(cut.Instance, "HandleContinue");

        await profileApi.Received(1).SaveSocialProfileAsync(
            Arg.Is<SaveSocialProfileDto>(request =>
                request.LinkedInMemberId == "yrZCpj2Z12" &&
                request.LinkedInProfileUrl == "https://www.linkedin.com/in/dev-user" &&
                request.GitHubLogin == "dev-user" &&
                request.GitHubProfileUrl == "https://github.com/dev-user"),
            Arg.Any<CancellationToken>());

        var navigation = ctx.Services.GetRequiredService<NavigationManager>();
        cut.WaitForState(() => navigation.Uri.EndsWith("/registration/aide", StringComparison.Ordinal), TimeSpan.FromSeconds(5));
        await Assert.That(navigation.Uri).EndsWith("/registration/aide");
    }

    [Test]
    public async Task SocialProfileConnections_HydratedSavedState_RemainsStableAcrossRerender()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
        profileApi.GetSocialProfileAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SocialProfileDto("Employee", true, false, "yrZCpj2Z12", "https://www.linkedin.com/in/dev-user", "dev-user", "https://github.com/dev-user")));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<SocialProfileConnections>();
        cut.Render();

        var linkedInUrlInput = cut.Find("[data-test='linkedin-profile-url-field'] input");
        var linkedInButton = cut.Find("[data-test='connect-linkedin-btn'] button");
        var gitHubButton = cut.Find("[data-test='connect-github-btn'] button");

        await Assert.That(cut.Markup).Contains("Connected as");
        await Assert.That(cut.Markup).Contains("yrZCpj2Z12");
        await Assert.That(cut.Markup).Contains("dev-user");
        await Assert.That(linkedInUrlInput.GetAttribute("value")).IsEqualTo("https://www.linkedin.com/in/dev-user");
        await Assert.That(linkedInUrlInput.HasAttribute("disabled")).IsTrue();
        await Assert.That(linkedInButton.TextContent).Contains("Reconnect LinkedIn");
        await Assert.That(gitHubButton.TextContent).Contains("Reconnect GitHub");
        await Assert.That(cut.Markup).DoesNotContain("Loading your saved LinkedIn status");
        await Assert.That(cut.Markup).DoesNotContain("Loading saved GitHub profile details");
    }

    [Test]
    public async Task NewUserProfile_EmployeeStatus_RevealsCompanyField()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        ctx.Services.AddSingleton(Substitute.For<IProfileApi>());

        var cut = ctx.RenderComponent<NewUserProfile>();
        SetModelProperty(cut.Instance, "_model", "OccupationStatus", "Employee");
        cut.Render();

        await Assert.That(cut.Markup).Contains("Name of company");
        await Assert.That(cut.Markup).DoesNotContain("Name of education institute");
    }

    [Test]
    public async Task NewUserProfile_StudentStatus_RevealsEducationInstituteField()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        ctx.Services.AddSingleton(Substitute.For<IProfileApi>());

        var cut = ctx.RenderComponent<NewUserProfile>();
        SetModelProperty(cut.Instance, "_model", "OccupationStatus", "Student");
        cut.Render();

        await Assert.That(cut.Markup).Contains("Name of education institute");
        await Assert.That(cut.Markup).DoesNotContain("Name of company");
    }

    [Test]
    public async Task NewUserProfile_AccessibilityContract_ExposesMainLandmarkHeadingAndPrimaryAction()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        ctx.Services.AddSingleton(Substitute.For<IProfileApi>());

        var cut = ctx.RenderComponent<NewUserProfile>();
        var main = cut.Find("section[aria-label='Complete your profile']");
        var heading = cut.Find("h1");
        var primaryAction = cut.Find("[data-test='save-profile-btn']");

        await Assert.That(main).IsNotNull();
        await Assert.That(heading.TextContent.Trim()).IsEqualTo("Let's set up your Bethuya profile");
        await Assert.That(primaryAction.GetAttribute("type")).IsEqualTo("submit");
        await Assert.That(primaryAction.TextContent).Contains("Save & Continue");
        await Assert.That(primaryAction.HasAttribute("disabled")).IsFalse();
    }

    [Test]
    public async Task NewUserProfile_RehydratedMandatoryModel_RetainsSavedValuesAcrossRerender()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        ctx.Services.AddSingleton(Substitute.For<IProfileApi>());

        var cut = ctx.RenderComponent<NewUserProfile>();
        SetModelProperty(cut.Instance, "_model", "GovernmentPhotoIdType", "Passport");
        SetModelProperty(cut.Instance, "_model", "GovernmentIdLastFour", "1234");
        SetModelProperty(cut.Instance, "_model", "FirstName", "Dev");
        SetModelProperty(cut.Instance, "_model", "LastName", "User");
        SetModelProperty(cut.Instance, "_model", "Email", "dev@bethuya.local");
        SetModelProperty(cut.Instance, "_model", "MobileNumber", "+91 98765 43210");
        SetModelProperty(cut.Instance, "_model", "OccupationStatus", "Employee");
        SetModelProperty(cut.Instance, "_model", "CompanyName", "GitHub");
        SetModelProperty(cut.Instance, "_model", "City", "Mumbai");
        SetModelProperty(cut.Instance, "_model", "State", "Maharashtra");
        SetModelProperty(cut.Instance, "_model", "PostalCode", "400001");
        SetModelProperty(cut.Instance, "_model", "Country", "India");
        cut.Render();
        cut.Render();

        var inputValues = JoinInputValues(cut);

        await Assert.That(cut.Markup).Contains("Name of company");
        await Assert.That(cut.Markup).DoesNotContain("Name of education institute");
        await Assert.That(inputValues).Contains("1234");
        await Assert.That(inputValues).Contains("Dev");
        await Assert.That(inputValues).Contains("User");
        await Assert.That(inputValues).Contains("dev@bethuya.local");
        await Assert.That(inputValues).Contains("+91 98765 43210");
        await Assert.That(inputValues).Contains("GitHub");
        await Assert.That(inputValues).Contains("Mumbai");
        await Assert.That(inputValues).Contains("Maharashtra");
        await Assert.That(inputValues).Contains("400001");
        await Assert.That(inputValues).Contains("India");
    }

    [Test]
    public async Task AideProfile_DisabilitySelection_RevealsDetailsField()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        ctx.Services.AddSingleton(Substitute.For<IProfileApi>());

        var cut = ctx.RenderComponent<AideProfile>();
        SetModelProperty(cut.Instance, "_model", "Disability", "Physical / mobility");
        cut.Render();

        await Assert.That(cut.Markup).Contains("disability-details-wrapper");
    }

    [Test]
    public async Task AideProfile_AccessibilityContract_ExposesOptionalPrimaryActionAndSkipLink()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        ctx.Services.AddSingleton(Substitute.For<IProfileApi>());

        var cut = ctx.RenderComponent<AideProfile>();
        var main = cut.Find("section[aria-label='Diversity and inclusion profile']");
        var primaryAction = cut.Find("[data-test='save-aide-btn']");
        var skipLink = cut.Find("[data-test='skip-aide-link']");

        await Assert.That(main).IsNotNull();
        await Assert.That(primaryAction.GetAttribute("type")).IsEqualTo("submit");
        await Assert.That(primaryAction.TextContent).Contains("Save & Finish");
        await Assert.That(skipLink.GetAttribute("href")).IsEqualTo("/");
        await Assert.That(skipLink.TextContent).Contains("Skip for now");
    }

    [Test]
    public async Task AideProfile_RendersPrivacyReassuranceSidebar()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        ctx.Services.AddSingleton(Substitute.For<IProfileApi>());

        var cut = ctx.RenderComponent<AideProfile>();

        await Assert.That(cut.Markup).Contains("Your privacy, respected");
        await Assert.That(cut.Markup).Contains("Good reasons to answer");
        await Assert.That(cut.Markup).Contains("Save &amp; Finish");
    }

    [Test]
    public async Task AideProfile_RehydratedOptionalModel_RetainsSavedValuesAcrossRerender()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        ctx.Services.AddSingleton(Substitute.For<IProfileApi>());

        var cut = ctx.RenderComponent<AideProfile>();
        SetModelProperty(cut.Instance, "_model", "GenderIdentity", "Prefer to self-describe");
        SetModelProperty(cut.Instance, "_model", "SelfDescribeGender", "Non-binary femme");
        SetModelProperty(cut.Instance, "_model", "Disability", "Physical / mobility");
        SetModelProperty(cut.Instance, "_model", "DisabilityDetails", "Wheelchair ramp access and aisle seat");
        SetModelProperty(cut.Instance, "_model", "DietaryRequirements", "Vegetarian");
        SetModelProperty(cut.Instance, "_model", "Neighborhood", "Andheri");
        SetModelProperty(cut.Instance, "_model", "LanguageProficiency", "English, Hindi");
        SetModelProperty(cut.Instance, "_model", "AdditionalSupport", "Quiet seating");
        cut.Render();
        cut.Render();

        var inputValues = JoinInputValues(cut);
        var textAreaValues = JoinTextareaValues(cut);

        await Assert.That(cut.Markup).Contains("Self-describe your gender");
        await Assert.That(cut.Markup).Contains("disability-details-wrapper");
        await Assert.That(inputValues).Contains("Non-binary femme");
        await Assert.That(inputValues).Contains("Vegetarian");
        await Assert.That(inputValues).Contains("Andheri");
        await Assert.That(inputValues).Contains("English, Hindi");
        await Assert.That(inputValues).Contains("Quiet seating");
        await Assert.That(textAreaValues).Contains("Wheelchair ramp access and aisle seat");
    }

    [Test]
    public async Task AideProfile_EmptySubmit_SavesOptionalProfileAndNavigatesHome()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.SaveAideProfileAsync(Arg.Any<SaveAideProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<AideProfile>();

        await InvokeAsync(cut.Instance, "HandleValidSubmit");

        await profileApi.Received(1).SaveAideProfileAsync(
            Arg.Is<SaveAideProfileDto>(request =>
                request.GenderIdentity == null &&
                request.Disability == null &&
                request.HowDidYouHear == null),
            Arg.Any<CancellationToken>());

        var navigation = ctx.Services.GetRequiredService<NavigationManager>();
        cut.WaitForState(() => navigation.Uri.EndsWith('/'), TimeSpan.FromSeconds(5));
        await Assert.That(navigation.Uri).EndsWith("/");
    }

    [Test]
    public async Task AideProfile_LoadingSavedData_DisablesSubmitUntilHydrated()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Dev User");
        var aideSource = new TaskCompletionSource<AideProfileDto>();
        var profileApi = Substitute.For<IProfileApi>();
        profileApi.GetCompletionStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProfileCompletionStatusDto(true, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
        profileApi.GetAideProfileAsync(Arg.Any<CancellationToken>())
            .Returns(_ => aideSource.Task);
        ctx.Services.AddSingleton(profileApi);

        var cut = ctx.RenderComponent<AideProfile>();
        var saveButton = cut.Find("[data-test='save-aide-btn']");

        await Assert.That(cut.Markup).Contains("Loading your saved accessibility profile…");
        await Assert.That(saveButton.HasAttribute("disabled")).IsTrue();

        aideSource.SetResult(new AideProfileDto(
            "Woman",
            null,
            "25–34",
            null,
            null,
            "Physical / mobility",
            "Step-free access helps.",
            "Vegetarian",
            null,
            "Parent / guardian",
            null,
            null,
            "Bandra",
            "Public transport",
            "Middle class",
            null,
            null,
            "English, Hindi",
            "Bachelor's degree",
            "Previous event",
            "Quiet seating if possible"));

        cut.WaitForAssertion(() =>
        {
            var hydratedButton = cut.Find("[data-test='save-aide-btn']");
            if (hydratedButton.HasAttribute("disabled"))
            {
                throw new InvalidOperationException("Expected the AIDE save action to re-enable after hydration.");
            }

            if (!string.Equals(GetModelProperty(cut.Instance, "_model", "Disability"), "Physical / mobility", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the saved disability selection to hydrate.");
            }

            if (!string.Equals(GetModelProperty(cut.Instance, "_model", "AdditionalSupport"), "Quiet seating if possible", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the saved support notes to hydrate.");
            }
        });
    }

    private static BunitCtx CreateContext()
    {
        var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        return ctx;
    }

    private static void SetModelProperty(object component, string fieldName, string propertyName, string value)
    {
        var field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        var model = field.GetValue(component)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was null.");
        var property = model.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Property '{propertyName}' was not found.");
        property.SetValue(model, value);
    }

    private static string? GetModelProperty(object component, string fieldName, string propertyName)
    {
        var field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        var model = field.GetValue(component)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was null.");
        var property = model.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Property '{propertyName}' was not found.");
        return property.GetValue(model) as string;
    }

    private static async Task InvokeAsync(object component, string methodName)
    {
        var method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        var result = method.Invoke(component, null);

        if (result is Task task)
        {
            await task;
        }
    }

    private static string JoinInputValues(IRenderedFragment cut)
        => string.Join("|", cut.FindAll("input")
            .Select(input => input.GetAttribute("value") ?? string.Empty));

    private static string JoinTextareaValues(IRenderedFragment cut)
        => string.Join("|", cut.FindAll("textarea")
            .Select(textarea => textarea.GetAttribute("value") ?? textarea.TextContent ?? string.Empty));

    private static IRenderedComponent<MainLayout> RenderLayoutWithBody<TBody>(BunitCtx ctx)
        where TBody : IComponent
    {
        RenderFragment body = builder =>
        {
            builder.OpenComponent<TBody>(0);
            builder.CloseComponent();
        };

        return ctx.RenderComponent<MainLayout>(parameters => parameters
            .Add<RenderFragment>(p => p.Body!, body));
    }

    private static IRenderedComponent<OnboardingLayout> RenderOnboardingLayoutWithBody<TBody>(BunitCtx ctx)
        where TBody : IComponent
    {
        RenderFragment body = builder =>
        {
            builder.OpenComponent<TBody>(0);
            builder.CloseComponent();
        };

        return ctx.RenderComponent<OnboardingLayout>(parameters => parameters
            .Add<RenderFragment>(p => p.Body!, body));
    }

    private sealed class StubCurrentUserService(bool isAuthenticated) : ICurrentUserService
    {
        public string? UserId => isAuthenticated ? "dev-user-001" : null;
        public string? Email => isAuthenticated ? "dev@bethuya.local" : null;
        public bool IsAuthenticated => isAuthenticated;

        public bool IsInRole(string role) => false;
    }
}

