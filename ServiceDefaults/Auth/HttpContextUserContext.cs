using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ServiceDefaults.Auth;

/// <summary>
/// <see cref="IUserContext"/> implementation that reads the validated principal from the ambient
/// <see cref="IHttpContextAccessor"/>. Returns an unauthenticated context when no HTTP context or
/// principal is present (fail-closed for background/non-request scopes).
/// </summary>
public sealed class HttpContextUserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated is true;

    /// <inheritdoc />
    public string? UserId => IsAuthenticated
        ? Principal!.FindFirst("sub")?.Value ?? Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
        : null;

    /// <inheritdoc />
    public string? Email => IsAuthenticated
        ? Principal!.FindFirst("email")?.Value ?? Principal.FindFirst(ClaimTypes.Email)?.Value
        : null;

    /// <inheritdoc />
    public string? Name => IsAuthenticated
        ? Principal!.FindFirst("name")?.Value ?? Principal.Identity?.Name
        : null;
}
