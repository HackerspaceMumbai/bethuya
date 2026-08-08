using System.Security.Claims;
using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Web.Auth;
using Bethuya.Hybrid.Web.Components.Dev;
using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using ServiceDefaults.Auth;

// Alias Bunit.TestContext to avoid collision with TUnit's own TestContext type.
using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

/// <summary>
/// Render tests for <see cref="DevPersonaToolbar"/>. Covers the DI-composition visibility gate
/// (requirement #2), the six allowlisted personas (requirement #3), the active identity/roles
/// display (requirement #4), and — implicitly, by successfully rendering every Blazor Blueprint
/// component the toolbar uses (BbButton, BbCard, BbCardHeader, BbCardTitle, BbCardDescription,
/// BbCardContent, BbSeparator) — that no unknown parameter was passed to a BB component, which
/// bUnit surfaces as a runtime <see cref="InvalidOperationException"/> rather than a compile error.
/// </summary>
public sealed class DevPersonaToolbarRenderTests
{
    private static readonly string[] AllPersonaKeys = ["anish", "priya", "rohan", "maya", "farah", "vikram"];

    [Test]
    public async Task Render_WhenFeatureRegistered_ShowsToolbarAndAllSixAllowlistedPersonas()
    {
        using var ctx = CreateAuthorizedContext(BethuyaRoles.Attendee);

        var cut = ctx.RenderComponent<DevPersonaToolbar>();

        await Assert.That(cut.Markup).Contains("data-test=\"dev-persona-toolbar\"");
        await Assert.That(cut.Markup).Contains("data-test=\"persona-selector\"");

        foreach (var key in AllPersonaKeys)
        {
            await Assert.That(cut.Markup).Contains($"data-test=\"persona-{key}\"");
        }

        foreach (var persona in DevelopmentPersonaCatalog.All)
        {
            await Assert.That(cut.Markup).Contains(persona.DisplayName);
        }
    }

    [Test]
    public async Task Render_WhenFeatureRegistered_ShowsActiveIdentityAndSingleRole()
    {
        using var ctx = CreateAuthorizedContext(
            BethuyaRoles.Attendee,
            name: "Farah",
            email: "farah@bethuya.dev",
            sub: "dev-persona-farah");

        var cut = ctx.RenderComponent<DevPersonaToolbar>();

        var activePersona = cut.Find("[data-test='active-persona']");
        await Assert.That(activePersona.TextContent).Contains("Farah");
        await Assert.That(activePersona.TextContent).Contains("farah@bethuya.dev");
        await Assert.That(activePersona.TextContent).Contains("dev-persona-farah");

        var activeRole = cut.Find("[data-test='active-persona-role']");
        await Assert.That(activeRole.TextContent).Contains(BethuyaRoles.Attendee);
        await Assert.That(activeRole.TextContent).DoesNotContain(BethuyaRoles.Admin);
        await Assert.That(activeRole.TextContent).DoesNotContain(BethuyaRoles.Organizer);
        await Assert.That(activeRole.TextContent).DoesNotContain(BethuyaRoles.Curator);
    }

    [Test]
    public async Task Render_WhenFeatureRegistered_ShowsAllEffectiveRoles_ForMultiRolePersona()
    {
        using var ctx = CreateAuthorizedContext(
            BethuyaRoles.Admin,
            BethuyaRoles.Organizer,
            BethuyaRoles.Curator,
            BethuyaRoles.Attendee,
            name: "Vikram",
            email: "vikram@bethuya.dev",
            sub: "dev-persona-vikram");

        var cut = ctx.RenderComponent<DevPersonaToolbar>();

        var activeRole = cut.Find("[data-test='active-persona-role']");
        await Assert.That(activeRole.TextContent).Contains(BethuyaRoles.Admin);
        await Assert.That(activeRole.TextContent).Contains(BethuyaRoles.Organizer);
        await Assert.That(activeRole.TextContent).Contains(BethuyaRoles.Curator);
        await Assert.That(activeRole.TextContent).Contains(BethuyaRoles.Attendee);
    }

    [Test]
    public async Task Render_PersonaSelection_NavigatesToSecurePersonaEndpointWithLocalReturnUrlAndForceLoad()
    {
        // A plain <a href> would be intercepted by Blazor's enhanced navigation as a same-origin
        // SPA-style fetch and never re-authenticate the SignalR circuit (see the component's
        // header comment). Selecting a persona must therefore call
        // NavigationManager.NavigateTo(url, forceLoad: true), targeting the Layer 2 secure
        // persona endpoint with a validated local returnUrl.
        using var ctx = CreateAuthorizedContext(BethuyaRoles.Attendee);
        var navigationManager = ctx.Services.GetRequiredService<FakeNavigationManager>();

        var cut = ctx.RenderComponent<DevPersonaToolbar>();
        var farahButton = cut.Find("[data-test='persona-farah']");
        farahButton.Click();

        await Assert.That(navigationManager.Uri).Contains("/dev/persona/Farah?returnUrl=");

        // FakeNavigationManager silently accepts forceLoad: false too, so asserting only on
        // .Uri would not catch a regression that dropped forceLoad. Assert on the recorded
        // NavigationOptions directly to prove the call site really requested a full reload.
        var lastNavigation = navigationManager.History.Last();
        await Assert.That(lastNavigation.Options.ForceLoad).IsTrue();
    }

    [Test]
    public async Task Render_WhenFeatureNotRegistered_RendersNothing()
    {
        // Simulates Production or a real Authentication:Provider — IDevPersonaToolbarFeature
        // is only registered by Program.cs when Provider == None AND Environment == Development.
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization().SetAuthorized("Farah").SetRoles(BethuyaRoles.Attendee);

        var cut = ctx.RenderComponent<DevPersonaToolbar>();

        await Assert.That(cut.Markup.Trim()).IsEmpty();
    }

    private static BunitCtx CreateAuthorizedContext(
        string role,
        string? secondRole = null,
        string? thirdRole = null,
        string? fourthRole = null,
        string name = "Test Persona",
        string email = "test@bethuya.dev",
        string sub = "dev-persona-test")
    {
        var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddSingleton<IDevPersonaToolbarFeature, DevPersonaToolbarFeature>();

        var roles = new List<string> { role };
        if (secondRole is not null) roles.Add(secondRole);
        if (thirdRole is not null) roles.Add(thirdRole);
        if (fourthRole is not null) roles.Add(fourthRole);

        ctx.AddTestAuthorization()
            .SetAuthorized(name)
            .SetRoles([.. roles])
            .SetClaims(
                new Claim("name", name),
                new Claim("email", email),
                new Claim("sub", sub));

        return ctx;
    }
}
