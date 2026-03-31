using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Tests.Models;

public class EventCoverImageTests
{
    [Test]
    public async Task Event_CoverImageUrl_DefaultsToNull()
    {
        var evt = new Event { Title = "Test Event", CreatedBy = "test@example.com" };

        await Assert.That(evt.CoverImageUrl).IsNull();
    }

    [Test]
    public async Task Event_CoverImageUrl_CanBeSetViaInitializer()
    {
        var url = "https://res.cloudinary.com/demo/image/upload/v1/bethuya/events/cover.jpg";

        var evt = new Event
        {
            Title = "Dev Days",
            CreatedBy = "org@hackmum.org",
            CoverImageUrl = url
        };

        await Assert.That(evt.CoverImageUrl).IsEqualTo(url);
    }

    [Test]
    public async Task Event_CoverImageUrl_CanBeUpdatedAfterCreation()
    {
        var evt = new Event { Title = "Workshop", CreatedBy = "test@example.com" };
        var url = "https://res.cloudinary.com/demo/image/upload/v1/bethuya/events/new-cover.png";

        evt.CoverImageUrl = url;

        await Assert.That(evt.CoverImageUrl).IsEqualTo(url);
    }

    [Test]
    public async Task Event_CoverImageUrl_CanBeClearedToNull()
    {
        var evt = new Event
        {
            Title = "Meetup",
            CreatedBy = "org@hackmum.org",
            CoverImageUrl = "https://example.com/image.jpg"
        };

        evt.CoverImageUrl = null;

        await Assert.That(evt.CoverImageUrl).IsNull();
    }
}
