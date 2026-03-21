using Bethuya.Hybrid.Web.Components;
using Bethuya.Hybrid.Shared.Services;
using Bethuya.Hybrid.Web.Services;
using BlazorBlueprint.Components;
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

// Auth abstraction — unauthenticated placeholder until a provider branch is merged.
// See: feature/auth/entra | feature/auth/auth0 | feature/auth/keycloak
builder.Services.AddScoped<ICurrentUserService, NullCurrentUserService>();

// Refit typed client for Backend Events API (Aspire service discovery)
builder.Services
    .AddRefitClient<IEventApi>()
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

app.UseCors("BethuyaMobileClients");

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(Bethuya.Hybrid.Shared._Imports).Assembly,
        typeof(Bethuya.Hybrid.Web.Client._Imports).Assembly);

app.MapDefaultEndpoints();

app.Run();
