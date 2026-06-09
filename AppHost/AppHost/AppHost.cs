using AppHost.Commands;
using AppHost.Extensions;
using AppHost.Security;
using AppHost.Infrastructure;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Foundry;
using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(args);
var acaEnv = builder.AddAzureContainerAppEnvironment("bethuya-env");
const int webHttpsPort = 7400;
const int webHttpPort = 5095;
const string gitHubCallbackPath = "/oauth/github/callback";
const string linkedInCallbackPath = "/oauth/linkedin/callback";

var gitHubClientId = builder.ResolveRequired(
    "SocialConnections:GitHub:ClientId",
    "Parameters:oauth-github-clientid",
    "oauth-github-clientid");
var gitHubClientSecret = builder.ResolveRequired(
    "SocialConnections:GitHub:ClientSecret",
    "Parameters:oauth-github-clientsecret",
    "oauth-github-clientsecret");
var linkedInClientId = builder.ResolveRequired(
    "SocialConnections:LinkedIn:ClientId",
    "Parameters:oauth-linkedin-clientid",
    "oauth-linkedin-clientid");
var linkedInClientSecret = builder.ResolveRequired(
    "SocialConnections:LinkedIn:ClientSecret",
    "Parameters:oauth-linkedin-clientsecret",
    "oauth-linkedin-clientsecret");
var linkedInScope0 = builder.ResolveOptional(
    "openid",
    "SocialConnections:LinkedIn:Scopes:0",
    "Parameters:oauth-linkedin-scope-0",
    "oauth-linkedin-scope-0");
var linkedInScope1 = builder.ResolveOptional(
    "profile",
    "SocialConnections:LinkedIn:Scopes:1",
    "Parameters:oauth-linkedin-scope-1",
    "oauth-linkedin-scope-1");

builder.ValidateRequired(
    gitHubClientId,
    "SocialConnections:GitHub:ClientId");

builder.ValidateRequired(
    gitHubClientSecret,
    "SocialConnections:GitHub:ClientSecret");

builder.ValidateRequired(
    linkedInClientId,
    "SocialConnections:LinkedIn:ClientId");

builder.ValidateRequired(
    linkedInClientSecret,
    "SocialConnections:LinkedIn:ClientSecret");

var socialAuthSettings = new SocialAuthSettings(
    gitHubClientId,
    gitHubClientSecret,
    gitHubCallbackPath,
    linkedInClientId,
    linkedInClientSecret,
    linkedInCallbackPath,
    linkedInScope0,
    linkedInScope1);


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

var sql = builder.ConfigureDatabase();

// Key Vault - provisioned in Azure only; no local emulator exists.
// Only wire it in publish mode (azd up) so local dev starts without Azure credentials.
var keyVault = builder.ConfigureKeyVault();

// Keycloak - local dev only; not published to Azure.
// The preview Keycloak package sets KC_HEALTH_ENABLED=true (boolean literal) in its container
// env vars. ACA's BaseContainerAppContext.ProcessValue cannot handle booleans, causing
// "Unsupported value type System.Boolean" during aspire deploy. In production, use Entra ID.
IResourceBuilder<KeycloakResource>? keycloak = null;
if (builder.IsLocalDevelopment())
{
    keycloak = builder.AddKeycloak("keycloak", 8080)
        .WithDataVolume();
}

// Cloudinary - image upload for event cover images
// secret: true omitted - causes "Unsupported value type System.Boolean" in Azure deployment.
// For production: route secrets through Key Vault via keyVault.AddSecret(...) in publish mode.
var cloudinaryCloudName = builder.AddParameter("cloudinary-cloud-name");
var cloudinaryApiKey = builder.AddParameter("cloudinary-api-key");
var cloudinaryApiSecret = builder.AddParameter("cloudinary-api-secret");

// AI provider configuration - configure via user-secrets (dev) or Key Vault (Azure).
// Local providers default in AppHost/appsettings.json; cloud keys must be set via user-secrets.
// FoundryLocal endpoint can be supplied via environment variable AI_FOUNDRYLOCAL_ENDPOINT or parameter.
// Example: $env:AI_FOUNDRYLOCAL_ENDPOINT = "http://127.0.0.1:55950"; aspire start
// Dev: dotnet user-secrets set "Parameters:ai-azure-openai-endpoint" "https://YOUR-RESOURCE.openai.azure.com/"
// Dev: dotnet user-secrets set "Parameters:ai-azure-openai-key" "<key>"
// Dev: dotnet user-secrets set "Parameters:ai-openai-key" "<key>"

// FoundryLocal endpoint: accept from environment variable or parameter with fallback to appsettings
var aiFoundryLocalEndpointValue = Environment.GetEnvironmentVariable("AI_FOUNDRYLOCAL_ENDPOINT")
    ?? builder.Configuration["Parameters:ai-foundrylocal-endpoint"]
    ?? "http://localhost:5272"; // Fallback to default

// TODO:
// Replace Azure OpenAI API key usage with
// Managed Identity + DefaultAzureCredential.
// Target architecture:
//
// ACA Managed Identity
//   -> Azure RBAC
//   -> Azure OpenAI

