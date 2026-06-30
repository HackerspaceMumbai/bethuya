using System.Security.Cryptography;
using System.Text;

namespace ServiceDefaults.Auth.Observability;

/// <summary>
/// Produces a stable, non-PII identifier for an authentication subject (JWT <c>sub</c>) so the audit
/// trail can correlate a caller's decisions without persisting or logging the raw IdP subject.
/// <para>
/// The <c>sub</c> claim is already an opaque, non-PII identifier; hashing is defence-in-depth so the
/// raw subject is never emitted to logs/metrics/traces and cannot be cross-referenced back to the IdP.
/// The hash is the lowercase hex of the first 12 bytes of <c>SHA-256(sub)</c> — deterministic across
/// processes (unsalted), collision-resistant enough for correlation, and short enough for log/trace
/// attributes. It must never be derived from Email, Name, or any other PII field.
/// </para>
/// </summary>
public static class SubjectHash
{
    private const int HashByteLength = 12;

    /// <summary>
    /// Computes the stable hash of <paramref name="subject"/>, or <see langword="null"/> when the
    /// subject is <see langword="null"/>/whitespace (no resolvable identity to correlate).
    /// </summary>
    /// <param name="subject">The authentication subject (JWT <c>sub</c>). Never an email or name.</param>
    public static string? Compute(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(subject));
        return Convert.ToHexStringLower(bytes.AsSpan(0, HashByteLength));
    }
}
