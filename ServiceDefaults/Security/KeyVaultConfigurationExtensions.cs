using Azure;
using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static Microsoft.Extensions.Hosting.KeyVaultStartupValidationHostedService;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Configures hosted Key Vault secret loading and startup validation.
/// </summary>
public static class KeyVaultConfigurationExtensions
{
    /// <summary>
    /// Adds Azure Key Vault as a configuration provider in non-development environments.
    /// </summary>
    public static TBuilder AddBethuyaKeyVaultConfiguration<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        if (builder.Environment.IsDevelopment())
        {
            return builder;
        }

        var options = new KeyVaultRuntimeOptions();
        builder.Configuration.GetSection(KeyVaultRuntimeOptions.SectionName).Bind(options);

        var keyVaultUriValue = builder.Configuration["KEY_VAULT_URI"]
            ?? builder.Configuration["VAULT_URI"]
            ?? builder.Configuration["KeyVaultUri"]
            ?? options.Uri;

        if (string.IsNullOrWhiteSpace(keyVaultUriValue))
        {
            throw new InvalidOperationException(
                "Hosted secret mode requires a Key Vault URI. " +
                "Configure KEY_VAULT_URI, VAULT_URI, KeyVaultUri, or KeyVault:Uri.");
        }

        if (!Uri.TryCreate(keyVaultUriValue, UriKind.Absolute, out var keyVaultUri))
        {
            throw new InvalidOperationException(
                $"Hosted secret mode received an invalid Key Vault URI: '{keyVaultUriValue}'.");
        }

        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true
        });
        builder.Services.AddSingleton<TokenCredential>(_ => credential);
        builder.Configuration.AddAzureKeyVault(keyVaultUri, credential, new SingleHyphenSecretManager());

        builder.Services.AddSingleton(new KeyVaultStartupValidationContext(
            keyVaultUri,
            options.RequiredConfigurationKeys,
            options.RequiredSecretNames));

        builder.Services.AddHostedService<KeyVaultStartupValidationHostedService>();

        return builder;
    }
}

/// <summary>
/// Non-secret runtime settings for hosted Key Vault configuration.
/// </summary>
public sealed class KeyVaultRuntimeOptions
{
    public const string SectionName = "KeyVault";

    /// <summary>
    /// Optional fallback URI when <c>KEY_VAULT_URI</c> is not present.
    /// </summary>
    public string? Uri { get; set; }

    /// <summary>
    /// Required configuration keys after all providers are loaded.
    /// </summary>
    public string[] RequiredConfigurationKeys { get; set; } = [];

    /// <summary>
    /// Required Key Vault secret names to assert existence at startup.
    /// </summary>
    public string[] RequiredSecretNames { get; set; } = [];
}

/// <summary>
/// Startup validation context for Key Vault checks.
/// </summary>
public sealed record KeyVaultStartupValidationContext(
    Uri VaultUri,
    string[] RequiredConfigurationKeys,
    string[] RequiredSecretNames);

internal sealed partial class KeyVaultStartupValidationHostedService(
    IConfiguration configuration,
    KeyVaultStartupValidationContext context,
    TokenCredential credential,
    ILogger<KeyVaultStartupValidationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LogValidatingHostedSecretConfiguration(logger, context.VaultUri.Host);

        var missingConfigurationKeys = context.RequiredConfigurationKeys
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .ToArray();

        if (missingConfigurationKeys.Length > 0)
        {
            throw new InvalidOperationException(
                $"Hosted configuration is missing required keys: {string.Join(", ", missingConfigurationKeys)}.");
        }

        if (context.RequiredSecretNames.Length == 0)
        {
            return;
        }

        var secretClient = new SecretClient(context.VaultUri, credential);
        var missingSecrets = new List<string>();

        foreach (var secretName in context.RequiredSecretNames)
        {
            try
            {
                await secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                missingSecrets.Add(secretName);
            }
            catch (RequestFailedException ex)
            {
                throw new InvalidOperationException(
                    $"Unable to validate secret '{secretName}' in Key Vault '{context.VaultUri.Host}'. " +
                    $"Received status code {ex.Status}.",
                    ex);
            }
        }

        if (missingSecrets.Count > 0)
        {
            throw new InvalidOperationException(
                $"Key Vault is missing required secrets: {string.Join(", ", missingSecrets)}.");
        }
    }

    /// <summary>
    /// Intercepts secrets pulled from Key Vault and formats the key for .NET IConfiguration.
    /// </summary>
    public sealed class SingleHyphenSecretManager : KeyVaultSecretManager
    {
        public override string GetKey(KeyVaultSecret secret)
        {
            // Translates single hyphens to colons for hierarchical configuration mapping.
            // Example: "cloudinary-cloudname" -> "cloudinary:cloudname"
            return secret.Name.Replace("-", ":");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Validating hosted secret configuration against Key Vault '{KeyVaultHost}'.")]
    private static partial void LogValidatingHostedSecretConfiguration(
        ILogger logger,
        string keyVaultHost);
}
