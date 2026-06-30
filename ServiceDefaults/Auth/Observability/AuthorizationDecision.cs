namespace ServiceDefaults.Auth.Observability;

/// <summary>
/// The outcome of a security-relevant authorization decision, recorded in the audit trail and emitted
/// as a metric dimension. Distinguishing <see cref="Bypass"/> from <see cref="Allow"/> is deliberate:
/// a grant earned by an operational bypass role (Admin/Organizer/Curator acting on a resource they do
/// not own) is a higher-signal event than a normal owner self-access.
/// </summary>
public enum AuthorizationDecision
{
    /// <summary>The caller was granted access as the resource owner (or by a role-scoped policy).</summary>
    Allow,

    /// <summary>The caller was denied access (ownership mismatch, missing identity, or policy failure).</summary>
    Deny,

    /// <summary>Access was granted via an operational bypass role rather than ownership.</summary>
    Bypass
}
