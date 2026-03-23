using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Backend.Contracts;

public sealed record CreateEventRequest(
    string Title,
    string? Description,
    EventType Type,
    int Capacity,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Location,
    string CreatedBy);

public sealed record UpdateEventRequest(
    string Title,
    string? Description,
    EventType Type,
    int Capacity,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Location,
    EventStatus Status);

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
    DateTimeOffset CreatedAt);
