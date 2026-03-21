using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Microsoft.Extensions.Hosting;

/// <summary>Extension methods for configuring Bethuya authentication on the Web (cookie + OIDC) and Backend (JWT Bearer) projects.</summary>
public static class BethuyaAuthenticationExtensions
{
    /// <summary>
    /// Adds Bethuya authentication for the Blazor Web App (cookie + OIDC).
    /// Reads the <c>Authentication</c> configuration section to select the provider.
    /// </summary>
    public static TBuilder AddBethuyaWebAuthentication<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var authOptions = new BethuyaAuthOptions();
        builder.Configuration.GetSection(BethuyaAuthOptions.SectionName).Bind(authOptions);

        if (authOptions.Provider == AuthProviderType.None)
        {
            return builder;
        }

        var services = builder.Services;
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, oidc =>
            {
                ConfigureOidc(oidc, authOptions);
            });

        services.AddCascadingAuthenticationState();

        return builder;
    }

    /// <summary>
    /// Adds Bethuya authentication for the Backend API (JWT Bearer).
    /// Reads the <c>Authentication</c> configuration section to determine the issuer.
    /// </summary>
    public static TBuilder AddBethuyaApiAuthentication<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var authOptions = new BethuyaAuthOptions();
        builder.Configuration.GetSection(BethuyaAuthOptions.SectionName).Bind(authOptions);

        if (authOptions.Provider == AuthProviderType.None)
        {
            return builder;
        }

        var services = builder.Services;
        services.AddAuthentication()
            .AddJwtBearer(jwt =>
            {
                jwt.Authority = GetAuthority(authOptions);
                jwt.Audience = GetAudience(authOptions);
                jwt.TokenValidationParameters.NameClaimType = "name";
                jwt.TokenValidationParameters.RoleClaimType = GetRoleClaim(authOptions);
            });

        return builder;
    }

    /// <summary>
    /// Adds authentication and authorization middleware to the pipeline.
    /// Call after <see cref="Extensions.UseSecurityDefaults"/> and before <c>UseAntiforgery</c>.
    /// </summary>
    public static WebApplication UseBethuyaAuthentication(this WebApplication app)
    {
        var authOptions = new BethuyaAuthOptions();
        app.Configuration.GetSection(BethuyaAuthOptions.SectionName).Bind(authOptions);

        if (authOptions.Provider == AuthProviderType.None)
        {
            return app;
        }

        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    /// <summary>Maps login and logout endpoints for the OIDC flow.</summary>
    public static IEndpointRouteBuilder MapBethuyaAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/authentication");

        group.MapGet("/login", async (string? returnUrl, HttpContext context) =>
        {
            await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties { RedirectUri = returnUrl ?? "/" });
        });

        group.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties { RedirectUri = "/" });
        });

        return endpoints;
    }

    private static void ConfigureOidc(OpenIdConnectOptions oidc, BethuyaAuthOptions authOptions)
    {
        oidc.ResponseType = OpenIdConnectResponseType.Code;
        oidc.SaveTokens = true;
        oidc.GetClaimsFromUserInfoEndpoint = true;
        oidc.MapInboundClaims = false;

        oidc.Scope.Clear();
        oidc.Scope.Add("openid");
        oidc.Scope.Add("profile");
        oidc.Scope.Add("email");

        oidc.TokenValidationParameters.NameClaimType = "name";
        oidc.TokenValidationParameters.RoleClaimType = GetRoleClaim(authOptions);

        switch (authOptions.Provider)
        {
            case AuthProviderType.Entra:
                ConfigureEntra(oidc, authOptions.Entra);
                break;
            case AuthProviderType.Auth0:
                ConfigureAuth0(oidc, authOptions.Auth0);
                break;
            case AuthProviderType.Keycloak:
                ConfigureKeycloak(oidc, authOptions.Keycloak);
                break;
        }
    }

    private static void ConfigureEntra(OpenIdConnectOptions oidc, EntraOptions entra)
    {
        var authority = string.IsNullOrEmpty(entra.Domain)
            ? $"{entra.Instance.TrimEnd('/')}/{entra.TenantId}/v2.0"
            : $"https://{entra.Domain}/{entra.TenantId}/v2.0";

        oidc.Authority = authority;
        oidc.ClientId = entra.ClientId;
        oidc.ClientSecret = entra.ClientSecret;
        oidc.CallbackPath = entra.CallbackPath;
    }

    private static void ConfigureAuth0(OpenIdConnectOptions oidc, Auth0Options auth0)
    {
        oidc.Authority = $"https://{auth0.Domain}";
        oidc.ClientId = auth0.ClientId;
        oidc.ClientSecret = auth0.ClientSecret;
        oidc.CallbackPath = auth0.CallbackPath;

        if (!string.IsNullOrEmpty(auth0.Audience))
        {
            oidc.Resource = auth0.Audience;
        }
    }

    private static void ConfigureKeycloak(OpenIdConnectOptions oidc, KeycloakOptions keycloak)
    {
        oidc.Authority = keycloak.Authority;
        oidc.ClientId = keycloak.ClientId;
        oidc.ClientSecret = keycloak.ClientSecret;
        oidc.CallbackPath = keycloak.CallbackPath;
    }

    private static string GetAuthority(BethuyaAuthOptions options) => options.Provider switch
    {
        AuthProviderType.Entra => string.IsNullOrEmpty(options.Entra.Domain)
            ? $"{options.Entra.Instance.TrimEnd('/')}/{options.Entra.TenantId}/v2.0"
            : $"https://{options.Entra.Domain}/{options.Entra.TenantId}/v2.0",
        AuthProviderType.Auth0 => $"https://{options.Auth0.Domain}",
        AuthProviderType.Keycloak => options.Keycloak.Authority,
        _ => ""
    };

    private static string GetAudience(BethuyaAuthOptions options) => options.Provider switch
    {
        AuthProviderType.Entra => options.Entra.ClientId,
        AuthProviderType.Auth0 => options.Auth0.Audience,
        AuthProviderType.Keycloak => options.Keycloak.ClientId,
        _ => ""
    };

    private static string GetRoleClaim(BethuyaAuthOptions options) => options.Provider switch
    {
        AuthProviderType.Entra => "roles",
        AuthProviderType.Auth0 => "https://bethuya.dev/roles",
        AuthProviderType.Keycloak => "realm_access",
        _ => "role"
    };
}
