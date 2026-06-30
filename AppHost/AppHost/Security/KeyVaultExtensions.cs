using System;
using System.Collections.Generic;
using System.Text;
using Aspire.Hosting.Azure;

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
}
