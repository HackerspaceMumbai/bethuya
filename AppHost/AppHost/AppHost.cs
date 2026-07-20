using AppHost.Commands;
using AppHost.Extensions;
using AppHost.Infrastructure;
using AppHost.Security;
using Azure.Provisioning.KeyVault;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Foundry;
using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(args);
var acaEnv = builder.AddAzureContainerAppEnvironment("bethuya-env");


var enableOnboardingFlowInDevelopment = string.Equals(
    Environment.GetEnvironmentVariable("ONBOARDING_ENABLE_FLOW_IN_DEVELOPMENT")
        ?? builder.ResolveOptional(
            "false",
            "Onboarding:EnableFlowInDevelopment",
            "Parameters:onboarding-enable-flow-in-development",
            "onboarding-enable-flow-in-development"),
    "true",
    StringComparison.OrdinalIgnoreCase);




// TODO:
// Migrate Azure SQL authentication to
// Entra ID + Managed Identity.
// Eliminate SQL passwords in production.

var sql = builder.ConfigureDatabase(acaEnv);
                                            

// Key Vault - provisioned in Azure only; no local emulator exists.
// Only wire it in publish mode (azd up) so local dev starts without Azure credentials.
var keyVault = builder.ConfigureKeyVault();

var cloudinaryCloudName = builder.AddParameter("cloudinary-cloud-name");
var cloudinaryApiKey = builder.AddParameter("cloudinary-api-key", secret: true);
var cloudinaryApiSecret = builder.AddParameter("cloudinary-api-secret", secret: true);

if (keyVault is not null)
{
    keyVault.AddSecret("Cloudinary--CloudName", cloudinaryCloudName);
    keyVault.AddSecret("Cloudinary--ApiKey", cloudinaryApiKey);
    keyVault.AddSecret("Cloudinary--ApiSecret", cloudinaryApiSecret);
}

// Keycloak - local dev only; not published to Azure.
// The preview Keycloak package sets KC_HEALTH_ENABLED=true (boolean literal) in its container
// env vars. ACA's BaseContainerAppContext.ProcessValue cannot handle booleans, causing
// "Unsupported value type System.Boolean" during aspire deploy. In production, use Entra ID.
IResourceBuilder<KeycloakResource>? keycloak = null;
if (builder.IsLocalDevelopment())
{
    keycloak = builder.AddKeycloak("keycloak", 8081)
        .WithDataVolume();
}

IResourceBuilder<ProjectResource>? plannerHosted = null;

var foundry = builder.AddFoundry("bethuya-foundry");

#pragma warning disable ASPIRECOMPUTE003

    var foundryProject = foundry.AddProject("bethuyaProject");
#pragma warning restore

    var plannerChatModel =
        foundryProject.AddModelDeployment(
            "plannerChat",
            FoundryModel.OpenAI.Gpt41);

    plannerHosted = builder
        .AddProject<Projects.Hackmum_Bethuya_Agents_Planner_Hosted>(
            "planner-hosted", launchProfileName: null)
        .WithReference(foundryProject)
        .WithReference(plannerChatModel)
        .WaitFor(plannerChatModel)
        .AsHostedAgent(foundryProject);


// Migration service - runs EF Core migrations then exits.
// Deployed as a Container App Job (not a long-running Container App) so ACA doesn't restart it.
// The job is triggered automatically via the azure.yaml postdeploy hook after each deploy.
// IMPORTANT: run `dotnet ef migrations add <Name> --project src/Hackmum.Bethuya.Infrastructure --startup-project src/Bethuya.MigrationService`
// for any DbContext schema changes before deploying.
   var migrationService = builder.AddProject<Projects.Bethuya_MigrationService>("migration-service")
         .WithReference(sql)
         .WaitFor(sql)
         .PublishAsAzureContainerAppJob()
         .WithComputeEnvironment(acaEnv);



var shouldBypassOnboardingInCurrentEnvironment =
    builder.ShouldEnableOnboardingBypass(
        enableOnboardingFlowInDevelopment);

