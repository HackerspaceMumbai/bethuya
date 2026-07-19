using System.Net;
using System.Net.Http.Json;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Hackmum.Bethuya.Tests.Endpoints;

public class ImageEndpointValidationTests : IAsyncDisposable
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private IImageUploadService _mockUploadService = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _mockUploadService = Substitute.For<IImageUploadService>();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_mockUploadService);

        _app = builder.Build();
        _app.MapImageEndpoints();
        await _app.StartAsync();

        _client = _app.GetTestClient();
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

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    [Test]
    public async Task CreateSession_RejectsMissingFileMetadata()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/images/direct-upload/session",
            new ImageEndpoints.CreateDirectUploadSessionRequest("", "", 0));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateSession_ReturnsSignedUploadSession()
    {
        var session = new DirectImageUploadSession(
            "https://api.cloudinary.com/v1_1/demo/image/upload",
            "demo",
            "key123",
            "bethuya/events/pending/test-cover",
            1_715_000_000,
            "signed-value",
            "delete-token",
            "signed-preset",
            5 * 1024 * 1024,
            "jpg,jpeg,png,gif,webp");

        _mockUploadService
            .CreateDirectUploadSessionAsync("cover.png", "image/png", 2048, Arg.Any<CancellationToken>())
            .Returns(session);

        var response = await _client.PostAsJsonAsync(
            "/api/images/direct-upload/session",
            new ImageEndpoints.CreateDirectUploadSessionRequest("cover.png", "image/png", 2048));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ImageEndpoints.DirectUploadSessionResponse>();
        await Assert.That(payload).IsNotNull();
        await Assert.That(payload!.PublicId).IsEqualTo(session.PublicId);
        await Assert.That(payload.Signature).IsEqualTo(session.Signature);
        await Assert.That(payload.DeleteToken).IsEqualTo(session.DeleteToken);
    }

    [Test]
    public async Task CreateSession_MapsArgumentErrorsToValidationProblem()
    {
        _mockUploadService
            .CreateDirectUploadSessionAsync("cover.bmp", "image/bmp", 1024, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DirectImageUploadSession>(
                new ArgumentException("Only JPEG, PNG, WebP, and GIF images are accepted.", "contentType")));

        var response = await _client.PostAsJsonAsync(
            "/api/images/direct-upload/session",
            new ImageEndpoints.CreateDirectUploadSessionRequest("cover.bmp", "image/bmp", 1024));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateSession_MapsMissingProviderConfigurationToUnavailable()
    {
        _mockUploadService
            .CreateDirectUploadSessionAsync("cover.png", "image/png", 2048, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DirectImageUploadSession>(
                new InvalidOperationException("Cloudinary image uploads are not configured.")));

        var response = await _client.PostAsJsonAsync(
            "/api/images/direct-upload/session",
            new ImageEndpoints.CreateDirectUploadSessionRequest("cover.png", "image/png", 2048));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task DeletePendingUpload_ReturnsNoContent_WhenDeleteTokenMatches()
    {
        _mockUploadService
            .DeletePendingUploadAsync("bethuya/events/pending/test-cover", "delete-token", Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.PostAsJsonAsync(
            "/api/images/direct-upload/delete",
            new ImageEndpoints.DeletePendingDirectUploadRequest("bethuya/events/pending/test-cover", "delete-token"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task DeletePendingUpload_ReturnsNotFound_WhenDeleteTokenDoesNotMatch()
    {
        _mockUploadService
            .DeletePendingUploadAsync("bethuya/events/pending/test-cover", "wrong-token", Arg.Any<CancellationToken>())
            .Returns(false);

        var response = await _client.PostAsJsonAsync(
            "/api/images/direct-upload/delete",
            new ImageEndpoints.DeletePendingDirectUploadRequest("bethuya/events/pending/test-cover", "wrong-token"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
