using Hackmum.Bethuya.Core.Enums;
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
}
