using System.Text.Json;
using System.Text.Json.Serialization;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Hackmum.Bethuya.Tests.Domain;

public class EventCreationTests
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Test]
    public async Task CreateEventRequest_WithValidData_MapsToEvent()
    {
        // Arrange
        var request = new CreateEventRequest(
            Title: "Community Workshop",
            Description: "Learn about AI agents",
            Type: EventType.Workshop,
            Capacity: 50,
            StartDate: new DateTimeOffset(2026, 6, 15, 14, 0, 0, TimeSpan.Zero),
            EndDate: new DateTimeOffset(2026, 6, 15, 17, 0, 0, TimeSpan.Zero),
            Location: "HackerspaceMumbai",
            CreatedBy: "org@hackmum.org",
            Hashtag: "AIWorkshop",
            CoverImageUrl: "https://res.cloudinary.com/hackmum/image/upload/test.jpg"
        );

        // Act - Map the request to an Event model
        var evt = new Event
        {
            Title = request.Title,
            Description = request.Description,
            Type = request.Type,
            Capacity = request.Capacity,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Location = request.Location,
            CreatedBy = request.CreatedBy,
            Hashtag = request.Hashtag,
            CoverImageUrl = request.CoverImageUrl
        };

        // Assert
        await Assert.That(evt.Title).IsEqualTo(request.Title);
        await Assert.That(evt.Description).IsEqualTo(request.Description);
        await Assert.That(evt.Type).IsEqualTo(request.Type);
        await Assert.That(evt.Capacity).IsEqualTo(request.Capacity);
        await Assert.That(evt.StartDate).IsEqualTo(request.StartDate);
        await Assert.That(evt.EndDate).IsEqualTo(request.EndDate);
        await Assert.That(evt.Location).IsEqualTo(request.Location);
        await Assert.That(evt.CreatedBy).IsEqualTo(request.CreatedBy);
        await Assert.That(evt.Hashtag).IsEqualTo(request.Hashtag);
        await Assert.That(evt.CoverImageUrl).IsEqualTo(request.CoverImageUrl);
        await Assert.That(evt.Status).IsEqualTo(EventStatus.Draft);
        await Assert.That(evt.Id).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task CreateEventRequest_TypeEnum_DeserializesFromString()
    {
        // Arrange
        var json = """
            {
                "title": "Test Event",
                "type": "Meetup",
                "capacity": 50,
                "startDate": "2026-06-15T14:00:00Z",
                "endDate": "2026-06-15T17:00:00Z",
                "createdBy": "test@example.com"
            }
            """;

        // Act
        var request = JsonSerializer.Deserialize<CreateEventRequest>(json, s_jsonOptions);

        // Assert
        await Assert.That(request).IsNotNull();
        await Assert.That(request!.Type).IsEqualTo(EventType.Meetup);
        await Assert.That(request.Title).IsEqualTo("Test Event");
    }

    [Test]
    public async Task CreateEventRequest_AllEventTypes_DeserializeCorrectly()
    {
        // Arrange & Act & Assert for each EventType
        var types = new[]
        {
            ("Workshop", EventType.Workshop),
            ("Meetup", EventType.Meetup),
            ("Hackathon", EventType.Hackathon),
            ("Conference", EventType.Conference),
            ("Panel", EventType.Panel),
            ("Social", EventType.Social)
        };

        foreach (var (jsonValue, expectedEnum) in types)
        {
            var json = $$"""
                {
                    "title": "Test",
                    "type": "{{jsonValue}}",
                    "capacity": 50,
                    "startDate": "2026-06-15T14:00:00Z",
                    "endDate": "2026-06-15T17:00:00Z",
                    "createdBy": "test@example.com"
                }
                """;

            var request = JsonSerializer.Deserialize<CreateEventRequest>(json, s_jsonOptions);

            await Assert.That(request).IsNotNull();
            await Assert.That(request!.Type).IsEqualTo(expectedEnum);
        }
    }

    [Test]
    public async Task Event_WhenCreated_HasDefaultDraftStatus()
    {
        // Arrange & Act
        var evt = new Event
        {
            Title = "New Event",
            CreatedBy = "test@example.com"
        };

        // Assert
        await Assert.That(evt.Status).IsEqualTo(EventStatus.Draft);
    }

    [Test]
    public async Task Event_WhenCreated_HasVersion7Guid()
    {
        // Arrange & Act
        var evt = new Event
        {
            Title = "Test Event",
            CreatedBy = "test@example.com"
        };

        // Assert - Version 7 GUIDs are not empty and have a specific timestamp structure
        await Assert.That(evt.Id).IsNotEqualTo(Guid.Empty);
        
        // Version 7 GUIDs have version bits set to 0111 in the time_hi_and_version field
        var bytes = evt.Id.ToByteArray();
        var versionByte = bytes[7];
        var version = (versionByte >> 4) & 0x0F;
        await Assert.That(version).IsEqualTo(7);
    }

    [Test]
    public async Task Event_Capacity_CanBeSetToValidRange()
    {
        // Arrange & Act
        var evt = new Event
        {
            Title = "Test Event",
            CreatedBy = "test@example.com",
            Capacity = 100
        };

        // Assert
        await Assert.That(evt.Capacity).IsEqualTo(100);
    }
}
