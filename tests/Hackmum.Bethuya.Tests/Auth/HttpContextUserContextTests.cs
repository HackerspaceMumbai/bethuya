using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR3 (M2/M3): <see cref="HttpContextUserContext"/> exposes the validated server-side identity
/// (id / email / name / authentication state) sourced from <see cref="IHttpContextAccessor"/>,
/// replacing hand-rolled claim reads in endpoints.
/// </summary>
public sealed class HttpContextUserContextTests
{
    [Test]
    public async Task AuthenticatedPrincipal_ExposesSubEmailAndName()
    {
        var sut = Create(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-42"),
            new Claim("email", "user42@bethuya.test"),
            new Claim("name", "User Forty-Two"),
        ], authenticationType: "Test")));

        await Assert.That(sut.IsAuthenticated).IsTrue();
        await Assert.That(sut.UserId).IsEqualTo("user-42");
        await Assert.That(sut.Email).IsEqualTo("user42@bethuya.test");
        await Assert.That(sut.Name).IsEqualTo("User Forty-Two");
    }

    [Test]
    public async Task FallsBackToNameIdentifierAndClaimTypesEmail()
    {
        var sut = Create(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "nid-7"),
            new Claim(ClaimTypes.Email, "nid7@bethuya.test"),
        ], authenticationType: "Test")));

        await Assert.That(sut.IsAuthenticated).IsTrue();
        await Assert.That(sut.UserId).IsEqualTo("nid-7");
        await Assert.That(sut.Email).IsEqualTo("nid7@bethuya.test");
    }

    [Test]
    public async Task AnonymousPrincipal_IsNotAuthenticated_AndHasNullIdentity()
    {
        var sut = Create(new ClaimsPrincipal(new ClaimsIdentity()));

        await Assert.That(sut.IsAuthenticated).IsFalse();
        await Assert.That(sut.UserId).IsNull();
        await Assert.That(sut.Email).IsNull();
    }

    [Test]
    public async Task NoHttpContext_IsNotAuthenticated()
    {
        var accessor = new HttpContextAccessor { HttpContext = null };
        var sut = new HttpContextUserContext(accessor);

        await Assert.That(sut.IsAuthenticated).IsFalse();
        await Assert.That(sut.UserId).IsNull();
    }

    private static HttpContextUserContext Create(ClaimsPrincipal principal)
    {
        var context = new DefaultHttpContext { User = principal };
        return new HttpContextUserContext(new HttpContextAccessor { HttpContext = context });
    }
}
