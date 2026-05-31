using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit.TestDoubles;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

public class RegistrationsRenderTests
{
    [Test]
    public async Task Render_LoadsRegistrationsFromApi()
    {
        using var ctx = CreateContext();
        ctx.AddTestAuthorization().SetAuthorized("Attendee").SetRoles(BethuyaRoles.Attendee);

        var eventId = Guid.Parse("019e263d-effb-7a55-86ec-170baeee3718");
        var registrationApi = Substitute.For<IRegistrationApi>();
        var profileApi = Substitute.For<IProfileApi>();
        registrationApi.GetByEventIdAsync(eventId, Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new List<RegistrationDto>
            {
                new(
                    Guid.Parse("019e263d-effb-7a55-86ec-170baeee3719"),
                    eventId,
                    "Attendee One",
                    "one@example.com",
                    "First attendee",
                    ["AI", ".NET"],
                    "Pending",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow),
                new(
                    Guid.Parse("019e263d-effb-7a55-86ec-170baeee3720"),
                    eventId,
                    "Attendee Two",
                    "two@example.com",
                    null,
                    ["Community"],
                    "Accepted",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow)
            }));

        profileApi.GetMandatoryProfileAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new MandatoryProfileDto(
                "Attendee",
                "One",
                "one@example.com",
                "+91 9876543210",
                "Passport",
                "1234",
                "Employee",
                "Hackerspace Mumbai",
                null,
                "Mumbai",
                "Maharashtra",
                "400001",
                "India")));

        ctx.Services.AddSingleton(registrationApi);
        ctx.Services.AddSingleton(profileApi);
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<Registrations>(parameters => parameters.Add(p => p.EventId, eventId));

        cut.WaitForState(() => cut.FindAll("[data-test='registration-row']").Count == 2, TimeSpan.FromSeconds(5));

        await Assert.That(cut.Markup).Contains("Attendee One");
        await Assert.That(cut.Markup).Contains("Attendee Two");
        await Assert.That(cut.Markup).Contains("one@example.com");
        await Assert.That(cut.Markup).Contains("Accepted");
        await Assert.That(cut.Markup).Contains("1. Intent");
        await Assert.That(cut.Markup).Contains("How likely are you to attend?");
        await Assert.That(cut.Markup).Contains("Upload Government ID");
        await Assert.That(cut.Markup).DoesNotContain("registration-name-field");
    }

    private static BunitCtx CreateContext()
    {
        var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx;
    }
}
