using System.Net;
using System.Net.Http.Json;
using Hackmum.Bethuya.Backend.Agents;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Planning;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Hackmum.Bethuya.Tests.Endpoints;

public sealed class PlanningCycleEndpointValidationTests
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    [Before(Test)]
    public async Task Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<BethuyaDbContext>(options =>
            options
                .UseInMemoryDatabase($"planning-cycle-endpoint-tests-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddScoped<PlanningCycleService>();
        builder.Services.AddSingleton<IAgentInvoker, FakeAgentInvoker>();

        _app = builder.Build();
        _app.MapPlanningCycleEndpoints();
        await _app.StartAsync();

        _client = _app.GetTestClient();
    }

    [After(Test)]
    public async Task Teardown()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    [Test]
    public async Task StartCycle_ReturnsDomainNotFound_ForUnknownEvent()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/planning-cycles/events/{Guid.CreateVersion7()}/start",
            new StartPlanningCycleRequest(RequestedBy: "tester"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("Event not found.");
    }

    private sealed class FakeAgentInvoker : IAgentInvoker
    {
        public Task<PlannerInvocationResult> InvokePlannerAsync(
            PlannerInvocationInput input,
            string conversationId,
            string workItemId,
            string? traceParent,
            string? correlationId,
            CancellationToken ct = default)
        {
            throw new NotSupportedException("Planner invocation is not used by start-cycle endpoint tests.");
        }
    }
}
