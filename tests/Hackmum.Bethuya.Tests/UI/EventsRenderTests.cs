using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

public sealed class EventsRenderTests
{
    [Test]
    public async Task Render_ShowsLifecycleFilterAndLifecycleBadge()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization();

        var eventApi = Substitute.For<IEventApi>();
        eventApi.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<EventDto>
            {
                CreateEventDto("Lifecycle event", "Published")
            }));
        ctx.Services.AddSingleton(eventApi);

        var cut = ctx.RenderComponent<Events>();

        cut.WaitForAssertion(() =>
        {
            if (!cut.Markup.Contains("lifecycle-filter", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Lifecycle filter was not rendered.");
            }
        });
        await Assert.That(cut.Markup).Contains("Published");
    }

    private static EventDto CreateEventDto(string title, string lifecycleState)
        => new(
            Guid.CreateVersion7(),
            title,
            "Lifecycle-aware event",
            "Meetup",
            "Draft",
            100,
            new DateTimeOffset(2026, 7, 19, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 19, 21, 0, 0, TimeSpan.Zero),
            "Hackerspace Mumbai",
            "organizer",
            new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
            "devdays",
            null,
            lifecycleState,
            "sessionize-123",
            null,
            null,
            null,
            null,
            null,
            null,
            new EventFairnessTargetsDto());
}
