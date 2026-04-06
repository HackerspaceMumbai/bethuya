﻿using Microsoft.Extensions.Logging;
using BlazorBlueprint.Components;
using Bethuya.Hybrid.Shared.Services;
using Bethuya.Hybrid.Services;

namespace Bethuya.Hybrid;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Add device-specific services used by the Bethuya.Hybrid.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddBlazorBlueprintComponents();

        // Ensure the Squad knows this is the "Bridge" to the Shared RCL
        builder.Services.AddSingleton<IBethuyaAuthStateProvider, MauiAuthStateProvider>();

        // Required for AddHttpMessageHandler<AuthenticatedUserHandler>()
        builder.Services.AddTransient<AuthenticatedUserHandler>();

        // Register the Aspire-aware HttpClient for the API
        // TODO: Validate this pattern with MAUI team (see issue #XXX)
        builder.Services.AddBethuyaClient("bethuya-api")
            .AddHttpMessageHandler<AuthenticatedUserHandler>(); // Syncs tokens to Shared RCL calls

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
