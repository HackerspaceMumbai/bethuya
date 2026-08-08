using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ServiceDefaults.Auth;

/// <summary>
/// Authenticates every local request with either the fixed development principal (no persona
/// selected) or a catalog-driven persona principal (persona key present in cookie or header).
/// </summary>
/// <remarks>
/// <para>
/// Resolution order when <c>Environment=Development</c> AND <c>Authentication:Provider=None</c>:
/// <list type="number">
///   <item><description>Header <c>X-Bethuya-Dev-Persona</c> (Backend-direct / Refit propagation)</description></item>
///   <item><description>Cookie <c>bethuya-dev-persona</c> (Web SSR browser surface)</description></item>
///   <item><description>Legacy fixed admin principal (no persona selected)</description></item>
/// </list>
/// </para>
/// <para>
/// Both surfaces are validated identically against <see cref="DevelopmentPersonaCatalog"/>
/// (allowlist, fail-closed). The header is populated only by <c>DevPersonaPropagationHandler</c>
/// on Refit calls, but since the Backend cannot cryptographically distinguish an internally-
/// generated header from an external one, the same strict validation applies regardless of
/// trust intent — no special-casing.
/// </para>
/// </remarks>
internal sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHostEnvironment environment,
    IOptionsMonitor<BethuyaAuthOptions> authOptionsMonitor)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private static readonly Action<ILogger, string, string?, Exception?> s_personaResolved =
        LoggerMessage.Define<string, string?>(
            LogLevel.Information,
            new EventId(3100, "DevPersonaResolved"),
            "Development persona resolved: {PersonaKey} -> {Subject}");

    private static readonly Action<ILogger, string, string?, Exception?> s_personaUnknown =
        LoggerMessage.Define<string, string?>(
            LogLevel.Warning,
            new EventId(3101, "DevPersonaUnknown"),
            "Development persona key not found in catalog: {PersonaKey} -> {Subject} (fail-closed, no roles assigned)");

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Belt-and-suspenders: only honor persona switching in Development+Provider=None.
        // The handler is only registered by BethuyaAuthenticationExtensions when Provider=None,
        // but we re-check environment defensively to ensure that even in edge cases
        // (e.g. AllowInsecureDevAuth=true in Production) the persona surface is never activated
        // outside a true Development environment.
        var isDevEnvironment = environment.IsDevelopment();
        var isProviderNone = authOptionsMonitor.CurrentValue.Provider == AuthProviderType.None;

        if (!isDevEnvironment || !isProviderNone)
        {
            // Not in persona-switching mode — return legacy fixed principal unconditionally.
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                DevelopmentAuthenticationDefaults.CreatePrincipal(),
                DevelopmentAuthenticationDefaults.SchemeName)));
        }

        // (a) Header first: Backend-direct / Refit propagation surface.
        Request.Headers.TryGetValue(DevelopmentPersonaCatalog.PersonaHeaderName, out var headerValues);
        var personaKey = headerValues.FirstOrDefault();

        // (b) Cookie: Web SSR browser surface.
        if (string.IsNullOrEmpty(personaKey))
        {
            Request.Cookies.TryGetValue(DevelopmentPersonaCatalog.PersonaCookieName, out var cookieValue);
            personaKey = cookieValue;
        }

        // (c) Neither present → legacy default (case 1, backward-compatible).
        if (string.IsNullOrEmpty(personaKey))
        {
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                DevelopmentAuthenticationDefaults.CreatePrincipal(),
                DevelopmentAuthenticationDefaults.SchemeName)));
        }

        ClaimsPrincipal principal;

        if (DevelopmentPersonaCatalog.TryGet(personaKey, out var persona) && persona is not null)
        {
            // Case 2: known persona — construct claims exclusively from catalog.
            principal = DevelopmentAuthenticationDefaults.CreatePersonaPrincipal(persona);
            var subject = principal.FindFirst("sub")?.Value;
            s_personaResolved(Logger, personaKey, subject, null);
        }
        else
        {
            // Case 3: unknown/malformed key — fail closed. Authenticated, but zero roles.
            // This ensures a tampered or typo'd key never silently escalates to admin.
            // Log the key as a structured field (not interpolated into the template string)
            // so it appears in log sinks without risk of log injection.
            principal = DevelopmentAuthenticationDefaults.CreateUnknownPersonaPrincipal(personaKey);
            var subject = principal.FindFirst("sub")?.Value;
            s_personaUnknown(Logger, personaKey, subject, null);
        }

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            principal,
            DevelopmentAuthenticationDefaults.SchemeName)));
    }
}