var aiFoundryLocalEndpoint = builder.AddParameter("ai-foundrylocal-endpoint", aiFoundryLocalEndpointValue);
var aiOllamaEndpoint = builder.AddParameter("ai-ollama-endpoint", 
    Environment.GetEnvironmentVariable("AI_OLLAMA_ENDPOINT") ?? "http://localhost:11434");
var aiAzureOpenAiEndpoint = builder.AddParameter("ai-azure-openai-endpoint");
var aiAzureOpenAiKey = builder.AddParameter("ai-azure-openai-key");
var aiAzureOpenAiModel = builder.AddParameter("ai-azure-openai-model");
var aiOpenAiKey = builder.AddParameter("ai-openai-key");
var aiOpenAiModel = builder.AddParameter("ai-openai-model");

var aiProviderSettings = new AiProviderSettings(
    aiFoundryLocalEndpoint,
    aiOllamaEndpoint,
    aiAzureOpenAiEndpoint,
    aiAzureOpenAiKey,
    aiAzureOpenAiModel,
    aiOpenAiKey,
    aiOpenAiModel);

var foundry = builder.AddFoundry("bethuya-foundry");

#pragma warning disable ASPIRECOMPUTE003
var foundryProject = foundry.AddProject("bethuya-project");
#pragma warning restore ASPIRECOMPUTE003

if (builder.IsCloudDeployment())
{
    var registry = builder.AddAzureContainerRegistry("acr");

#pragma warning disable ASPIRECOMPUTE003
    foundryProject.WithContainerRegistry(registry);
#pragma warning restore ASPIRECOMPUTE003
}

var plannerChatModel = foundryProject.AddModelDeployment(
    "planner-chat",
    FoundryModel.OpenAI.Gpt41);


var plannerHosted = builder
    .AddProject<Projects.Hackmum_Bethuya_Agents_Planner_Hosted>(
        "planner-hosted")
    .WithHttpEndpoint(targetPort: 8088)
    .WithReference(foundryProject)
    .WithReference(plannerChatModel)
    .WaitFor(plannerChatModel)
    .AsHostedAgent(foundryProject);

// Migration service - runs EF Core migrations then exits; backend waits for it to complete.
// IMPORTANT: run `dotnet ef migrations add InitialCreate --project src/Hackmum.Bethuya.Infrastructure`
// before the first Azure deployment.
var migrationService = builder.AddProject<Projects.Bethuya_MigrationService>("migration-service")
    .WithReference(sql)
    .WaitFor(sql);

var shouldBypassOnboardingInCurrentEnvironment =
    builder.ShouldEnableOnboardingBypass(
        enableOnboardingFlowInDevelopment);

var onboardingBypassSocialConnections = shouldBypassOnboardingInCurrentEnvironment ? "true" : "false";
var onboardingBypassMandatoryProfile = shouldBypassOnboardingInCurrentEnvironment ? "true" : "false";

builder.EnforceProductionSecurityPolicies(
    shouldBypassOnboardingInCurrentEnvironment);


var backend = builder.AddProject<Projects.Hackmum_Bethuya_Backend>("backend")
    .WithReference(sql)
    .WithReference(plannerHosted)
    .WaitFor(sql)
    .WaitFor(plannerHosted)
    .WaitForCompletion(migrationService)
    .WithEnvironment("Cloudinary__CloudName", cloudinaryCloudName)
    .WithEnvironment("Cloudinary__ApiKey", cloudinaryApiKey)
    .WithEnvironment("Cloudinary__ApiSecret", cloudinaryApiSecret)
    .ConfigureAiProviders(aiProviderSettings)
    .WithEnvironment("Onboarding__BypassMandatoryProfile", onboardingBypassMandatoryProfile)
    .WithHttpHealthCheck("/health")
    .PublishAsAzureContainerApp((infra, app) =>
    {
        app.Template.Scale.MinReplicas = 1;
        app.Template.Scale.MaxReplicas = 5;
    })
    .WithComputeEnvironment(acaEnv);

var backendHttpsEndpoint = backend.GetEndpoint("https");


if (builder.IsLocalDevelopment())
{
    backend.ConfigureSeedCommands(backendHttpsEndpoint);
}

if (keyVault is not null)
    backend.WithReference(keyVault);

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
    .WithHttpEndpoint(port: webHttpPort)
    .WithHttpsEndpoint(port: webHttpsPort)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("Onboarding__BypassSocialConnections", onboardingBypassSocialConnections)
    .WithEnvironment("Onboarding__BypassMandatoryProfile", onboardingBypassMandatoryProfile)
    .WithEnvironment("ASPNETCORE_STATICWEBASSETS", webStaticAssetsManifest)
    .ConfigureSocialAuth(socialAuthSettings)
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

// Scalar API reference - dev only; no Scalar container in Azure.
// AddScalarApiReference() registers a container with boolean env vars that cause
// "Unsupported value type System.Boolean" in ACA's BaseContainerAppContext.ProcessValue.
if (builder.IsLocalDevelopment())
{
    builder.AddScalarApiReference()
        .WithApiReference(backend);
}

builder.Build().Run();
