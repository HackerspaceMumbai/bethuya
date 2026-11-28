using System.Buffers.Text;
using System.Security.Claims;
using System.Text;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

public class SocialIdentityProofTests
{
    private const string SigningKey = "unit-test-social-verification-key-0123456789";
    private const string Subject = "user-123";
    private static readonly DateTimeOffset _now = new(2026, 11, 28, 10, 0, 0, TimeSpan.Zero);

    private static SocialIdentityClaims GitHubClaims(DateTimeOffset? expiresAt = null)
        => new(
            SocialIdentityProof.GitHub,
            Subject,
            "583231",
            "octocat",
            "https://github.com/octocat",
            (expiresAt ?? _now.AddMinutes(30)).ToUnixTimeSeconds());

    [Test]
    public async Task Create_ThenValidate_ReturnsOriginalClaims()
    {
        var token = SocialIdentityProof.Create(GitHubClaims(), SigningKey);

        var isValid = SocialIdentityProof.TryValidate(token, SigningKey, SocialIdentityProof.GitHub, Subject, _now, out var claims);

        await Assert.That(isValid).IsTrue();
        await Assert.That(claims).IsEqualTo(GitHubClaims());
    }

    [Test]
    public async Task Validate_EditedPayload_IsRejected()
    {
        var token = SocialIdentityProof.Create(GitHubClaims(), SigningKey);
        var parts = token.Split('.');
        var forgedJson = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[1])).Replace("octocat", "torvalds", StringComparison.Ordinal);
        var forged = $"{parts[0]}.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(forgedJson))}.{parts[2]}";

        var isValid = SocialIdentityProof.TryValidate(forged, SigningKey, SocialIdentityProof.GitHub, Subject, _now, out var claims);

        await Assert.That(isValid).IsFalse();
        await Assert.That(claims).IsNull();
    }

    [Test]
    public async Task Validate_WrongKey_IsRejected()
    {
        var token = SocialIdentityProof.Create(GitHubClaims(), "a-different-signing-key-that-is-long-enough");

        var isValid = SocialIdentityProof.TryValidate(token, SigningKey, SocialIdentityProof.GitHub, Subject, _now, out _);

        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task Validate_Expired_IsRejected()
    {
        var token = SocialIdentityProof.Create(GitHubClaims(expiresAt: _now.AddSeconds(-1)), SigningKey);

        var isValid = SocialIdentityProof.TryValidate(token, SigningKey, SocialIdentityProof.GitHub, Subject, _now, out _);

        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task Validate_OtherSubject_IsRejected()
    {
        var token = SocialIdentityProof.Create(GitHubClaims(), SigningKey);

        var isValid = SocialIdentityProof.TryValidate(token, SigningKey, SocialIdentityProof.GitHub, "someone-else", _now, out _);

        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task Validate_OtherProvider_IsRejected()
    {
        var token = SocialIdentityProof.Create(GitHubClaims(), SigningKey);

        var isValid = SocialIdentityProof.TryValidate(token, SigningKey, SocialIdentityProof.LinkedIn, Subject, _now, out _);

        await Assert.That(isValid).IsFalse();
    }

    [Test]
    [Arguments("")]
    [Arguments("not-a-proof")]
    [Arguments("v1.%%%.%%%")]
    [Arguments("v2.e30.AAAA")]
    public async Task Validate_Malformed_IsRejected(string token)
    {
        var isValid = SocialIdentityProof.TryValidate(token, SigningKey, SocialIdentityProof.GitHub, Subject, _now, out _);

        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task Validate_MissingOrShortKey_IsRejected()
    {
        var token = SocialIdentityProof.Create(GitHubClaims(), SigningKey);

        await Assert.That(SocialIdentityProof.TryValidate(token, null, SocialIdentityProof.GitHub, Subject, _now, out _)).IsFalse();
        await Assert.That(SocialIdentityProof.TryValidate(token, "short", SocialIdentityProof.GitHub, Subject, _now, out _)).IsFalse();
    }

    [Test]
    public async Task Create_ShortKey_Throws()
    {
        await Assert.That(() => SocialIdentityProof.Create(GitHubClaims(), "short")).Throws<ArgumentException>();
    }

    [Test]
    public async Task ResolveSubject_PrefersObjectIdThenSub()
    {
        var entraUser = new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid", "object-id"), new Claim("sub", "pairwise-sub")], "Test"));
        var oidcUser = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "keycloak-sub")], "Test"));
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "ignored")]));

        await Assert.That(SocialIdentityProof.ResolveSubject(entraUser)).IsEqualTo("object-id");
        await Assert.That(SocialIdentityProof.ResolveSubject(oidcUser)).IsEqualTo("keycloak-sub");
        await Assert.That(SocialIdentityProof.ResolveSubject(anonymous)).IsNull();
    }
}