var onboardingBypassSocialConnections = shouldBypassOnboardingInCurrentEnvironment ? "true" : "false";
var onboardingBypassMandatoryProfile = shouldBypassOnboardingInCurrentEnvironment ? "true" : "false";

builder.EnforceProductionSecurityPolicies(
    shouldBypassOnboardingInCurrentEnvironment);


var backend = builder.AddProject<Projects.Hackmum_Bethuya_Backend>("backend", launchProfileName: null)
    .WithHttpEndpoint(port: 8080, targetPort: 8080, isProxied: false).WithReference(sql)
    .WithEnvironment("ASPNETCORE_PREVENTHOSTINGSTARTUP", "true")
    .WaitFor(sql)
    .WaitForCompletion(migrationService)
    .WithEnvironment("Onboarding__BypassMandatoryProfile", onboardingBypassMandatoryProfile)
    .WithHttpHealthCheck("/health")
//  .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:8080")
    .PublishAsAzureContainerApp((infra, app) =>
    {
        app.Template.Scale.MinReplicas = 1;
        app.Template.Scale.MaxReplicas = 5;
    })
    .WithComputeEnvironment(acaEnv);



if (plannerHosted is not null)
{
    backend
        .WithReference(plannerHosted)
        .WaitFor(plannerHosted);
}


var backendHttpEndpoint = backend.GetEndpoint("http");

if (builder.IsLocalDevelopment())
{
    backend
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
        .WithEnvironment("Cloudinary__CloudName", cloudinaryCloudName)
        .WithEnvironment("Cloudinary__ApiKey", cloudinaryApiKey)
        .WithEnvironment("Cloudinary__ApiSecret", cloudinaryApiSecret);
    backend.ConfigureSeedCommands(backendHttpEndpoint);
}


if (keyVault is not null)
{
    backend
        .WithReference(keyVault)
        .WithRoleAssignments(keyVault, KeyVaultBuiltInRole.KeyVaultSecretsUser);
}

var webStaticAssetsManifest = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory,
    "..",
    "..",
    "..",
    "..",
    "..",
    "src",
    "Bethuya.Hybrid",
    "Bethuya.Hybrid.Web",
    "obj",
    "Debug",
    "net10.0",
    "staticwebassets.development.json"));

var web = builder.AddProject<Projects.Bethuya_Hybrid_Web>("web", launchProfileName: null)
    .WithReference(backend)
    .WaitFor(backend)
    .WithHttpEndpoint()
    //   .WithHttpsEndpoint()
    .WithEnvironment("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true")
    .WithEnvironment("Onboarding__BypassSocialConnections", onboardingBypassSocialConnections)
    .WithEnvironment("Onboarding__BypassMandatoryProfile", onboardingBypassMandatoryProfile)
//  .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:8082")
    .WithEnvironment("ASPNETCORE_ALLOWEDHOSTS", "*")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithComputeEnvironment(acaEnv)
    .PublishAsAzureContainerApp((infra, app) =>
    {
        app.Template.Scale.MinReplicas = 1;
        app.Template.Scale.MaxReplicas = 5;
    });

if (builder.IsLocalDevelopment())
{
    web
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
        .WithEnvironment("ASPNETCORE_STATICWEBASSETS", webStaticAssetsManifest);
}

if (keyVault is not null)
{
    web
        .WithReference(keyVault)
        .WithRoleAssignments(keyVault, KeyVaultBuiltInRole.KeyVaultSecretsUser);
}

if (keycloak is not null)
{
    backend.WithReference(keycloak);
    web.WithReference(keycloak);
}

// Scalar API reference - dev only; no Scalar container in Azure.
// AddScalarApiReference() registers a container with boolean env vars that cause
// "Unsupported value type System.Boolean" in ACA's BaseContainerAppContext.ProcessValue.
if (builder.IsLocalDevelopment())
{
    builder.AddScalarApiReference()
        .WithApiReference(backend);
}

builder.Build().Run();
