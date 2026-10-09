using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Hackmum.Bethuya.Tests.Endpoints;

public sealed class RegistrationEndpointTests : IAsyncDisposable
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private IRegistrationRepository _registrationRepository = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _registrationRepository = Substitute.For<IRegistrationRepository>();
        _registrationRepository.CreateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Registration>());

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(_registrationRepository);
        builder.Services.AddSingleton(Substitute.For<IAttendeeProfileRepository>());
        builder.Services.AddScoped<InclusionSignalsNormalizer>();
        builder.Services.AddScoped<CommunityPassportService>();
        builder.Services.AddDbContext<BethuyaDbContext>(options =>
            options.UseInMemoryDatabase($"registration-endpoint-tests-{Guid.NewGuid():N}"));

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapRegistrationEndpoints();
        await _app.StartAsync();

        _client = _app.GetTestClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
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
    public async Task CreateRegistration_TrimsEmailBeforeLinkingMemberAndPersisting()
    {
        Registration? captured = null;
        _registrationRepository.CreateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<Registration>();
                return captured;
            });

        var request = new CreateRegistrationRequest(
            Guid.CreateVersion7(),
            "Passport Tester",
            " passport@example.com ",
            null,
            [],
            "Learn with the community",
            null,
            [],
            null,
            null,
            null);

        var response = await _client.PostAsJsonAsync("/api/registrations", request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Email).IsEqualTo("passport@example.com");
        await Assert.That(captured.CommunityMemberId).IsNotNull();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            Claim[] claims =
            [
                new Claim("sub", "passport-user"),
                new Claim("name", "Passport Tester"),
                new Claim(ClaimTypes.Email, "passport@example.com")
            ];

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
