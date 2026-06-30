using System.Text.Json;
using Bethuya.Hybrid.Web.Components;
using Bethuya.Hybrid.Shared.Services;
using Bethuya.Hybrid.Web.Auth;
using Bethuya.Hybrid.Web.Services;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Refit;

var builder = WebApplication.CreateBuilder(args);

builder.AddBethuyaKeyVaultConfiguration();


if (!builder.Environment.IsDevelopment())
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        var configuredForwardLimit = builder.Configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit");

        options.ForwardLimit = configuredForwardLimit is > 0
            ? configuredForwardLimit
            : 1;

        options.RequireHeaderSymmetry = true;

        var knownProxies =
            builder.Configuration
                .GetSection("ForwardedHeaders:KnownProxies")
                .Get<string[]>()
            ?? [];

        foreach (var knownProxy in knownProxies)
        {
            if (System.Net.IPAddress.TryParse(knownProxy, out var proxyAddress))
            {
                options.KnownProxies.Add(proxyAddress);
            }
        }

        var knownNetworks =
            builder.Configuration
                .GetSection("ForwardedHeaders:KnownNetworks")
                .Get<string[]>()
            ?? [];

        foreach (var knownNetwork in knownNetworks)
        {
            if (System.Net.IPNetwork.TryParse(knownNetwork, out var network))
            {
                options.KnownIPNetworks.Add(network);
            }
        }
    });
}


builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddBlazorBlueprintComponents();
builder.AddSocialProfileConnectionAuthentication();

// Add device-specific services used by the Bethuya.Hybrid.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// Authentication — provider selected via appsettings "Authentication:Provider"
builder.AddBethuyaWebAuthentication();
builder.AddBethuyaAuthorization();

// Resolve auth options to determine provider mode
var authOptions = new BethuyaAuthOptions();
builder.Configuration.GetSection(BethuyaAuthOptions.SectionName).Bind(authOptions);

if (authOptions.Provider == AuthProviderType.None)
{
    // Dev mode: fake auth state + shared development principal
    builder.Services.AddScoped<AuthenticationStateProvider, DevelopmentAuthenticationStateProvider>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, ClaimsCurrentUserService>();
}
else
{
    // Production: real OIDC auth state + claims-based user service
    builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();
    builder.Services.AddScoped<ICurrentUserService, ClaimsCurrentUserService>();
    builder.Services.AddHttpContextAccessor();

    // Forward the signed-in user's access token to the Backend API so it can enforce authorization.
    builder.Services.AddScoped<IBackendAccessTokenProvider, HttpContextBackendAccessTokenProvider>();
    builder.Services.AddTransient<BackendAccessTokenHandler>();
}

// Attaches the backend access-token handler to a Refit client, but only when a real auth provider
// is configured (in dev mode Provider=None there is no token to forward and the handler is unregistered).
IHttpClientBuilder ConfigureBackendAuth(IHttpClientBuilder clientBuilder)
{
    if (authOptions.Provider != AuthProviderType.None)
    {
        clientBuilder.AddHttpMessageHandler<BackendAccessTokenHandler>();
    }

    return clientBuilder;
}

// Refit typed client for Backend Events API (Aspire service discovery)
ConfigureBackendAuth(builder.Services
    .AddRefitClient<IEventApi>(new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    })
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https+http://backend")))
    .AddStandardResilienceHandler(options =>
    {
        // File uploads can exceed the default 30s timeout
        options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
        // Circuit breaker sampling must be ≥ 2× attempt timeout
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
    });

// Refit typed client for Backend Planning Cycle API (Aspire service discovery)
ConfigureBackendAuth(builder.Services
    .AddRefitClient<IPlanningCycleApi>(new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    })
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https+http://backend")))
    .AddStandardResilienceHandler();

// Refit typed client for Backend Profile API (Aspire service discovery)
ConfigureBackendAuth(builder.Services
    .AddRefitClient<IProfileApi>(new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    })
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https+http://backend")))
    .AddStandardResilienceHandler();

// Refit typed client for Backend Registrations API (Aspire service discovery)
ConfigureBackendAuth(builder.Services
    .AddRefitClient<IRegistrationApi>(new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    })
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https+http://backend")))
    .AddStandardResilienceHandler();

// Refit typed client for Backend Curation API (Aspire service discovery)
ConfigureBackendAuth(builder.Services
    .AddRefitClient<ICurationApi>(new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    })
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https+http://backend")))
    .AddStandardResilienceHandler();

// CORS — origins from appsettings.json "Cors:AllowedOrigins" (empty by default in production)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("BethuyaMobileClients", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()));


/*builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
});*/


var app = builder.Build();



if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
}



if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseHttpsRedirection(); // ✅ only dev
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // HSTS optional → since HTTPS termination happens at the edge, not in the app, HSTS headers from the app won't have any effect.
    // Ensure HSTS is configured at the edge (e.g., Azure Front Door) if desired. 
}


// Security headers (CSP, X-Frame-Options, etc.) + rate limiting — from ServiceDefaults

if (!app.Environment.IsDevelopment())
{
    app.UseSecurityDefaults();
}

app.UseCors("BethuyaMobileClients");

// Authentication + Authorization middleware (no-op when provider is None)
app.UseBethuyaAuthentication();

app.UseAntiforgery();

app.MapStaticAssets();

// Auth endpoints (login/logout/user-info) — only functional when a provider is configured
app.MapBethuyaAuthEndpoints();
app.MapSocialProfileConnectionEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(Bethuya.Hybrid.Shared._Imports).Assembly,
        typeof(Bethuya.Hybrid.Web.Client._Imports).Assembly);

app.MapDefaultEndpoints();

app.Run();
