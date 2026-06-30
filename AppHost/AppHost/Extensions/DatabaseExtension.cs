using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure.AppContainers;

namespace AppHost.Extensions;

public static class DatabaseExtensions
{
        // Postgres keeps local development lightweight and avoids managed database provisioning friction.
    public static IResourceBuilder<PostgresDatabaseResource>
            ConfigureDatabase(
                this IDistributedApplicationBuilder builder, IResourceBuilder<AzureContainerAppEnvironmentResource> acaEnv)
    {
        var postgres = builder
            .AddPostgres("postgres")
            .WithComputeEnvironment(acaEnv);

        var database = postgres.AddDatabase("BethuyaDb");

        return database;
    }
}
