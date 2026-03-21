using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Bethuya.Hybrid.Shared.Services;
using Bethuya.Hybrid.Web.Client.Auth;
using Bethuya.Hybrid.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add device-specific services used by the Bethuya.Hybrid.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// Auth state from server-persisted claims
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();

await builder.Build().RunAsync();
