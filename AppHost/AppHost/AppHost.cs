var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddAzureSqlServer("sql")
    .RunAsContainer()
    .AddDatabase("BethuyaDb");

var backend = builder.AddProject<Projects.Hackmum_Bethuya_Backend>("backend")
    .WithReference(sql)
    .WaitFor(sql);

var web = builder.AddProject<Projects.Bethuya_Hybrid_Web>("web")
    .WithReference(backend)
    .WaitFor(backend);

builder.Build().Run();
