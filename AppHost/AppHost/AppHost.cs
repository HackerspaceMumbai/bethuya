using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddAzureSqlServer("sql")
    .RunAsContainer()
    .AddDatabase("BethuyaDb");

var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume();

var cloudinaryCloudName = builder.AddParameter("cloudinary-cloud-name");
var cloudinaryApiKey = builder.AddParameter("cloudinary-api-key", secret: true);
var cloudinaryApiSecret = builder.AddParameter("cloudinary-api-secret", secret: true);

var backend = builder.AddProject<Projects.Hackmum_Bethuya_Backend>("backend")
    .WithReference(sql)
    .WaitFor(sql)
    .WithReference(keycloak)
    .WithEnvironment("Cloudinary__CloudName", cloudinaryCloudName)
    .WithEnvironment("Cloudinary__ApiKey", cloudinaryApiKey)
    .WithEnvironment("Cloudinary__ApiSecret", cloudinaryApiSecret);

var web = builder.AddProject<Projects.Bethuya_Hybrid_Web>("web")
    .WithReference(backend)
    .WaitFor(backend)
    .WithReference(keycloak);

builder.AddScalarApiReference()
    .WithApiReference(backend);

builder.Build().Run();
