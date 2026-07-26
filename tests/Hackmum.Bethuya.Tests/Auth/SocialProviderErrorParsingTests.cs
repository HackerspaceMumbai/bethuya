using Bethuya.Hybrid.Web.Auth;
using Microsoft.AspNetCore.Authentication;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// Covers NormalizeProviderError's exception-message fallback so regressions are caught
/// if ASP.NET changes its OAuth token-failure message format or if parsing is refactored.
/// </summary>
public class SocialProviderErrorParsingTests
{
    [Test]
    public async Task NormalizeProviderError_QueryParamPresent_ReturnsQueryParam()
    {
        var result = SocialProfileConnectionExtensions.NormalizeProviderError("access_denied");
        await Assert.That(result).IsEqualTo("access_denied");
    }

    [Test]
    public async Task NormalizeProviderError_NullQueryParam_NullException_ReturnsNone()
    {
        var result = SocialProfileConnectionExtensions.NormalizeProviderError(null, null);
        await Assert.That(result).IsEqualTo("none");
    }

    [Test]
    public async Task NormalizeProviderError_EmptyQueryParam_OAuthTokenFailureException_ExtractsErrorCode()
    {
        // This is the exact format ASP.NET Core's OAuth handler emits on token-exchange failure.
        var exception = new AuthenticationFailureException(
            "OAuth token endpoint failure: incorrect_client_credentials;" +
            "Description=The client_id and/or client_secret passed are incorrect.;" +
            "Uri=https://docs.github.com/apps/managing-oauth-apps/troubleshooting-oauth-app-access-token-request-errors/#incorrect-client-credentials");

        var result = SocialProfileConnectionExtensions.NormalizeProviderError(string.Empty, exception);

        await Assert.That(result).IsEqualTo("incorrect_client_credentials");
    }

    [Test]
    public async Task NormalizeProviderError_EmptyQueryParam_OAuthTokenFailureException_NoSemicolon_ExtractsFullToken()
    {
        var exception = new AuthenticationFailureException("OAuth token endpoint failure: bad_verification_code");

        var result = SocialProfileConnectionExtensions.NormalizeProviderError(string.Empty, exception);

        await Assert.That(result).IsEqualTo("bad_verification_code");
    }

    [Test]
    public async Task NormalizeProviderError_EmptyQueryParam_UnrelatedExceptionWithColons_ReturnsNone()
    {
        // Exceptions like "Response status code does not indicate success: 401 (Unauthorized)"
        // must not be mis-parsed as a provider error token.
        var exception = new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized)");

        var result = SocialProfileConnectionExtensions.NormalizeProviderError(string.Empty, exception);

        await Assert.That(result).IsEqualTo("none");
    }

    [Test]
    public async Task NormalizeProviderError_QueryParamTooLong_IsTruncatedTo64Chars()
    {
        var longError = new string('x', 100);
        var result = SocialProfileConnectionExtensions.NormalizeProviderError(longError);
        await Assert.That(result.Length).IsEqualTo(64);
    }

    [Test]
    public async Task NormalizeProviderError_OAuthErrorTokenTooLong_IsTruncatedTo64Chars()
    {
        var longCode = new string('y', 100);
        var exception = new AuthenticationFailureException($"OAuth token endpoint failure: {longCode};Description=...");

        var result = SocialProfileConnectionExtensions.NormalizeProviderError(string.Empty, exception);

        await Assert.That(result.Length).IsEqualTo(64);
    }
}
