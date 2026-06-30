using ServiceDefaults.Auth.Observability;

namespace Hackmum.Bethuya.Tests.Auth.Observability;

/// <summary>
/// PR5: <see cref="SubjectHash"/> produces a stable, non-PII correlation id from the JWT <c>sub</c>. It
/// must be deterministic across processes (so a caller's decisions correlate), must differ per subject,
/// must collapse null/whitespace to <see langword="null"/>, and must never echo the raw subject.
/// </summary>
public sealed class SubjectHashTests
{
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Compute_NullOrWhitespace_ReturnsNull(string? subject)
    {
        await Assert.That(SubjectHash.Compute(subject)).IsNull();
    }

    [Test]
    public async Task Compute_SameSubject_IsStable()
    {
        var first = SubjectHash.Compute("auth0|abc123");
        var second = SubjectHash.Compute("auth0|abc123");

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first).IsNotNull();
    }

    [Test]
    public async Task Compute_DifferentSubjects_Differ()
    {
        var a = SubjectHash.Compute("subject-a");
        var b = SubjectHash.Compute("subject-b");

        await Assert.That(a).IsNotEqualTo(b);
    }

    [Test]
    public async Task Compute_DoesNotEchoRawSubject()
    {
        const string subject = "auth0|raw-subject-value";

        var hash = SubjectHash.Compute(subject)!;

        await Assert.That(hash).IsNotEqualTo(subject);
        await Assert.That(hash.Contains(subject, StringComparison.OrdinalIgnoreCase)).IsFalse();
    }

    [Test]
    public async Task Compute_IsLowercaseHex()
    {
        var hash = SubjectHash.Compute("subject-hex")!;

        // 12 bytes => 24 hex chars, all lowercase hex digits.
        await Assert.That(hash.Length).IsEqualTo(24);
        await Assert.That(hash.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'))).IsTrue();
    }
}
