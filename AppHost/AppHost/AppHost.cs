using Aspire.Hosting.Azure;
using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(args);
builder.AddAzureContainerAppEnvironment("bethuya-env");



var sql = builder.AddAzureSqlServer("sql")
    .RunAsContainer()
    .AddDatabase("BethuyaDb");

// Key Vault — provisioned in Azure only; no local emulator exists.
// Only wire it in publish mode (azd up) so local dev starts without Azure credentials.
IResourceBuilder<AzureKeyVaultResource>? keyVault = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureKeyVault("vault")
    : null;

// Keycloak — local dev only; not published to Azure.
// The preview Keycloak package sets KC_HEALTH_ENABLED=true (boolean literal) in its container
// env vars. ACA's BaseContainerAppContext.ProcessValue cannot handle booleans, causing
// "Unsupported value type System.Boolean" during aspire deploy. In production, use Entra ID.
IResourceBuilder<KeycloakResource>? keycloak = null;
if (!builder.ExecutionContext.IsPublishMode)
{
    keycloak = builder.AddKeycloak("keycloak", 8080)
        .WithDataVolume();
}

// Cloudinary — image upload for event cover images
// secret: true omitted — causes "Unsupported value type System.Boolean" in Azure deployment.
// For production: route secrets through Key Vault via keyVault.AddSecret(...) in publish mode.
var cloudinaryCloudName = builder.AddParameter("cloudinary-cloud-name");
var cloudinaryApiKey = builder.AddParameter("cloudinary-api-key");
var cloudinaryApiSecret = builder.AddParameter("cloudinary-api-secret");

// // AI provider configuration — configure via user-secrets (dev) or Key Vault (Azure)
// // Dev: dotnet user-secrets set "Parameters:azure-openai-endpoint" "https://YOUR-RESOURCE.openai.azure.com/"
// var azureOpenAiEndpoint = builder.AddParameter("azure-openai-endpoint");
// var azureOpenAiApiKey = builder.AddParameter("azure-openai-api-key", secret: true);
// var openAiApiKey = builder.AddParameter("openai-api-key", secret: true);

// Migration service — runs EF Core migrations then exits; backend waits for it to complete.
// IMPORTANT: run `dotnet ef migrations add InitialCreate --project src/Hackmum.Bethuya.Infrastructure`
// before the first Azure deployment.
var migrationService = builder.AddProject<Projects.Bethuya_MigrationService>("migration-service")
    .WithReference(sql)
    .WaitFor(sql);

var backend = builder.AddProject<Projects.Hackmum_Bethuya_Backend>("backend")
    .WithReference(sql)
    .WaitFor(sql)
    .WaitForCompletion(migrationService)
    .WithEnvironment("Cloudinary__CloudName", cloudinaryCloudName)
    .WithEnvironment("Cloudinary__ApiKey", cloudinaryApiKey)
    .WithEnvironment("Cloudinary__ApiSecret", cloudinaryApiSecret)
    .WithEnvironment("AI_PROVIDER_PLANNER", "AzureOpenAI")
    .WithEnvironment("AI_PROVIDER_CURATOR", "FoundryLocal")
    .WithEnvironment("AI_PROVIDER_REPORTER", "AzureOpenAI")
    .WithEnvironment("AI_PROVIDER_ORCHESTRATOR", "AzureOpenAI")
    .WithHttpHealthCheck("/health")
    .PublishAsAzureContainerApp((infra, app) =>
    {
        app.Template.Scale.MinReplicas = 1;
        app.Template.Scale.MaxReplicas = 5;
    });
;
    //.WithEnvironment("AI__Providers__AzureOpenAI__Endpoint", azureOpenAiEndpoint)
    //.WithEnvironment("AI__Providers__AzureOpenAI__ApiKey", azureOpenAiApiKey)
    //.WithEnvironment("AI__Providers__OpenAI__ApiKey", openAiApiKey);

if (keyVault is not null)
    backend.WithReference(keyVault);

var web = builder.AddProject<Projects.Bethuya_Hybrid_Web>("web")
    .WithReference(backend)
    .WaitFor(backend)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((infra, app) =>
    {
        app.Template.Scale.MinReplicas = 1;
        app.Template.Scale.MaxReplicas = 5;
    });

if (keycloak is not null)
{
    backend.WithReference(keycloak);
    web.WithReference(keycloak);
}

// Scalar API reference — dev only; no Scalar container in Azure.
// AddScalarApiReference() registers a container with boolean env vars that cause
// "Unsupported value type System.Boolean" in ACA's BaseContainerAppContext.ProcessValue.
if (!builder.ExecutionContext.IsPublishMode)
{
    builder.AddScalarApiReference()
        .WithApiReference(backend);
}

builder.Build().Run();
