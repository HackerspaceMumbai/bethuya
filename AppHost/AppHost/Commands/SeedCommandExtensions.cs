using Aspire.Hosting.ApplicationModel;
using System.Net.Http;
using Bethuya.Hybrid.Shared.Services;
using Refit;

namespace AppHost.Commands;

public static class SeedCommandExtensions
{

    public static IResourceBuilder<ProjectResource> ConfigureSeedCommands(
    this IResourceBuilder<ProjectResource> backend,
    EndpointReference backendHttpEndpoint)
    {
        backend.WithCommand(
            "seed-curation",
            "Seed curation sandbox",
            async context =>
            {
                try
                {
                    var endpointUrl = await backendHttpEndpoint
                        .GetValueAsync(context.CancellationToken);

                    if (string.IsNullOrWhiteSpace(endpointUrl))
                    {
                        return CommandResults.Failure(
                            "Backend HTTP endpoint is unavailable.");
                    }

                    var requestUrl =
                        $"{endpointUrl.TrimEnd('/')}/api/dev/curation/seed?reviewableCount=50";

                    using var httpClient = new HttpClient();

                    Console.WriteLine(
                        $"Seed curation sandbox request URL: {requestUrl}");

                    using var response = await httpClient.PostAsync(
                        requestUrl,
                        content: null,
                        context.CancellationToken);

                    var responseBody = await response.Content
                        .ReadAsStringAsync(context.CancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine(
                            $"Seed curation sandbox response: {responseBody}");

                        return CommandResults.Success();
                    }

                    return CommandResults.Failure(
                        $"Seed request failed. " +
                        $"Status: {(int)response.StatusCode} " +
                        $"({response.ReasonPhrase}). " +
                        $"Response: {responseBody}");
                }
                catch (Exception ex)
                {
                    return CommandResults.Failure(ex);
                }
            },
            new CommandOptions
            {
                Description =
                    "Create a fresh curation sandbox event with ~50 varied reviewable registrants plus fairness and reliability edge cases.",

                ConfirmationMessage =
                    "Generate a new curation sandbox event with seeded registrants?"
            });

        backend.WithCommand(
            "seed-community-simulation",
            "Seed community simulation",
            async context =>
            {
                try
                {
                    var endpointUrl = await backendHttpEndpoint
                        .GetValueAsync(context.CancellationToken);

                    if (string.IsNullOrWhiteSpace(endpointUrl))
                    {
                        return CommandResults.Failure(
                            "Backend HTTP endpoint is unavailable.");
                    }

                    var requestUrl =
                        $"{endpointUrl.TrimEnd('/')}/api/dev/community-simulation/seed";

                    using var httpClient = new HttpClient();

                    // Trust boundary: the endpoint requires RequireOrganizer.
                    // Vikram is the canonical Admin+Organizer dev persona (non-secret catalog key,
                    // Development-only, no real credentials — identical trust model to integration
                    // tests that impersonate Vikram via the same header).
                    httpClient.DefaultRequestHeaders.Add("X-Bethuya-Dev-Persona", "Vikram");

                    Console.WriteLine(
                        $"Seed community simulation request URL: {requestUrl}");

                    using var response = await httpClient.PostAsync(
                        requestUrl,
                        content: null,
                        context.CancellationToken);

                    var responseBody = await response.Content
                        .ReadAsStringAsync(context.CancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine(
                            $"Seed community simulation response: {responseBody}");

                        return CommandResults.Success();
                    }

                    return CommandResults.Failure(
                        $"Seed request failed. " +
                        $"Status: {(int)response.StatusCode} " +
                        $"({response.ReasonPhrase}). " +
                        $"Response: {responseBody}");
                }
                catch (Exception ex)
                {
                    return CommandResults.Failure(ex);
                }
            },
            new CommandOptions
            {
                Description =
                    "Seed the six canonical development personas with community member profiles, linked identities, participation history, and a shared fixture event.",

                ConfirmationMessage =
                    "Seed the community simulation fixtures for the six development personas?"
            });

        backend.WithCommand(
            "seed-community-graph",
            "Seed Community Graph",
            async context =>
            {
                try
                {
                    var endpointUrl = await backendHttpEndpoint.GetValueAsync(context.CancellationToken);
                    if (string.IsNullOrWhiteSpace(endpointUrl))
                        return CommandResults.Failure("Backend HTTP endpoint is unavailable.");

                    // Same Development-only organizer persona boundary as the community simulation command.
                    // Provisioning personas makes the graph visible immediately when switching personas in Web.
                    var api = RestService.For<ICommunityGraphSeedApi>(endpointUrl.TrimEnd('/'));
                    await api.SeedPersonasAsync("Vikram", context.CancellationToken);
                    await api.SeedGraphAsync("Vikram", context.CancellationToken);
                    return CommandResults.Success();
                }
                catch (Exception ex)
                {
                    return CommandResults.Failure(ex);
                }
            },
            new CommandOptions
            {
                Description = "Seed development personas and fictional verified participation for the Community Graph, Passport proof, and opportunity suggestions. Safe to repeat.",
                ConfirmationMessage = "Seed fictional Community Graph fixtures and development personas? Existing records are preserved."
            });

        return backend;
    }

}
