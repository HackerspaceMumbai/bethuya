using Bethuya.Hybrid.Shared.Services;

namespace Bethuya.Hybrid.Web.Services;

/// <summary>
/// Unauthenticated placeholder for <see cref="ICurrentUserService"/>.
/// Replace by merging or cherry-picking from an auth provider branch:
///   feature/auth/entra    — Microsoft Entra External ID
///   feature/auth/auth0    — Auth0
///   feature/auth/keycloak — Keycloak (self-hosted OIDC)
/// </summary>
internal sealed class NullCurrentUserService : ICurrentUserService
{
    public string? UserId => null;
    public string? Email => null;
    public bool IsAuthenticated => false;
    public bool IsInRole(string role) => false;
}
