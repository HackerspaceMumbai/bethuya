using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Backend.Contracts;

public sealed record PlanEventRequest(
    string Title,
    string? Description,
    EventType Type,
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
    EventStatus Status = EventStatus.Draft,
    MeetupLifecycleState LifecycleState = MeetupLifecycleState.Drafted,
    EventFairnessTargetsContract? FairnessTargets = null);

public sealed record UpdateEventRequest(
    string Title,
    string? Description,
    EventType Type,
    int Capacity,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Location,
    EventStatus Status,
    string? CoverImageUrl,
    string? SessionizeEventId = null,
    string? GitHubFolderUrl = null,
    string? TeamsAnnouncementMessageId = null,
    string? RegistrationUrl = null,
    EventFairnessTargetsContract? FairnessTargets = null);

public sealed record EventResponse(
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
    EventFairnessTargetsContract FairnessTargets);

public sealed record EventFairnessTargetsContract(
    double GeoOutsideDominantMinPercent = 0.35,
    double LocalLanguageMinPercent = 0.25,
    double UnderrepresentedEducationMinPercent = 0.25,
    bool EnableSocioeconomicDimension = false,
    double? UnderrepresentedSocioeconomicMinPercent = null,
    int KAnonymityThreshold = 5,
    double GenderDiversityMinPercent = 0.40);

public sealed record EventLifecycleTransitionRequest(
    MeetupLifecycleState TargetState,
    string Actor);

public sealed record PublishEventRequest(
    string Actor,
    string? RegistrationUrl = null);

public sealed record ScheduleAlterationRequest(
    string Actor,
    string Reason);

public sealed record CompleteEventRequest(
    string Actor,
    DateTimeOffset? AssetDueAt = null);

public sealed record ArchiveEventRequest(
    string Actor,
    bool OverrideMissingAssets = false);

public sealed record EventLifecycleOperationResponse(
    Guid EventId,
    string LifecycleState,
    string? GitHubFolderUrl,
    string? RegistrationUrl,
    string Message);

public sealed record NormalizedSpeakerContract(
    string Name,
    string? GitHubHandle,
    string? TwitterHandle,
    string? AvatarUrl);

public sealed record NormalizedSessionContract(
    string Title,
    string? Description,
    IReadOnlyCollection<NormalizedSpeakerContract> Speakers,
    string Source,
    string? SourceSessionId,
    DateTimeOffset? PreferredStartTime,
    TimeSpan? Duration);

public sealed record SessionizeImportResponse(int ImportedCount);
