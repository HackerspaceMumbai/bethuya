using Bethuya.MigrationService;
using Hackmum.Bethuya.Infrastructure.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<BethuyaDbContext>("BethuyaDb");
builder.Services.AddHostedService<MigrationWorker>();

var host = builder.Build();
host.Run();
