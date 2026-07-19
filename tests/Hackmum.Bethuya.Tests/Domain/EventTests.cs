using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Events;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Tests.Domain;

public class EventTests
{
    [Test]
    public async Task Event_DefaultStatus_IsDraft()
    {
        var evt = new Event { Title = "Test Event", CreatedBy = "test@example.com" };
        await Assert.That(evt.Status).IsEqualTo(EventStatus.Draft);
    }

    [Test]
    public async Task Event_Id_IsVersion7Guid()
    {
        var evt = new Event { Title = "Test Event", CreatedBy = "test@example.com" };
        await Assert.That(evt.Id).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task Event_CreatedAt_IsSetAutomatically()
    {
        var before = DateTimeOffset.UtcNow;
        var evt = new Event { Title = "Test Event", CreatedBy = "test@example.com" };
        var after = DateTimeOffset.UtcNow;

        await Assert.That(evt.CreatedAt).IsGreaterThanOrEqualTo(before);
        await Assert.That(evt.CreatedAt).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task Event_Registrations_InitializedEmpty()
    {
        var evt = new Event { Title = "Test Event", CreatedBy = "test@example.com" };
        await Assert.That(evt.Registrations).IsNotNull();
        await Assert.That(evt.Registrations.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Event_CanSetCapacity()
    {
        var evt = new Event { Title = "Dev Days", CreatedBy = "org@hackmum.org", Capacity = 100 };
        await Assert.That(evt.Capacity).IsEqualTo(100);
    }

    [Test]
    public async Task Event_DefaultLifecycleState_IsDrafted()
    {
        var evt = new Event { Title = "Test Event", CreatedBy = "test@example.com" };
        await Assert.That(evt.LifecycleState).IsEqualTo(MeetupLifecycleState.Drafted);
    }

    [Test]
    public async Task TransitionLifecycleTo_AllowedPath_UpdatesStateAndEmitsDomainEvent()
    {
        var occurredAt = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
        var evt = new Event { Title = "Test Event", CreatedBy = "test@example.com" };

        evt.TransitionLifecycleTo(MeetupLifecycleState.VenueLocked, occurredAt);

        await Assert.That(evt.LifecycleState).IsEqualTo(MeetupLifecycleState.VenueLocked);
        await Assert.That(evt.DomainEvents).Count().IsEqualTo(1);
        await Assert.That(evt.DomainEvents.Single()).IsTypeOf<EventLifecycleTransitioned>();
    }

    [Test]
    public async Task TransitionLifecycleTo_InvalidPath_ThrowsWithoutChangingState()
    {
        var evt = new Event { Title = "Test Event", CreatedBy = "test@example.com" };

        var action = () => evt.TransitionLifecycleTo(MeetupLifecycleState.Published, DateTimeOffset.UtcNow);

        await Assert.That(action).Throws<InvalidOperationException>();
        await Assert.That(evt.LifecycleState).IsEqualTo(MeetupLifecycleState.Drafted);
        await Assert.That(evt.DomainEvents).IsEmpty();
    }

    [Test]
    public async Task TransitionLifecycleTo_Published_AssignsPublishedTimestampOnce()
    {
        var firstPublishedAt = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
        var secondPublishedAt = firstPublishedAt.AddHours(2);
        var evt = new Event { Title = "Test Event", CreatedBy = "test@example.com" };

        evt.TransitionLifecycleTo(MeetupLifecycleState.VenueLocked, firstPublishedAt.AddHours(-6));
        evt.TransitionLifecycleTo(MeetupLifecycleState.CfpOpen, firstPublishedAt.AddHours(-5));
        evt.TransitionLifecycleTo(MeetupLifecycleState.ReviewAndPlanning, firstPublishedAt.AddHours(-4));
        evt.TransitionLifecycleTo(MeetupLifecycleState.AgendaApproved, firstPublishedAt.AddHours(-3));
        evt.TransitionLifecycleTo(MeetupLifecycleState.Published, firstPublishedAt);
        evt.TransitionLifecycleTo(MeetupLifecycleState.ScheduleAltered, firstPublishedAt.AddHours(1));
        evt.TransitionLifecycleTo(MeetupLifecycleState.Published, secondPublishedAt);

        await Assert.That(evt.PublishedAt).IsEqualTo(firstPublishedAt);
        await Assert.That(evt.DomainEvents.OfType<EventPublished>()).Count().IsEqualTo(2);
    }
}
