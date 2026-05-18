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
    Task<EventDto> PlanAsync([Body] PlanEventDto request, CancellationToken ct = default);

    [Put("/api/events/{id}")]
    Task<EventDto> UpdateAsync(Guid id, [Body] UpdateEventDto request, CancellationToken ct = default);

    [Post("/api/images/direct-upload/session")]
    Task<DirectImageUploadSessionResponse> CreateDirectImageUploadSessionAsync(
        [Body] CreateDirectImageUploadSessionRequest request,
        CancellationToken ct = default);

    [Post("/api/images/direct-upload/delete")]
    Task DeletePendingImageUploadAsync([Body] DeletePendingImageUploadRequest request, CancellationToken ct = default);

    [Post("/api/agents/recommend-dates")]
    Task<RecommendDatesResponse> RecommendDatesAsync([Body] RecommendDatesRequest request, CancellationToken ct = default);
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

/// <summary>Payload sent to plan a new event.</summary>
public sealed record PlanEventDto(
    string Title,
    string? Description,
    string Type,
    int Capacity,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Location,
    string CreatedBy,
    string? Hashtag,
    string? CoverImageUrl,
    string Status = "Draft");

/// <summary>Payload sent to update an existing event.</summary>
public sealed record UpdateEventDto(
    string Title,
    string? Description,
    string Type,
    int Capacity,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Location,
    string Status,
    string? CoverImageUrl);

/// <summary>Request for a signed Cloudinary direct-upload session.</summary>
public sealed record CreateDirectImageUploadSessionRequest(
    string FileName,
    string ContentType,
    long FileSize);

/// <summary>Response containing the signed parameters required for direct upload.</summary>
public sealed record DirectImageUploadSessionResponse(
    [property: JsonPropertyName("uploadUrl")] string UploadUrl,
    [property: JsonPropertyName("cloudName")] string CloudName,
    [property: JsonPropertyName("apiKey")] string ApiKey,
    [property: JsonPropertyName("publicId")] string PublicId,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("signature")] string Signature,
    [property: JsonPropertyName("deleteToken")] string DeleteToken,
    [property: JsonPropertyName("uploadPreset")] string? UploadPreset,
    [property: JsonPropertyName("maxFileSize")] long MaxFileSize,
    [property: JsonPropertyName("allowedFormats")] string AllowedFormats);

/// <summary>Deletes a pending image upload that has not yet been attached to a saved event.</summary>
public sealed record DeletePendingImageUploadRequest(
    string PublicId,
    string DeleteToken);

/// <summary>Request to recommend optimal event dates via AI.</summary>
public sealed record RecommendDatesRequest(
    string? Title = null,
    string? Type = null,
    string? Description = null,
    string? Location = null,
    int? Capacity = null);

/// <summary>AI-recommended date and time for an event.</summary>
public sealed record RecommendDatesResponse(
    string StartDate,
    string StartTime,
    string EndDate,
    string EndTime,
    string? Reasoning = null);
