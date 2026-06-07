using Aspire.Hosting.ApplicationModel;

namespace AppHost.Extensions;

public sealed record AiProviderSettings(
    IResourceBuilder<ParameterResource> FoundryLocalEndpoint,
    IResourceBuilder<ParameterResource> OllamaEndpoint,
    IResourceBuilder<ParameterResource> AzureOpenAiEndpoint,
    IResourceBuilder<ParameterResource> AzureOpenAiKey,
    IResourceBuilder<ParameterResource> AzureOpenAiModel,
    IResourceBuilder<ParameterResource> OpenAiKey,
    IResourceBuilder<ParameterResource> OpenAiModel);

public static class AiProviderExtensions
{
    public static IResourceBuilder<T> ConfigureAiProviders<T>(
        this IResourceBuilder<T> project,
        AiProviderSettings settings)
        where T : ProjectResource
    {
        project
            .WithEnvironment(
                "AI__Providers__FoundryLocal__Endpoint",
                settings.FoundryLocalEndpoint)

            .WithEnvironment(
                "AI__Providers__Ollama__Endpoint",
                settings.OllamaEndpoint)

            .WithEnvironment(
                "AI__Providers__AzureOpenAI__Endpoint",
                settings.AzureOpenAiEndpoint)

            .WithEnvironment(
                "AI__Providers__AzureOpenAI__ApiKey",
                settings.AzureOpenAiKey)

            .WithEnvironment(
                "AI__Providers__AzureOpenAI__ModelId",
                settings.AzureOpenAiModel)

            .WithEnvironment(
                "AI__Providers__OpenAI__ApiKey",
                settings.OpenAiKey)

            .WithEnvironment(
                "AI__Providers__OpenAI__ModelId",
                settings.OpenAiModel);

        return project;
    }
}
