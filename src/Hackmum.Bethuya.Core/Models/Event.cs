using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Events;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hackmum.Bethuya.Core.Models;

public sealed class Event
{
    private readonly List<EventLifecycleDomainEvent> _domainEvents = [];

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Title { get; set; }
    public string? Description { get; set; }
    public EventType Type { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public MeetupLifecycleState LifecycleState { get; private set; } = MeetupLifecycleState.Drafted;
    public int Capacity { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string? Location { get; set; }
    public string? Hashtag { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? SessionizeEventId { get; set; }
    public string? GitHubFolderUrl { get; set; }
    public string? TeamsAnnouncementMessageId { get; set; }
    public string? RegistrationUrl { get; set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public EventFairnessTargets FairnessTargets { get; set; } = new();
    public required string CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Registration> Registrations { get; init; } = [];
    public Agenda? Agenda { get; set; }

    [NotMapped]
    public IReadOnlyCollection<EventLifecycleDomainEvent> DomainEvents => _domainEvents;

    public static bool CanTransition(MeetupLifecycleState current, MeetupLifecycleState next)
        => current switch
        {
            MeetupLifecycleState.Drafted => next is MeetupLifecycleState.VenueLocked,
            MeetupLifecycleState.VenueLocked => next is MeetupLifecycleState.CfpOpen,
            MeetupLifecycleState.CfpOpen => next is MeetupLifecycleState.CfpExtended or MeetupLifecycleState.ReviewAndPlanning,
            MeetupLifecycleState.CfpExtended => next is MeetupLifecycleState.ReviewAndPlanning,
            MeetupLifecycleState.ReviewAndPlanning => next is MeetupLifecycleState.AgendaApproved,
            MeetupLifecycleState.AgendaApproved => next is MeetupLifecycleState.Published,
            MeetupLifecycleState.Published => next is MeetupLifecycleState.ScheduleAltered or MeetupLifecycleState.Delayed or MeetupLifecycleState.Completed,
            MeetupLifecycleState.ScheduleAltered => next is MeetupLifecycleState.Published or MeetupLifecycleState.Completed,
            MeetupLifecycleState.Delayed => next is MeetupLifecycleState.Published or MeetupLifecycleState.Completed,
            MeetupLifecycleState.Completed => next is MeetupLifecycleState.Archived,
            MeetupLifecycleState.Archived => false,
            _ => false
        };

    public void TransitionLifecycleTo(MeetupLifecycleState next, DateTimeOffset occurredAt)
    {
        if (LifecycleState == next)
        {
            return;
        }

        if (!CanTransition(LifecycleState, next))
        {
            throw new InvalidOperationException($"Cannot transition event lifecycle from {LifecycleState} to {next}.");
        }

        var previous = LifecycleState;
        LifecycleState = next;
        UpdatedAt = occurredAt;
        ApplyLifecycleTimestamp(next, occurredAt);
        _domainEvents.Add(CreateLifecycleEvent(previous, next, occurredAt));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void ApplyLifecycleTimestamp(MeetupLifecycleState state, DateTimeOffset occurredAt)
    {
        switch (state)
        {
            case MeetupLifecycleState.Published:
                PublishedAt ??= occurredAt;
                break;
            case MeetupLifecycleState.Completed:
                CompletedAt ??= occurredAt;
                break;
            case MeetupLifecycleState.Archived:
                ArchivedAt ??= occurredAt;
                break;
        }
    }

    private EventLifecycleDomainEvent CreateLifecycleEvent(
        MeetupLifecycleState previous,
        MeetupLifecycleState next,
        DateTimeOffset occurredAt)
        => next switch
        {
            MeetupLifecycleState.CfpOpen => new EventCfpOpened(Id, occurredAt),
            MeetupLifecycleState.AgendaApproved => new EventAgendaApproved(Id, occurredAt),
            MeetupLifecycleState.Published => new EventPublished(Id, occurredAt),
            MeetupLifecycleState.ScheduleAltered => new EventScheduleAltered(Id, occurredAt),
            MeetupLifecycleState.Delayed => new EventDelayed(Id, occurredAt),
            MeetupLifecycleState.Completed => new EventCompleted(Id, occurredAt),
            MeetupLifecycleState.Archived => new EventArchived(Id, occurredAt),
            _ => new EventLifecycleTransitioned(Id, previous, next, occurredAt)
        };
}
