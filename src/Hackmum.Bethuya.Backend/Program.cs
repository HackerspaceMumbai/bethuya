using System.Text.Json;
using Hackmum.Bethuya.Agents.Extensions;
using Hackmum.Bethuya.AI.Extensions;
using Hackmum.Bethuya.Backend.Agents;
using Hackmum.Bethuya.Backend;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refit;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi();

builder.AddServiceDefaults();
builder.AddBethuyaApiAuthentication();
builder.AddBethuyaAuthorization();
builder.AddBethuyaInfrastructure();
builder.AddBethuyaAI();
builder.AddBethuyaAgents();
builder.Services.Configure<PlannerInvokerOptions>(
    builder.Configuration.GetSection(PlannerInvokerOptions.SectionName));
builder.Services
    .AddRefitClient<IPlannerResponsesApi>(new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    })
    .ConfigureHttpClient((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<PlannerInvokerOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
    })
    .AddStandardResilienceHandler();
builder.Services.AddScoped<IAgentInvoker, FoundryResponsesInvoker>();
builder.Services.AddScoped<InclusionSignalsNormalizer>();
builder.Services.AddScoped<CurationFairnessService>();
builder.Services.AddScoped<PlanningCycleService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await dbContext.EnsurePendingImageUploadSchemaAsync();

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSecurityDefaults();
app.UseBethuyaAuthentication();

app.MapEventEndpoints();
app.MapImageEndpoints();
app.MapRegistrationEndpoints();
app.MapAgentEndpoints();
app.MapCurationEndpoints();
app.MapApprovalEndpoints();
app.MapProfileEndpoints();
app.MapPlanningCycleEndpoints();

app.MapDefaultEndpoints();

app.Run();
