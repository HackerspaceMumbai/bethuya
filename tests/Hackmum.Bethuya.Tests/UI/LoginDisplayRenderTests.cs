using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Shared.Components.Auth;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using BunitCtx = Bunit.BunitContext;

namespace Hackmum.Bethuya.Tests.UI;

public sealed class LoginDisplayRenderTests
{
    [Test]
    public async Task Render_WhenAuthenticated_ShowsUserNameAndPlatformRoles()
    {
        await using var ctx = new BunitCtx();
        ctx.AddAuthorization()
            .SetAuthorized("Priya")
            .SetRoles(BethuyaRoles.Curator, BethuyaRoles.Attendee);

        var cut = ctx.Render<LoginDisplay>();

        await Assert.That(cut.Find("[data-test='user-name']").TextContent).IsEqualTo("Priya");
        var roleSummary = cut.Find("[data-test='user-role']").TextContent;
        await Assert.That(roleSummary).Contains(BethuyaRoles.Curator);
        await Assert.That(roleSummary).Contains(BethuyaRoles.Attendee);
    }

    [Test]
    public async Task Render_WhenUnauthenticated_IncludesCurrentReturnUrlOnLoginLink()
    {
        await using var ctx = new BunitCtx();
        ctx.AddAuthorization().SetNotAuthorized();
        var navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("events");

        var cut = ctx.Render<LoginDisplay>();

        var loginHref = cut.Find("[data-test='login-link']").GetAttribute("href");
        await Assert.That(loginHref).IsEqualTo("/authentication/login?returnUrl=%2Fevents");
    }
}
