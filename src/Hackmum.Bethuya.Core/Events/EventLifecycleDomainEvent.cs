using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Events;

/// <summary>
/// Base type for event lifecycle domain events emitted by the event aggregate.
/// </summary>
public abstract record EventLifecycleDomainEvent(Guid EventId, DateTimeOffset OccurredAt);

/// <summary>
/// Emitted when an event CFP opens.
/// </summary>
public sealed record EventCfpOpened(Guid EventId, DateTimeOffset OccurredAt)
    : EventLifecycleDomainEvent(EventId, OccurredAt);

/// <summary>
/// Emitted when an event agenda is approved.
/// </summary>
public sealed record EventAgendaApproved(Guid EventId, DateTimeOffset OccurredAt)
    : EventLifecycleDomainEvent(EventId, OccurredAt);

/// <summary>
/// Emitted when an event is published.
/// </summary>
public sealed record EventPublished(Guid EventId, DateTimeOffset OccurredAt)
    : EventLifecycleDomainEvent(EventId, OccurredAt);

/// <summary>
/// Emitted when a published event schedule changes.
/// </summary>
public sealed record EventScheduleAltered(Guid EventId, DateTimeOffset OccurredAt)
    : EventLifecycleDomainEvent(EventId, OccurredAt);

/// <summary>
/// Emitted when an event is delayed.
/// </summary>
public sealed record EventDelayed(Guid EventId, DateTimeOffset OccurredAt)
    : EventLifecycleDomainEvent(EventId, OccurredAt);

/// <summary>
/// Emitted when an event completes.
/// </summary>
public sealed record EventCompleted(Guid EventId, DateTimeOffset OccurredAt)
    : EventLifecycleDomainEvent(EventId, OccurredAt);

/// <summary>
/// Emitted when an event is archived.
/// </summary>
public sealed record EventArchived(Guid EventId, DateTimeOffset OccurredAt)
    : EventLifecycleDomainEvent(EventId, OccurredAt);

/// <summary>
/// Emitted for valid lifecycle transitions that do not have a specialized event yet.
/// </summary>
public sealed record EventLifecycleTransitioned(
    Guid EventId,
    MeetupLifecycleState PreviousState,
    MeetupLifecycleState NewState,
    DateTimeOffset OccurredAt)
    : EventLifecycleDomainEvent(EventId, OccurredAt);
