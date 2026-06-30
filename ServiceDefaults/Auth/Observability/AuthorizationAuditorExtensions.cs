using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ServiceDefaults.Auth.Observability;

/// <summary>
/// Helpers that turn a resource-ownership authorization outcome into an audit record. Centralises the
/// classification of <see cref="AuthorizationDecision"/> so endpoints record ownership decisions
/// consistently after calling <c>IAuthorizationService.AuthorizeAsync(...)</c>.
/// </summary>
public static class AuthorizationAuditorExtensions
{
    /// <summary>
    /// Classifies and records a resource-ownership decision evaluated against the
    /// <see cref="BethuyaPolicyNames.ResourceOwner"/> policy:
    /// <list type="bullet">
    ///   <item><see cref="AuthorizationDecision.Deny"/> when the result did not succeed;</item>
    ///   <item><see cref="AuthorizationDecision.Bypass"/> when success was earned by a bypass role
    ///   acting on a resource it does not own;</item>
    ///   <item><see cref="AuthorizationDecision.Allow"/> when the owning subject was granted access.</item>
    /// </list>
    /// </summary>
    /// <returns>The classified decision (also useful for branching at the call site).</returns>
    public static AuthorizationDecision RecordResourceOwnership(
        this IAuthorizationAuditor auditor,
        ClaimsPrincipal user,
        AuthorizationResult result,
        string? ownerUserId,
        string resourceType,
        string? resourceId,
        string? route,
        int outcomeStatusCode,
        string policyName = BethuyaPolicyNames.ResourceOwner)
    {
        ArgumentNullException.ThrowIfNull(auditor);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(result);

        var subject = OwnershipBypass.ResolveSubject(user);
        var decision = Classify(user, result, subject, ownerUserId);

        auditor.Record(new AuthorizationAuditEvent
        {
            Decision = decision,
            PolicyName = policyName,
            ResourceType = resourceType,
            ResourceId = resourceId,
            SubjectHash = SubjectHash.Compute(subject),
            Route = route,
            OutcomeStatusCode = outcomeStatusCode
        });

        return decision;
    }

    /// <summary>
    /// Records a non-ownership decision (decider-identity resolution, create-time stamping, onboarding
    /// guard) with an explicit decision and the caller's non-PII subject hash.
    /// </summary>
    public static void RecordDecision(
        this IAuthorizationAuditor auditor,
        AuthorizationDecision decision,
        string policyName,
        string resourceType,
        string? subject,
        string? resourceId,
        string? route,
        int outcomeStatusCode)
    {
        ArgumentNullException.ThrowIfNull(auditor);

        auditor.Record(new AuthorizationAuditEvent
        {
            Decision = decision,
            PolicyName = policyName,
            ResourceType = resourceType,
            ResourceId = resourceId,
            SubjectHash = SubjectHash.Compute(subject),
            Route = route,
            OutcomeStatusCode = outcomeStatusCode
        });
    }

    private static AuthorizationDecision Classify(
        ClaimsPrincipal user,
        AuthorizationResult result,
        string? subject,
        string? ownerUserId)
    {
        if (!result.Succeeded)
        {
            return AuthorizationDecision.Deny;
        }

        var isOwner = !string.IsNullOrEmpty(ownerUserId)
            && string.Equals(subject, ownerUserId, StringComparison.Ordinal);

        return !isOwner && OwnershipBypass.IsBypassRole(user)
            ? AuthorizationDecision.Bypass
            : AuthorizationDecision.Allow;
    }
}
