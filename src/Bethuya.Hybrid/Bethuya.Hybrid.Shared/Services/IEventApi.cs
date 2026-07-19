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

    [Get("/api/events/{id}/fairness-targets")]
    Task<EventFairnessTargetsDto> GetFairnessTargetsAsync(Guid id, CancellationToken ct = default);

    [Put("/api/events/{id}/fairness-targets")]
    Task<EventFairnessTargetsDto> UpdateFairnessTargetsAsync(Guid id, [Body] EventFairnessTargetsDto request, CancellationToken ct = default);

    [Get("/api/events/{id}/sessionize/preview")]
    Task<List<NormalizedSessionDto>> PreviewSessionizeAsync(Guid id, CancellationToken ct = default);

    [Post("/api/events/{id}/sessionize/import")]
    Task<SessionizeImportResponseDto> ImportSessionizeAsync(Guid id, CancellationToken ct = default);

    [Post("/api/events/{id}/lifecycle")]
    Task<EventLifecycleOperationDto> TransitionLifecycleAsync(Guid id, [Body] EventLifecycleTransitionDto request, CancellationToken ct = default);

    [Post("/api/events/{id}/publish")]
    Task<EventLifecycleOperationDto> PublishAsync(Guid id, [Body] PublishEventDto request, CancellationToken ct = default);

    [Post("/api/events/{id}/schedule-alterations")]
    Task<EventLifecycleOperationDto> AlterScheduleAsync(Guid id, [Body] ScheduleAlterationDto request, CancellationToken ct = default);

    [Post("/api/events/{id}/complete")]
    Task<EventLifecycleOperationDto> CompleteAsync(Guid id, [Body] CompleteEventDto request, CancellationToken ct = default);

    [Post("/api/events/{id}/archive")]
    Task<EventLifecycleOperationDto> ArchiveAsync(Guid id, [Body] ArchiveEventDto request, CancellationToken ct = default);

    [Post("/api/agents/recommend-dates")]
    Task<RecommendDatesResponse> RecommendDatesAsync([Body] RecommendDatesRequest request, CancellationToken ct = default);
}

/// <summary>Refit-generated typed HTTP client for image upload endpoints.</summary>
public interface IImageUploadApi
{
    [Post("/api/images/direct-upload/session")]
    Task<DirectImageUploadSessionResponse> CreateDirectImageUploadSessionAsync(
        [Body] CreateDirectImageUploadSessionRequest request,
        CancellationToken ct = default);

    [Post("/api/images/direct-upload/delete")]
    Task DeletePendingImageUploadAsync([Body] DeletePendingImageUploadRequest request, CancellationToken ct = default);
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
    string? CoverImageUrl,
    string LifecycleState,
    string? SessionizeEventId,
    string? GitHubFolderUrl,
    string? TeamsAnnouncementMessageId,
    string? RegistrationUrl,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ArchivedAt,
    EventFairnessTargetsDto FairnessTargets);

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
    string? SessionizeEventId = null,
    string? GitHubFolderUrl = null,
    string? TeamsAnnouncementMessageId = null,
    string? RegistrationUrl = null,
    string Status = "Draft",
    string LifecycleState = "Drafted",
    EventFairnessTargetsDto? FairnessTargets = null);

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
    string? CoverImageUrl,
    string? SessionizeEventId = null,
    string? GitHubFolderUrl = null,
    string? TeamsAnnouncementMessageId = null,
    string? RegistrationUrl = null,
    EventFairnessTargetsDto? FairnessTargets = null);

public sealed record EventFairnessTargetsDto(
    double GeoOutsideDominantMinPercent = 0.35,
    double LocalLanguageMinPercent = 0.25,
    double UnderrepresentedEducationMinPercent = 0.25,
    bool EnableSocioeconomicDimension = false,
    double? UnderrepresentedSocioeconomicMinPercent = null,
    int KAnonymityThreshold = 5,
    double GenderDiversityMinPercent = 0.40);

public sealed record EventLifecycleTransitionDto(
    string TargetState,
    string Actor);

public sealed record PublishEventDto(
    string Actor,
    string? RegistrationUrl = null);

public sealed record ScheduleAlterationDto(
    string Actor,
    string Reason);

public sealed record CompleteEventDto(
    string Actor,
    DateTimeOffset? AssetDueAt = null);

public sealed record ArchiveEventDto(
    string Actor,
    bool OverrideMissingAssets = false);

public sealed record EventLifecycleOperationDto(
    Guid EventId,
    string LifecycleState,
    string? GitHubFolderUrl,
    string? RegistrationUrl,
    string Message);

public sealed record NormalizedSpeakerDto(
    string Name,
    string? GitHubHandle,
    string? TwitterHandle,
    string? AvatarUrl);

public sealed record NormalizedSessionDto(
    string Title,
    string? Description,
    IReadOnlyCollection<NormalizedSpeakerDto> Speakers,
    string Source,
    string? SourceSessionId,
    DateTimeOffset? PreferredStartTime,
    TimeSpan? Duration);

public sealed record SessionizeImportResponseDto(int ImportedCount);

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
