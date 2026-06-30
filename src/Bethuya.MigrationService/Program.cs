using Bethuya.MigrationService;
using Hackmum.Bethuya.Infrastructure.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<BethuyaDbContext>("BethuyaDb");
builder.Services.AddHostedService<MigrationWorker>();

var host = builder.Build();
host.Run();
