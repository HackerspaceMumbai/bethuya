using System.Net;
using System.Net.Http.Json;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Hackmum.Bethuya.Tests.Endpoints;

public sealed class EventEndpointValidationTests : IAsyncDisposable
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private IEventRepository _eventRepository = null!;
    private IImageUploadService _imageUploadService = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _eventRepository = Substitute.For<IEventRepository>();
        _imageUploadService = Substitute.For<IImageUploadService>();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_eventRepository);
        builder.Services.AddSingleton(_imageUploadService);
        builder.Services.AddDbContext<BethuyaDbContext>(options =>
            options
                .UseInMemoryDatabase($"event-endpoint-tests-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        _app = builder.Build();
        _app.MapEventEndpoints();
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
    public async Task CreateEvent_RejectsCoverUrlWhenPendingAssetDoesNotExistInCloudinary()
    {
        const string coverUrl = "https://res.cloudinary.com/demo/image/upload/v1715000000/bethuya/events/pending/test-cover.png";
        const string publicId = "bethuya/events/pending/test-cover";

        _imageUploadService.TryGetPublicId(coverUrl, out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = publicId;
                return true;
            });
        _imageUploadService.IsPendingUploadAsync(publicId, Arg.Any<CancellationToken>()).Returns(true);
        _imageUploadService.UploadedAssetExistsAsync(publicId, Arg.Any<CancellationToken>()).Returns(false);

        var response = await _client.PostAsJsonAsync("/api/events", CreateRequest(coverUrl));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await _eventRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Test]
    public async Task CreateEvent_AttachesCoverAfterSuccessfulValidation()
    {
        const string coverUrl = "https://res.cloudinary.com/demo/image/upload/v1715000000/bethuya/events/pending/test-cover.png";
        const string publicId = "bethuya/events/pending/test-cover";

        _imageUploadService.TryGetPublicId(coverUrl, out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = publicId;
                return true;
            });
        _imageUploadService.IsPendingUploadAsync(publicId, Arg.Any<CancellationToken>()).Returns(true);
        _imageUploadService.UploadedAssetExistsAsync(publicId, Arg.Any<CancellationToken>()).Returns(true);
        _eventRepository.CreateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Event>());

        var response = await _client.PostAsJsonAsync("/api/events", CreateRequest(coverUrl));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await _imageUploadService.Received(1).MarkUploadAttachedAsync(publicId, Arg.Any<CancellationToken>());
    }

    private static PlanEventRequest CreateRequest(string? coverImageUrl) =>
        new(
            Title: "Direct cover upload event",
            Description: "Validates Cloudinary-backed cover saves.",
            Type: EventType.Meetup,
            Status: EventStatus.Draft,
            Capacity: 42,
            StartDate: new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero),
            EndDate: new DateTimeOffset(2026, 6, 1, 21, 0, 0, TimeSpan.Zero),
            Location: "Hackerspace",
            CreatedBy: "organizer",
            Hashtag: "directcover",
            CoverImageUrl: coverImageUrl);
}
