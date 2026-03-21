var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddAzureSqlServer("sql")
    .RunAsContainer()
    .AddDatabase("BethuyaDb");

var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume();

var backend = builder.AddProject<Projects.Hackmum_Bethuya_Backend>("backend")
    .WithReference(sql)
    .WaitFor(sql)
    .WithReference(keycloak);

var web = builder.AddProject<Projects.Bethuya_Hybrid_Web>("web")
    .WithReference(backend)
    .WaitFor(backend)
    .WithReference(keycloak);

builder.Build().Run();
