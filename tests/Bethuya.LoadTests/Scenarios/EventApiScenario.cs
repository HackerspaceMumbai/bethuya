using System.Text;
using System.Text.Json;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace Bethuya.LoadTests.Scenarios;

/// <summary>
/// NBomber scenario: Event API CRUD under load.
/// Target: p99 &lt; 180ms @ 2,500 RPS.
/// </summary>
public static class EventApiScenario
{
    public static ScenarioProps CreateListScenario(string baseUrl)
    {
        HttpClient? httpClient = null;

        return Scenario.Create("event_list", async context =>
        {
            var request = Http.CreateRequest("GET", $"{baseUrl}/api/events")
                .WithHeader("Accept", "application/json");

            return await Http.Send(httpClient!, request);
        })
        .WithInit(context =>
        {
            httpClient = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            });
            return Task.CompletedTask;
        })
        .WithClean(context =>
        {
            httpClient?.Dispose();
            return Task.CompletedTask;
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.RampingInject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
            Simulation.Inject(rate: 2500, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)),
            Simulation.RampingInject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(5))
        );
    }

    public static ScenarioProps CreatePostScenario(string baseUrl)
    {
        HttpClient? httpClient = null;

        return Scenario.Create("event_create", async context =>
        {
            var body = new
            {
                title = $"Load Test Event {context.ScenarioInfo.InstanceNumber}-{context.InvocationNumber}",
                description = "NBomber load test event",
                type = "Meetup",
                capacity = 50,
                startDate = DateTimeOffset.UtcNow.AddDays(7).ToString("o"),
                endDate = DateTimeOffset.UtcNow.AddDays(7).AddHours(3).ToString("o"),
                location = "Online",
                createdBy = "loadtest@bethuya.dev"
            };

            var json = JsonSerializer.Serialize(body);
            var request = Http.CreateRequest("POST", $"{baseUrl}/api/events")
                .WithHeader("Accept", "application/json")
                .WithHeader("Content-Type", "application/json")
                .WithBody(new StringContent(json, Encoding.UTF8, "application/json"));

            return await Http.Send(httpClient!, request);
        })
        .WithInit(context =>
        {
            httpClient = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            });
            return Task.CompletedTask;
        })
        .WithClean(context =>
        {
            httpClient?.Dispose();
            return Task.CompletedTask;
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.RampingInject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
            Simulation.Inject(rate: 500, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)),
            Simulation.RampingInject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(5))
        );
    }
}
