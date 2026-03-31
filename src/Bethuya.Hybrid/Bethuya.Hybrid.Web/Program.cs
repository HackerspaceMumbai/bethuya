using System.Text.Json;
using Bethuya.Hybrid.Web.Components;
using Bethuya.Hybrid.Shared.Services;
using Bethuya.Hybrid.Web.Auth;
using Bethuya.Hybrid.Web.Services;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Hosting;
using Refit;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddBlazorBlueprintComponents();

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
    // Dev mode: fake auth state + null user service
    builder.Services.AddScoped<AuthenticationStateProvider, DevelopmentAuthenticationStateProvider>();
    builder.Services.AddScoped<ICurrentUserService, NullCurrentUserService>();
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddAuthorization();
}
else
{
    // Production: real OIDC auth state + claims-based user service
    builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();
    builder.Services.AddScoped<ICurrentUserService, ClaimsCurrentUserService>();
    builder.Services.AddHttpContextAccessor();
}

// Refit typed client for Backend Events API (Aspire service discovery)
builder.Services
    .AddRefitClient<IEventApi>(new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    })
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https+http://backend"));

// CORS — origins from appsettings.json "Cors:AllowedOrigins" (empty by default in production)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("BethuyaMobileClients", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Security headers (CSP, X-Frame-Options, etc.) + rate limiting — from ServiceDefaults
app.UseSecurityDefaults();

// Authentication + Authorization middleware (no-op when provider is None)
app.UseBethuyaAuthentication();

app.UseCors("BethuyaMobileClients");

app.UseAntiforgery();

app.MapStaticAssets();

// Auth endpoints (login/logout/user-info) — only functional when a provider is configured
app.MapBethuyaAuthEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(Bethuya.Hybrid.Shared._Imports).Assembly,
        typeof(Bethuya.Hybrid.Web.Client._Imports).Assembly);

app.MapDefaultEndpoints();

app.Run();
