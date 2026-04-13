using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bethuya.Hybrid.Web.Auth;

internal static class SocialProfileConnectionDefaults
{
    public const string ExternalCookieScheme = "SocialProfileExternalCookie";
    public const string GitHubScheme = "GitHubProfileConnect";
    public const string LinkedInScheme = "LinkedInProfileConnect";
}

/// <summary>Registers and maps verified social profile connection flows for onboarding.</summary>
public static class SocialProfileConnectionExtensions
{
    public static WebApplicationBuilder AddSocialProfileConnectionAuthentication(this WebApplicationBuilder builder)
    {
        var options = new SocialProfileConnectionOptions();
        builder.Configuration.GetSection(SocialProfileConnectionOptions.SectionName).Bind(options);
        builder.Services.Configure<SocialProfileConnectionOptions>(
            builder.Configuration.GetSection(SocialProfileConnectionOptions.SectionName));

        var authenticationBuilder = builder.Services.AddAuthentication();

        authenticationBuilder.AddCookie(SocialProfileConnectionDefaults.ExternalCookieScheme, cookie =>
        {
            cookie.Cookie.Name = "__bethuya-social-connect";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Lax;
            cookie.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        });

        if (IsConfigured(options.GitHub))
        {
            authenticationBuilder.AddOAuth(SocialProfileConnectionDefaults.GitHubScheme, oauth =>
            {
                oauth.SignInScheme = SocialProfileConnectionDefaults.ExternalCookieScheme;
                oauth.ClientId = options.GitHub.ClientId;
                oauth.ClientSecret = options.GitHub.ClientSecret;
                oauth.CallbackPath = string.IsNullOrWhiteSpace(options.GitHub.CallbackPath)
                    ? "/signin-github-connect"
                    : options.GitHub.CallbackPath;
                oauth.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                oauth.TokenEndpoint = "https://github.com/login/oauth/access_token";
                oauth.UserInformationEndpoint = "https://api.github.com/user";
                oauth.SaveTokens = false;
                oauth.Scope.Clear();
                oauth.Scope.Add("read:user");

                oauth.ClaimActions.MapJsonKey("urn:github:login", "login");
                oauth.ClaimActions.MapJsonKey("urn:github:id", "id");
                oauth.ClaimActions.MapJsonKey("urn:github:profile_url", "html_url");

                oauth.Events = new OAuthEvents
                {
                    OnCreatingTicket = async context =>
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                        request.Headers.Accept.ParseAdd("application/json");
                        request.Headers.Add("User-Agent", "Bethuya");
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);

                        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                        response.EnsureSuccessStatusCode();

                        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                        context.RunClaimActions(payload.RootElement);
                    }
                };
            });
        }

        if (IsConfigured(options.LinkedIn))
        {
            authenticationBuilder.AddOAuth(SocialProfileConnectionDefaults.LinkedInScheme, oauth =>
            {
                oauth.SignInScheme = SocialProfileConnectionDefaults.ExternalCookieScheme;
                oauth.ClientId = options.LinkedIn.ClientId;
                oauth.ClientSecret = options.LinkedIn.ClientSecret;
                oauth.CallbackPath = string.IsNullOrWhiteSpace(options.LinkedIn.CallbackPath)
                    ? "/signin-linkedin-connect"
                    : options.LinkedIn.CallbackPath;
                oauth.AuthorizationEndpoint = "https://www.linkedin.com/oauth/v2/authorization";
                oauth.TokenEndpoint = "https://www.linkedin.com/oauth/v2/accessToken";
                oauth.UserInformationEndpoint = "https://api.linkedin.com/v2/me?projection=(id,vanityName)";
                oauth.SaveTokens = false;
                oauth.Scope.Clear();

                var scopes = options.LinkedIn.Scopes.Length > 0 ? options.LinkedIn.Scopes : ["r_liteprofile"];
                foreach (var scope in scopes)
                {
                    oauth.Scope.Add(scope);
                }

                oauth.ClaimActions.MapJsonKey("urn:linkedin:member_id", "id");
                oauth.ClaimActions.MapJsonKey("urn:linkedin:vanity_name", "vanityName");

                oauth.Events = new OAuthEvents
                {
                    OnCreatingTicket = async context =>
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                        request.Headers.Accept.ParseAdd("application/json");
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
                        request.Headers.Add("X-RestLi-Protocol-Version", "2.0.0");

                        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                        response.EnsureSuccessStatusCode();

                        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                        context.RunClaimActions(payload.RootElement);
                    }
                };
            });
        }

        return builder;
    }

    public static IEndpointRouteBuilder MapSocialProfileConnectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/authentication/social");

        group.MapGet("/{provider}/start", async (
            string provider,
            string? returnUrl,
            HttpContext context,
            IConfiguration configuration) =>
        {
            var options = new SocialProfileConnectionOptions();
            configuration.GetSection(SocialProfileConnectionOptions.SectionName).Bind(options);

            var normalizedProvider = provider.Trim().ToLowerInvariant();
            var scheme = normalizedProvider switch
            {
                "github" => SocialProfileConnectionDefaults.GitHubScheme,
                "linkedin" => SocialProfileConnectionDefaults.LinkedInScheme,
                _ => null
            };

            if (scheme is null || !IsConfigured(normalizedProvider, options))
            {
                context.Response.Redirect(BuildReturnUrl(returnUrl, "social-provider-not-configured", normalizedProvider));
                return;
            }

            await context.ChallengeAsync(scheme, new AuthenticationProperties
            {
                RedirectUri = $"/authentication/social/{normalizedProvider}/complete?returnUrl={Uri.EscapeDataString(NormalizeReturnUrl(returnUrl))}"
            });
        });

        group.MapGet("/{provider}/complete", async (
            string provider,
            string? returnUrl,
            HttpContext context) =>
        {
            var normalizedProvider = provider.Trim().ToLowerInvariant();
            var result = await context.AuthenticateAsync(SocialProfileConnectionDefaults.ExternalCookieScheme);

            if (!result.Succeeded || result.Principal is null)
            {
                context.Response.Redirect(BuildReturnUrl(returnUrl, "social-connect-failed", normalizedProvider));
                return;
            }

            await context.SignOutAsync(SocialProfileConnectionDefaults.ExternalCookieScheme);

            var redirectUrl = normalizedProvider switch
            {
                "github" => BuildGitHubReturnUrl(returnUrl, result.Principal),
                "linkedin" => BuildLinkedInReturnUrl(returnUrl, result.Principal),
                _ => BuildReturnUrl(returnUrl, "social-provider-not-supported", normalizedProvider)
            };

            context.Response.Redirect(redirectUrl);
        });

        return endpoints;
    }

    private static bool IsConfigured(string provider, SocialProfileConnectionOptions options) => provider switch
    {
        "github" => IsConfigured(options.GitHub),
        "linkedin" => IsConfigured(options.LinkedIn),
        _ => false
    };

    private static bool IsConfigured(SocialOAuthOptions options)
        => !string.IsNullOrWhiteSpace(options.ClientId) && !string.IsNullOrWhiteSpace(options.ClientSecret);

    private static string BuildGitHubReturnUrl(string? returnUrl, ClaimsPrincipal principal)
    {
        var login = principal.FindFirstValue("urn:github:login");
        var profileUrl = principal.FindFirstValue("urn:github:profile_url");

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(profileUrl))
        {
            return BuildReturnUrl(returnUrl, "github-connect-incomplete", "github");
        }

        return QueryHelpers.AddQueryString(NormalizeReturnUrl(returnUrl), new Dictionary<string, string?>
        {
            ["githubLogin"] = login,
            ["githubProfileUrl"] = profileUrl
        });
    }

    private static string BuildLinkedInReturnUrl(string? returnUrl, ClaimsPrincipal principal)
    {
        var memberId = principal.FindFirstValue("urn:linkedin:member_id");
        var vanityName = principal.FindFirstValue("urn:linkedin:vanity_name");

        if (string.IsNullOrWhiteSpace(memberId))
        {
            return BuildReturnUrl(returnUrl, "linkedin-connect-incomplete", "linkedin");
        }

        var query = new Dictionary<string, string?>
        {
            ["linkedinMemberId"] = memberId
        };

        if (!string.IsNullOrWhiteSpace(vanityName))
        {
            query["linkedinProfileUrl"] = $"https://www.linkedin.com/in/{vanityName}";
        }

        return QueryHelpers.AddQueryString(NormalizeReturnUrl(returnUrl), query);
    }

    private static string BuildReturnUrl(string? returnUrl, string errorCode, string? provider = null)
    {
        var query = new Dictionary<string, string?>
        {
            ["socialError"] = errorCode
        };

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query["socialProvider"] = provider;
        }

        return QueryHelpers.AddQueryString(NormalizeReturnUrl(returnUrl), query);
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/registration/social";
        }

        return returnUrl[0] == '/' && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : "/registration/social";
    }
}
