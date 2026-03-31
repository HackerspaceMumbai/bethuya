using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Refit;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Refit-generated typed HTTP client for the Bethuya Events API.</summary>
public interface IEventApi
{
    [Get("/api/events")]
    Task<List<EventDto>> GetAllAsync(CancellationToken ct = default);

    [Get("/api/events/{id}")]
    Task<EventDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    [Get("/api/events/slug/{hashtag}")]
    Task<EventDto> GetByHashtagAsync(string hashtag, CancellationToken ct = default);

    [Post("/api/events")]
    Task<EventDto> CreateAsync([Body] CreateEventDto request, CancellationToken ct = default);

    [Multipart]
    [Post("/api/images/upload")]
    Task<ImageUploadResponse> UploadImageAsync([AliasAs("file")] StreamPart file, CancellationToken ct = default);
}

/// <summary>Event data returned from the API.</summary>
public sealed record EventDto(
    Guid Id,
    string Title,
    string? Description,
    string Type,
    string Status,
    int Capacity,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Location,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? Hashtag,
    string? CoverImageUrl);

/// <summary>Payload sent to create a new event draft.</summary>
public sealed record CreateEventDto(
    string Title,
    string? Description,
    string Type,
    int Capacity,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Location,
    string CreatedBy,
    string? Hashtag,
    string? CoverImageUrl);

/// <summary>Response from the image upload endpoint.</summary>
public sealed record ImageUploadResponse([property: JsonPropertyName("url")] string Url);
