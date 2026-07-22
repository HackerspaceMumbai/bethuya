using System.Collections.Generic;
using Aspire.Hosting.Azure;
using Aspire.Hosting.ApplicationModel;

namespace AppHost.Security;

public static class KeyVaultExtensions
{
    public static IResourceBuilder<AzureKeyVaultResource>? ConfigureKeyVault(
        this IDistributedApplicationBuilder builder)
    {

        if (!builder.ExecutionContext.IsPublishMode)
        {
            return null;
        }

        return builder.AddAzureKeyVault("vault");

    }

    /// <summary>
    /// Adds a Key Vault secret using a collision-safe internal resource name while preserving
    /// the canonical external secret name used by hosted configuration validation.
    /// </summary>
    public static void AddBridgedSecret(
        this IResourceBuilder<AzureKeyVaultResource> builder,
        string secretName,
        IResourceBuilder<ParameterResource> parameterResource)
    {
        var resourceName = CreateSecretResourceName(secretName);
        _ = builder.AddSecret(resourceName, secretName, parameterResource);
    }

    private static string CreateSecretResourceName(string secretName)
    {
        var chars = new List<char>(secretName.Length + 3) { 'k', 'v', '-' };
        var previousWasDash = true;

        foreach (var character in secretName.ToLowerInvariant())
        {
            var nextCharacter = char.IsLetterOrDigit(character) ? character : '-';
            if (nextCharacter == '-' && previousWasDash)
            {
                continue;
            }

            chars.Add(nextCharacter);
            previousWasDash = nextCharacter == '-';
        }

        if (chars[^1] == '-')
        {
            chars.RemoveAt(chars.Count - 1);
        }

        return new string([.. chars]);
    }
}
