using Microsoft.AspNetCore.Authorization;

namespace ServiceDefaults.Auth;

/// <summary>
/// Authorization requirement for the <see cref="BethuyaPolicyNames.ResourceOwner"/> policy. Satisfied
/// when the caller owns the target resource (subject matches the resource owner) or holds a role with
/// a legitimate operational bypass. Evaluated against a <see cref="ResourceOwnerContext"/> resource.
/// </summary>
public sealed class SameOwnerRequirement : IAuthorizationRequirement;
