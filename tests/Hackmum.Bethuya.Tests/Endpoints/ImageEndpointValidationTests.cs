using System.Net;
using System.Net.Http.Headers;
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
    public async Task Upload_RejectsOversizedFile()
    {
        // 6 MB exceeds the 5 MB limit
        using var content = CreateMultipartFile(
            sizeBytes: 6 * 1024 * 1024,
            contentType: "image/jpeg",
            fileName: "large.jpg");

        var response = await _client.PostAsync("/api/images/upload", content);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Upload_RejectsInvalidContentType()
    {
        using var content = CreateMultipartFile(
            sizeBytes: 1024,
            contentType: "application/pdf",
            fileName: "document.pdf");

        var response = await _client.PostAsync("/api/images/upload", content);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [Arguments("image/jpeg", "photo.jpg")]
    [Arguments("image/png", "icon.png")]
    [Arguments("image/webp", "banner.webp")]
    [Arguments("image/gif", "animation.gif")]
    public async Task Upload_AcceptsValidImageTypes(string contentType, string fileName)
    {
        var expectedUrl = $"https://res.cloudinary.com/demo/image/upload/v1/bethuya/events/{fileName}";
        _mockUploadService
            .UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expectedUrl);

        using var content = CreateMultipartFile(
            sizeBytes: 2048,
            contentType: contentType,
            fileName: fileName);

        var response = await _client.PostAsync("/api/images/upload", content);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Upload_ReturnsUrlFromUploadService()
    {
        const string expectedUrl = "https://res.cloudinary.com/demo/image/upload/v1/bethuya/events/cover.jpg";
        _mockUploadService
            .UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expectedUrl);

        using var content = CreateMultipartFile(
            sizeBytes: 4096,
            contentType: "image/jpeg",
            fileName: "cover.jpg");

        var response = await _client.PostAsync("/api/images/upload", content);
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains(expectedUrl);
    }

    [Test]
    public async Task Upload_FileAtExactSizeLimit_IsAccepted()
    {
        // Exactly 5 MB — should pass
        _mockUploadService
            .UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://res.cloudinary.com/demo/image/upload/v1/bethuya/events/max.jpg");

        using var content = CreateMultipartFile(
            sizeBytes: 5 * 1024 * 1024,
            contentType: "image/png",
            fileName: "max.png");

        var response = await _client.PostAsync("/api/images/upload", content);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Upload_FileOneByteOverLimit_IsRejected()
    {
        // 5 MB + 1 byte — should fail
        using var content = CreateMultipartFile(
            sizeBytes: 5 * 1024 * 1024 + 1,
            contentType: "image/jpeg",
            fileName: "oversize.jpg");

        var response = await _client.PostAsync("/api/images/upload", content);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Upload_CallsUploadServiceWithFileName()
    {
        _mockUploadService
            .UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://res.cloudinary.com/demo/image/upload/v1/bethuya/events/event-cover.jpg");

        using var content = CreateMultipartFile(
            sizeBytes: 1024,
            contentType: "image/jpeg",
            fileName: "event-cover.jpg");

        await _client.PostAsync("/api/images/upload", content);

        await _mockUploadService.Received(1)
            .UploadAsync(Arg.Any<Stream>(), Arg.Is("event-cover.jpg"), Arg.Any<CancellationToken>());
    }

    private static MultipartFormDataContent CreateMultipartFile(int sizeBytes, string contentType, string fileName)
    {
        var fileBytes = new byte[sizeBytes];
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", fileName);
        return form;
    }
}
