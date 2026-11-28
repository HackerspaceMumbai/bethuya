using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ServiceDefaults.Auth;

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Propagates the currently-selected development persona key from either the Web SSR cookie
/// or the active Blazor <c>InteractiveServer</c> circuit principal to outbound Refit requests
/// as the <c>X-Bethuya-Dev-Persona</c> header, so the Backend <c>DevelopmentAuthenticationHandler</c>
/// constructs the same persona principal as the Web tier.
/// </summary>
public sealed class DevPersonaPropagationHandler(
    IHttpContextAccessor httpContextAccessor,
    ICircuitPrincipalAccessor? circuitPrincipalAccessor = null) : DelegatingHandler
{
    private const string UnknownPersonaSub = "dev-persona-unknown";

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Only propagate if the header is not already present.
        if (!request.Headers.Contains(DevelopmentPersonaCatalog.PersonaHeaderName))
        {
            // Prefer the HTTP request cookie when inside an active HTTP request (SSR / tests).
            var personaKey = httpContextAccessor.HttpContext?.Request.Cookies[
                DevelopmentPersonaCatalog.PersonaCookieName];

            // On InteractiveServer SignalR circuit dispatches, HttpContext is unavailable;
            // fall back to the circuit's authenticated ClaimsPrincipal captured by BethuyaCircuitHandler.
            if (string.IsNullOrEmpty(personaKey))
            {
                personaKey = ResolvePersonaKeyFromPrincipal(circuitPrincipalAccessor?.CurrentPrincipal);
            }

            if (!string.IsNullOrEmpty(personaKey))
            {
                request.Headers.TryAddWithoutValidation(
                    DevelopmentPersonaCatalog.PersonaHeaderName,
                    personaKey);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static string? ResolvePersonaKeyFromPrincipal(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return null;
        }

        var personaClaim = principal.Claims.FirstOrDefault(c =>
            c.Type == DevelopmentAuthenticationDefaults.PersonaClaimType &&
            c.Issuer == DevelopmentAuthenticationDefaults.PersonaClaimIssuer);

        if (!string.IsNullOrWhiteSpace(personaClaim?.Value))
        {
            return personaClaim.Value;
        }

        // Preserve fail-closed behavior if the circuit connected with an unrecognized persona cookie.
        if (string.Equals(principal.FindFirst("sub")?.Value, UnknownPersonaSub, StringComparison.Ordinal))
        {
            return UnknownPersonaSub;
        }

        return null;
    }
}
