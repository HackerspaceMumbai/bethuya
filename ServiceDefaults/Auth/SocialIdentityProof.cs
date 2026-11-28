using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServiceDefaults.Auth;

/// <summary>
/// The provider identity that a completed GitHub or LinkedIn OAuth sign-in vouched for,
/// bound to the Bethuya user who performed it.
/// </summary>
/// <param name="Provider">Either <see cref="SocialIdentityProof.GitHub"/> or <see cref="SocialIdentityProof.LinkedIn"/>.</param>
/// <param name="Subject">The Bethuya user the proof was issued to (see <see cref="SocialIdentityProof.ResolveSubject"/>).</param>
/// <param name="ProviderUserId">The provider's stable account id (GitHub numeric id, LinkedIn member id).</param>
/// <param name="Login">The provider login or handle, when the provider returns one.</param>
/// <param name="ProfileUrl">The provider profile URL, when the provider returns one.</param>
/// <param name="ExpiresAtUnixSeconds">When the proof stops being accepted.</param>
public sealed record SocialIdentityClaims(
    string Provider,
    string Subject,
    string ProviderUserId,
    string? Login,
    string? ProfileUrl,
    long ExpiresAtUnixSeconds);

/// <summary>
/// Issues and validates tamper-proof social identity proofs.
/// </summary>
/// <remarks>
/// The web host mints a proof inside the OAuth callback, from the claims the provider returned.
/// The backend only stores a social identity when it receives a proof that carries a valid
/// HMAC-SHA256 signature, has not expired, names the expected provider and was issued to the
/// calling user. Values typed into a request or edited in the browser address bar are never
/// enough on their own.
/// Format: <c>v1.&lt;base64url(json claims)&gt;.&lt;base64url(hmac)&gt;</c>.
/// </remarks>
public static class SocialIdentityProof
{
    /// <summary>Configuration key that holds the shared signing key (at least 32 characters).</summary>
    public const string SigningKeyConfigurationKey = "SocialVerification:SigningKey";

    /// <summary>Provider name for GitHub proofs.</summary>
    public const string GitHub = "github";

    /// <summary>Provider name for LinkedIn proofs.</summary>
    public const string LinkedIn = "linkedin";

    /// <summary>Minimum signing key length, in characters.</summary>
    public const int MinimumSigningKeyLength = 32;

    /// <summary>How long a proof stays valid; long enough to connect both providers in one sitting.</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(30);

    private const string VersionPrefix = "v1";
    private const int MaxTokenLength = 4096;

    /// <summary>Returns true when <paramref name="signingKey"/> is long enough to sign proofs.</summary>
    public static bool IsUsableSigningKey(string? signingKey)
        => signingKey is { Length: >= MinimumSigningKeyLength };

    /// <summary>
    /// The user identifier a proof is bound to. Prefers the Entra object id (<c>oid</c>), which stays
    /// the same in ID tokens and API access tokens, then falls back to <c>sub</c> / name identifier.
    /// </summary>
    public static string? ResolveSubject(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated is not true)
        {
            return null;
        }

        var subject = user.FindFirst("oid")?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrWhiteSpace(subject) ? null : subject;
    }

    /// <summary>Creates a signed proof for <paramref name="claims"/>.</summary>
    /// <exception cref="ArgumentException">The signing key is missing or too short.</exception>
    public static string Create(SocialIdentityClaims claims, string signingKey)
    {
        ArgumentNullException.ThrowIfNull(claims);

        if (!IsUsableSigningKey(signingKey))
        {
            throw new ArgumentException(
                $"The social verification signing key must be at least {MinimumSigningKeyLength} characters.",
                nameof(signingKey));
        }

        var payloadJson = JsonSerializer.SerializeToUtf8Bytes(claims, SocialIdentityProofJsonContext.Default.SocialIdentityClaims);
        var signedPart = $"{VersionPrefix}.{Base64Url.EncodeToString(payloadJson)}";
        return $"{signedPart}.{Base64Url.EncodeToString(Sign(signedPart, signingKey))}";
    }

    /// <summary>
    /// Validates <paramref name="token"/> and returns its claims when the signature is valid, the proof
    /// has not expired, and it was issued for <paramref name="expectedProvider"/> to <paramref name="expectedSubject"/>.
    /// </summary>
    public static bool TryValidate(
        string? token,
        string? signingKey,
        string expectedProvider,
        string? expectedSubject,
        DateTimeOffset now,
        out SocialIdentityClaims? claims)
    {
        claims = null;

        if (string.IsNullOrWhiteSpace(token)
            || token.Length > MaxTokenLength
            || !IsUsableSigningKey(signingKey)
            || string.IsNullOrWhiteSpace(expectedSubject))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 3 || !string.Equals(parts[0], VersionPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var signedPart = $"{parts[0]}.{parts[1]}";
        byte[] providedSignature;
        byte[] payloadJson;

        try
        {
            providedSignature = Base64Url.DecodeFromChars(parts[2]);
            payloadJson = Base64Url.DecodeFromChars(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(providedSignature, Sign(signedPart, signingKey!)))
        {
            return false;
        }

        SocialIdentityClaims? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(payloadJson, SocialIdentityProofJsonContext.Default.SocialIdentityClaims);
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed is null
            || !string.Equals(parsed.Provider, expectedProvider, StringComparison.Ordinal)
            || !string.Equals(parsed.Subject, expectedSubject, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(parsed.ProviderUserId)
            || now.ToUnixTimeSeconds() >= parsed.ExpiresAtUnixSeconds)
        {
            return false;
        }

        claims = parsed;
        return true;
    }

    private static byte[] Sign(string signedPart, string signingKey)
        => HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(signedPart));
}

[JsonSerializable(typeof(SocialIdentityClaims))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class SocialIdentityProofJsonContext : JsonSerializerContext;
