using Hackmum.Bethuya.Backend.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// Verifies the defense-in-depth guard inside <see cref="DevelopmentEndpoints.MapDevelopmentEndpoints"/>:
/// calling it outside the Development environment must throw unconditionally, regardless of what the
/// call-site guard (in Program.cs) does. This ensures a call-site regression cannot accidentally
/// expose the curation seeder or identity diagnostic in production.
/// </summary>
public class DevelopmentEndpointGuardTests : IAsyncDisposable
{
    private readonly List<WebApplication> _apps = [];

    [Test]
    public async Task MapDevelopmentEndpoints_Production_Throws()
    {
        var app = BuildApp(Environments.Production);

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapDevelopmentEndpoints());

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains(nameof(DevelopmentEndpoints.MapDevelopmentEndpoints));
    }

    [Test]
    public async Task MapDevelopmentEndpoints_Staging_Throws()
    {
        var app = BuildApp(Environments.Staging);

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapDevelopmentEndpoints());

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("Staging");
    }

    [Test]
    public async Task MapDevelopmentEndpoints_Development_DoesNotThrow()
    {
        // In Development the guard should pass and routes should be registered normally.
        var app = BuildApp(Environments.Development);

        // No exception expected — just confirm it doesn't throw.
        var registered = true;
        app.MapDevelopmentEndpoints();

        await Assert.That(registered).IsTrue();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var app in _apps)
        {
            await app.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    private WebApplication BuildApp(string environmentName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "None",
            ["Authentication:AllowInsecureDevAuth"] = "true"
        });
        builder.AddBethuyaApiAuthentication();
        builder.AddBethuyaAuthorization();

        var app = builder.Build();
        _apps.Add(app);
        return app;
    }
}
