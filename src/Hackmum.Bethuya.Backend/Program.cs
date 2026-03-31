using Hackmum.Bethuya.Agents.Extensions;
using Hackmum.Bethuya.AI.Extensions;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSecurityDefaults();
app.UseBethuyaAuthentication();

app.MapEventEndpoints();
app.MapImageEndpoints();
app.MapRegistrationEndpoints();
app.MapAgentEndpoints();
app.MapApprovalEndpoints();

app.MapDefaultEndpoints();

app.Run();
