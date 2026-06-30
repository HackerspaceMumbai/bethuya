using Microsoft.AspNetCore.Authorization;
using ServiceDefaults.Auth.Observability;

namespace ServiceDefaults.Auth;

/// <summary>
/// Resource-based authorization handler for <see cref="SameOwnerRequirement"/>. Succeeds when the
/// caller holds a bypass role (<see cref="BethuyaRoleNames.Admin"/>,
/// <see cref="BethuyaRoleNames.Organizer"/>, <see cref="BethuyaRoleNames.Curator"/>) or when the
/// caller's validated subject matches the resource's recorded owner.
/// <para>
/// Reads roles and subject directly off the validated principal so it stays decoupled from request
/// scopes and DI. A <see langword="null"/> owner is never matched to a subject — only a bypass role
/// can reach an unowned resource (fail-closed for legacy/unowned rows).
/// </para>
/// </summary>
public sealed class ResourceOwnerHandler : AuthorizationHandler<SameOwnerRequirement, ResourceOwnerContext>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SameOwnerRequirement requirement,
        ResourceOwnerContext resource)
    {
        var user = context.User;

        if (user.Identity?.IsAuthenticated is not true)
        {
            return Task.CompletedTask;
        }

        if (OwnershipBypass.IsBypassRole(user))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var subject = OwnershipBypass.ResolveSubject(user);

        if (!string.IsNullOrEmpty(resource.OwnerUserId)
            && string.Equals(subject, resource.OwnerUserId, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
