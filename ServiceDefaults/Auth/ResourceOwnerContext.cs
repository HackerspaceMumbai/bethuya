namespace ServiceDefaults.Auth;

/// <summary>
/// Lightweight resource descriptor passed to
/// <c>IAuthorizationService.AuthorizeAsync(user, resource, BethuyaPolicyNames.ResourceOwner)</c>.
/// Carries only the owning subject so domain entities never need to reference the authorization layer
/// (the endpoint projects the entity's server-set owner id into this context).
/// </summary>
/// <param name="OwnerUserId">
/// The authentication subject that owns the resource, or <see langword="null"/> when the resource has
/// no recorded owner (e.g. legacy rows created before ownership tracking). A <see langword="null"/>
/// owner can only be accessed by a bypass role — it is never matched to a caller subject.
/// </param>
public sealed record ResourceOwnerContext(string? OwnerUserId);
