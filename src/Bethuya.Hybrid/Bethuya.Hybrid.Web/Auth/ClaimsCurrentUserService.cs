using System.Security.Claims;
using Bethuya.Hybrid.Shared.Services;

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Production <see cref="ICurrentUserService"/> implementation that reads identity
/// from the current <see cref="ClaimsPrincipal"/> via <see cref="IHttpContextAccessor"/>
/// or the active Blazor <c>InteractiveServer</c> circuit via <see cref="ICircuitPrincipalAccessor"/>.
/// </summary>
internal sealed class ClaimsCurrentUserService(
    IHttpContextAccessor httpContextAccessor,
    ICircuitPrincipalAccessor? circuitPrincipalAccessor = null) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User ?? circuitPrincipalAccessor?.CurrentPrincipal;

    public string? UserId => User?.FindFirst("sub")?.Value ?? User?.FindFirst("oid")?.Value;

    public string? Email => User?.FindFirst("email")?.Value ?? User?.FindFirst("preferred_username")?.Value;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
