using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

public class LocalReturnUrlTests
{
    [Test]
    [Arguments("/")]
    [Arguments("/events")]
    [Arguments("/registration/social?step=2")]
    public async Task IsLocal_AppRelativePath_ReturnsTrue(string url)
    {
        await Assert.That(LocalReturnUrl.IsLocal(url)).IsTrue();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("events")]
    [Arguments("https://evil.example")]
    [Arguments("//evil.example")]
    [Arguments("/\\evil.example")]
    [Arguments("/\t/evil.example")]
    [Arguments("/\r\n/evil.example")]
    public async Task IsLocal_OffSiteOrInvalid_ReturnsFalse(string? url)
    {
        await Assert.That(LocalReturnUrl.IsLocal(url)).IsFalse();
    }

    [Test]
    public async Task GetOrDefault_OffSiteUrl_ReturnsFallback()
    {
        await Assert.That(LocalReturnUrl.GetOrDefault("/\\evil.example")).IsEqualTo("/");
        await Assert.That(LocalReturnUrl.GetOrDefault("https://evil.example", "/registration/social"))
            .IsEqualTo("/registration/social");
        await Assert.That(LocalReturnUrl.GetOrDefault("/events")).IsEqualTo("/events");
    }
}
