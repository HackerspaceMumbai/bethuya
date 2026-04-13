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
        await Assert.That(cut.Markup).Contains("social-connection-details");
        await Assert.That(cut.Markup).Contains("GitHub is required for students.");
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
    public async Task SocialProfileConnections_DisconnectedState_ReservesMetaSpaceForAlignedButtons()
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

        await Assert.That(cut.Markup).Contains("social-connection-meta--placeholder");
        await Assert.That(cut.Markup).Contains("Verified GitHub profile details appear here after you connect.");
        await Assert.That(cut.Markup).Contains("Verified LinkedIn profile details appear here after you connect.");
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
                request.GitHubLogin == "dev-user" &&
                request.GitHubProfileUrl == "https://github.com/dev-user"),
            Arg.Any<CancellationToken>());
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
