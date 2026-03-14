using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Bethuya.Hybrid.Shared.Services;
using Bethuya.Hybrid.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add device-specific services used by the Bethuya.Hybrid.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

await builder.Build().RunAsync();
