using Microsoft.AspNetCore.Http;

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Propagates the currently-selected development persona key from the Web SSR cookie to
/// outbound Refit requests as the <c>X-Bethuya-Dev-Persona</c> header, so the Backend
/// <c>DevelopmentAuthenticationHandler</c> constructs the same persona principal as the Web tier.
/// </summary>
/// <remarks>
/// <para>
/// Active ONLY when <c>Authentication:Provider=None</c> AND <c>Environment=Development</c>
/// (enforced at the call-site in <c>Program.cs</c> — the handler is not registered otherwise).
/// Real-provider flows are completely unaffected.
/// </para>
/// <para>
/// The propagated value is the opaque persona <em>key</em> (e.g. <c>Farah</c>) — never roles,
/// claims, or profile data. The Backend validates the key against the same allowlist
/// (<see cref="ServiceDefaults.Auth.DevelopmentPersonaCatalog"/>) and constructs claims from
/// the catalog entry, so no caller-supplied authorization data crosses the service boundary.
/// </para>
/// <para>
/// Never overwrites an existing <c>X-Bethuya-Dev-Persona</c> header if one was already set by
/// the caller (mirrors <see cref="BackendAccessTokenHandler"/> behavior, consistent with the
/// "don't double-set" convention; in practice nothing else sets this header).
/// </para>
/// </remarks>
public sealed class DevPersonaPropagationHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Only propagate if the header is not already present.
        if (!request.Headers.Contains(ServiceDefaults.Auth.DevelopmentPersonaCatalog.PersonaHeaderName))
        {
            // Read the persona key from the Web SSR cookie — this is guaranteed to be the same
            // key the Web DevelopmentAuthenticationHandler resolved for this request.
            var personaKey = httpContextAccessor.HttpContext?.Request.Cookies[
                ServiceDefaults.Auth.DevelopmentPersonaCatalog.PersonaCookieName];

            if (!string.IsNullOrEmpty(personaKey))
            {
                request.Headers.TryAddWithoutValidation(
                    ServiceDefaults.Auth.DevelopmentPersonaCatalog.PersonaHeaderName,
                    personaKey);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
