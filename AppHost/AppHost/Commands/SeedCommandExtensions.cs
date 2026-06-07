using Aspire.Hosting.ApplicationModel;
using System.Net.Http;

namespace AppHost.Commands;

public static class SeedCommandExtensions
{

    public static IResourceBuilder<ProjectResource> ConfigureSeedCommands(
    this IResourceBuilder<ProjectResource> backend,
    EndpointReference backendHttpsEndpoint)
    {
        backend.WithCommand(
            "seed-curation",
            "Seed curation sandbox",
            async context =>
            {
                try
                {
                    var endpointUrl = await backendHttpsEndpoint
                        .GetValueAsync(context.CancellationToken);

                    if (string.IsNullOrWhiteSpace(endpointUrl))
                    {
                        return CommandResults.Failure(
                            "Backend HTTPS endpoint is unavailable.");
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

        return backend;
    }

}
