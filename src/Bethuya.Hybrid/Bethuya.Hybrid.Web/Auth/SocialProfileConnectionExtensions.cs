using System.Diagnostics;
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
using Microsoft.Extensions.Logging;

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
    private static readonly string[] _defaultLinkedInOidcScopes = ["openid", "profile"];
    private static readonly Action<ILogger, string, string, string, string, string, string, Exception?> _socialFailureLog =
        LoggerMessage.Define<string, string, string, string, string, string>(
            LogLevel.Warning,
            new EventId(3001, "SocialConnectionFailure"),
            "Social connection failure detected at stage {Stage}. Provider: {Provider}. ErrorCode: {ErrorCode}. ProviderError: {ProviderError}. ReturnPath: {ReturnPath}. TraceId: {TraceId}");

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
                oauth.CorrelationCookie.SameSite =
                      builder.Environment.IsDevelopment()
                         ? SameSiteMode.Lax
                         : SameSiteMode.None;

                oauth.CorrelationCookie.SecurePolicy =
                    builder.Environment.IsDevelopment()
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;
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
                    },
                    OnRemoteFailure = context => HandleSocialRemoteFailureAsync(context, "github")
                };
            });
        }

        if (IsConfigured(options.LinkedIn))
        {
            authenticationBuilder.AddOAuth(SocialProfileConnectionDefaults.LinkedInScheme, oauth =>
            {
                var scopes = ResolveLinkedInScopes(options.LinkedIn);
                var usesOpenIdConnect = UsesLinkedInOpenIdConnect(scopes);

                oauth.SignInScheme = SocialProfileConnectionDefaults.ExternalCookieScheme;
                oauth.ClientId = options.LinkedIn.ClientId;
                oauth.ClientSecret = options.LinkedIn.ClientSecret;
                oauth.CallbackPath = string.IsNullOrWhiteSpace(options.LinkedIn.CallbackPath)
                    ? "/signin-linkedin-connect"
                    : options.LinkedIn.CallbackPath;
                // ✅ FIX: Correlation cookie for ACA + local
                oauth.CorrelationCookie.SameSite =
                    builder.Environment.IsDevelopment()
                        ? SameSiteMode.Lax
                        : SameSiteMode.None;

                oauth.CorrelationCookie.SecurePolicy =
                    builder.Environment.IsDevelopment()
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;
                oauth.AuthorizationEndpoint = "https://www.linkedin.com/oauth/v2/authorization";
                oauth.TokenEndpoint = "https://www.linkedin.com/oauth/v2/accessToken";
                oauth.UserInformationEndpoint = usesOpenIdConnect
                    ? "https://api.linkedin.com/v2/userinfo"
                    : "https://api.linkedin.com/v2/me?projection=(id,vanityName)";
                oauth.SaveTokens = false;
                oauth.Scope.Clear();
                foreach (var scope in scopes)
                {
                    oauth.Scope.Add(scope);
                }

                oauth.ClaimActions.MapJsonKey("urn:linkedin:member_id", usesOpenIdConnect ? "sub" : "id");

                if (!usesOpenIdConnect)
                {
                    oauth.ClaimActions.MapJsonKey("urn:linkedin:vanity_name", "vanityName");
                }

                oauth.Events = new OAuthEvents
                {
                    OnCreatingTicket = context => CreateLinkedInTicketAsync(context, usesOpenIdConnect),
                    OnRemoteFailure = context => HandleSocialRemoteFailureAsync(context, "linkedin")
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
            IConfiguration configuration,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Bethuya.Hybrid.Web.Auth.SocialProfileConnection");
            var options = new SocialProfileConnectionOptions();
            configuration.GetSection(SocialProfileConnectionOptions.SectionName).Bind(options);

            var normalizedProvider = provider.Trim().ToLowerInvariant();
            var scheme = normalizedProvider switch
            {
                "github" => SocialProfileConnectionDefaults.GitHubScheme,
                "linkedin" => SocialProfileConnectionDefaults.LinkedInScheme,
                _ => null
            };

            if (scheme is null)
            {
                RecordSocialFailureTelemetry(
                    logger,
                    normalizedProvider,
                    "social-provider-not-supported",
                    "start",
                    returnUrl,
                    null);
                context.Response.Redirect(BuildReturnUrl(returnUrl, "social-provider-not-supported", normalizedProvider));
                return;
            }

            if (!IsConfigured(normalizedProvider, options))
            {
                RecordSocialFailureTelemetry(
                    logger,
                    normalizedProvider,
                    "social-provider-not-configured",
                    "start",
                    returnUrl,
                    null);
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
            HttpContext context,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Bethuya.Hybrid.Web.Auth.SocialProfileConnection");
            var normalizedProvider = provider.Trim().ToLowerInvariant();
            var result = await context.AuthenticateAsync(SocialProfileConnectionDefaults.ExternalCookieScheme);

            if (!result.Succeeded || result.Principal is null)
            {
                RecordSocialFailureTelemetry(
                    logger,
                    normalizedProvider,
                    "social-connect-failed",
                    "complete",
                    returnUrl,
                    null,
                    result.Failure);
                context.Response.Redirect(BuildReturnUrl(returnUrl, "social-connect-failed", normalizedProvider));
                return;
            }

            await context.SignOutAsync(SocialProfileConnectionDefaults.ExternalCookieScheme);

            var redirectUrl = normalizedProvider switch
            {
                "github" => BuildGitHubReturnUrl(returnUrl, result.Principal, logger),
                "linkedin" => BuildLinkedInReturnUrl(returnUrl, result.Principal, logger),
                _ => BuildUnsupportedProviderReturnUrl(returnUrl, normalizedProvider, logger)
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

    private static string BuildGitHubReturnUrl(
        string? returnUrl,
        ClaimsPrincipal principal,
        ILogger logger)
    {
        var login = principal.FindFirstValue("urn:github:login");
        var profileUrl = principal.FindFirstValue("urn:github:profile_url");

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(profileUrl))
        {
            RecordSocialFailureTelemetry(
                logger,
                "github",
                "github-connect-incomplete",
                "complete",
                returnUrl,
                null);
            return BuildReturnUrl(returnUrl, "github-connect-incomplete", "github");
        }

        return QueryHelpers.AddQueryString(NormalizeReturnUrl(returnUrl), new Dictionary<string, string?>
        {
            ["githubLogin"] = login,
            ["githubProfileUrl"] = profileUrl
        });
    }

    private static string BuildLinkedInReturnUrl(
        string? returnUrl,
        ClaimsPrincipal principal,
        ILogger logger)
    {
        var memberId = principal.FindFirstValue("urn:linkedin:member_id");
        var vanityName = principal.FindFirstValue("urn:linkedin:vanity_name");

        if (string.IsNullOrWhiteSpace(memberId))
        {
            RecordSocialFailureTelemetry(
                logger,
                "linkedin",
                "linkedin-connect-incomplete",
                "complete",
                returnUrl,
                null);
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

    private static string BuildUnsupportedProviderReturnUrl(
        string? returnUrl,
        string provider,
        ILogger logger)
    {
        RecordSocialFailureTelemetry(
            logger,
            provider,
            "social-provider-not-supported",
            "complete",
            returnUrl,
            null);
        return BuildReturnUrl(returnUrl, "social-provider-not-supported", provider);
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

    private static string[] ResolveLinkedInScopes(SocialOAuthOptions options)
    {
        var configuredScopes = options.Scopes
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return configuredScopes.Length > 0 ? configuredScopes : _defaultLinkedInOidcScopes;
    }

    private static bool UsesLinkedInOpenIdConnect(IEnumerable<string> scopes)
        => scopes.Contains("openid", StringComparer.OrdinalIgnoreCase);

    private static async Task CreateLinkedInTicketAsync(OAuthCreatingTicketContext context, bool usesOpenIdConnect)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);

        if (!usesOpenIdConnect)
        {
            request.Headers.Add("X-RestLi-Protocol-Version", "2.0.0");
        }

        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"LinkedIn user information request failed with status code {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
        context.RunClaimActions(payload.RootElement);
    }

    private static Task HandleSocialRemoteFailureAsync(RemoteFailureContext context, string provider)
    {
        var errorCode = ResolveRemoteFailureErrorCode(provider, context);
        var returnUrl = ExtractReturnUrl(context.Properties?.RedirectUri);
        var providerError = NormalizeProviderError(context.Request.Query["error"].ToString());

        RecordSocialFailureTelemetry(
            GetLogger(context.HttpContext),
            provider,
            errorCode,
            "remote-failure",
            returnUrl,
            providerError,
            context.Failure);

        context.HandleResponse();
        context.Response.Redirect(BuildReturnUrl(returnUrl, errorCode, provider));
        return Task.CompletedTask;
    }

    private static string ResolveRemoteFailureErrorCode(string provider, RemoteFailureContext context)
    {
        var providerError = context.Request.Query["error"].ToString();
        var failureMessage = context.Failure?.Message ?? string.Empty;

        if (provider.Equals("linkedin", StringComparison.OrdinalIgnoreCase) &&
            (providerError.Equals("unauthorized_scope_error", StringComparison.OrdinalIgnoreCase) ||
             failureMessage.Contains("unauthorized_scope_error", StringComparison.OrdinalIgnoreCase) ||
             failureMessage.Contains("scope", StringComparison.OrdinalIgnoreCase)))
        {
            return "social-provider-scope-not-authorized";
        }

        if (providerError.Equals("access_denied", StringComparison.OrdinalIgnoreCase))
        {
            return "social-connect-cancelled";
        }

        return "social-connect-failed";
    }

    private static string? ExtractReturnUrl(string? redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return null;
        }

        var queryIndex = redirectUri.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0 || queryIndex == redirectUri.Length - 1)
        {
            return null;
        }

        var query = QueryHelpers.ParseQuery(redirectUri[(queryIndex + 1)..]);
        return query.TryGetValue("returnUrl", out var returnUrl) ? returnUrl.ToString() : null;
    }

    private static ILogger GetLogger(HttpContext context)
        => context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Bethuya.Hybrid.Web.Auth.SocialProfileConnection");

    private static string NormalizeProviderError(string? providerError)
    {
        if (string.IsNullOrWhiteSpace(providerError))
        {
            return "none";
        }

        var trimmed = providerError.Trim();
        return trimmed.Length > 64 ? trimmed[..64] : trimmed;
    }

    private static void RecordSocialFailureTelemetry(
        ILogger logger,
        string provider,
        string errorCode,
        string stage,
        string? returnUrl,
        string? providerError,
        Exception? exception = null)
    {
        var normalizedProvider = string.IsNullOrWhiteSpace(provider) ? "unknown" : provider;
        var normalizedReturnPath = NormalizeReturnUrl(returnUrl);
        var normalizedProviderError = NormalizeProviderError(providerError);
        var traceId = Activity.Current?.TraceId.ToString();

        _socialFailureLog(
            logger,
            stage,
            normalizedProvider,
            errorCode,
            normalizedProviderError,
            normalizedReturnPath,
            traceId ?? "none",
            exception);

        Activity.Current?.SetStatus(ActivityStatusCode.Error, errorCode);
        Activity.Current?.SetTag("bethuya.social.provider", normalizedProvider);
        Activity.Current?.SetTag("bethuya.social.error_code", errorCode);
        Activity.Current?.SetTag("bethuya.social.stage", stage);
        Activity.Current?.SetTag("bethuya.social.return_path", normalizedReturnPath);
        Activity.Current?.SetTag("bethuya.social.provider_error", normalizedProviderError);
        Activity.Current?.AddEvent(new ActivityEvent(
            "bethuya.social.connection.failure",
            tags: new ActivityTagsCollection
            {
                { "provider", normalizedProvider },
                { "error_code", errorCode },
                { "stage", stage }
            }));
    }
}
