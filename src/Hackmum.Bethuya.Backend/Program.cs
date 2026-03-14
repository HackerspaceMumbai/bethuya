using Hackmum.Bethuya.Agents.Extensions;
using Hackmum.Bethuya.AI.Extensions;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBethuyaInfrastructure();
builder.AddBethuyaAI();
builder.AddBethuyaAgents();

var app = builder.Build();

app.UseSecurityDefaults();

app.MapEventEndpoints();
app.MapRegistrationEndpoints();
app.MapAgentEndpoints();
app.MapApprovalEndpoints();

app.MapDefaultEndpoints();

app.Run();
