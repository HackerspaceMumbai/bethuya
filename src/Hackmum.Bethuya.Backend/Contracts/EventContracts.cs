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
    EventStatus Status = EventStatus.Draft,
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
    EventFairnessTargetsContract FairnessTargets);

public sealed record EventFairnessTargetsContract(
    double GeoOutsideDominantMinPercent = 0.35,
    double LocalLanguageMinPercent = 0.25,
    double UnderrepresentedEducationMinPercent = 0.25,
    bool EnableSocioeconomicDimension = false,
    double? UnderrepresentedSocioeconomicMinPercent = null,
    int KAnonymityThreshold = 5);
