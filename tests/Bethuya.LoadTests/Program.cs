using Bethuya.LoadTests.Scenarios;
using NBomber.CSharp;

// Default target: Aspire-launched backend
var baseUrl = args.Length > 0 ? args[0] : "https://localhost:7092";

Console.WriteLine($"Running load tests against: {baseUrl}");
Console.WriteLine("Ensure Aspire is running: aspire start --project AppHost/AppHost/AppHost.csproj");
Console.WriteLine();

var scenarios = new[]
{
    EventApiScenario.CreateListScenario(baseUrl),
    EventApiScenario.CreatePostScenario(baseUrl),
    ImageUploadScenario.Create(baseUrl)
};

NBomberRunner
    .RegisterScenarios(scenarios)
    .WithReportFolder("reports")
    .Run();
