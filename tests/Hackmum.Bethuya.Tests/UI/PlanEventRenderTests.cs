using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit.TestDoubles;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

// Alias Bunit.TestContext to avoid collision with TUnit's own TestContext type.
using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

/// <summary>
/// bUnit rendering tests for <see cref="PlanEvent"/>.
///
/// These tests catch the class of runtime error:
///   InvalidOperationException: Object of type 'BbTimePicker' does not have a property matching the name 'data-test'
/// which only surfaces when the component actually renders — TUnit unit tests alone cannot catch it.
/// </summary>
public class PlanEventRenderTests
{
    [Test]
    public async Task Render_DoesNotThrow_WhenDataTestAttributesAreOnWrapperDivs()
    {
        // Arrange
        using var ctx = new BunitCtx();

        // BlazorBlueprint components call JS module imports — use Loose mode to auto-satisfy them.
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var eventApi = Substitute.For<IEventApi>();
        ctx.Services.AddSingleton(eventApi);

        // BlazorBlueprint components (BbDatePicker, BbTimePicker) require IBbLocalizer.
        ctx.Services.AddBlazorBlueprintComponents();

        // Provide fake auth state so any AuthorizeView/[Authorize] cascades resolve.
        ctx.AddTestAuthorization();

        // Act ─ if data-test were placed directly on BbTimePicker or BbDatePicker
        // (which have no CaptureUnmatchedValues), Blazor would throw an
        // InvalidOperationException here before any assertion is reached.
        var cut = ctx.RenderComponent<PlanEvent>();

        var markup = cut.Markup;

        // Assert ─ wrapper divs with data-test attributes are present
        await Assert.That(markup).Contains("start-date-picker");
        await Assert.That(markup).Contains("start-time-picker");
        await Assert.That(markup).Contains("end-date-picker");
        await Assert.That(markup).Contains("end-time-picker");
        await Assert.That(markup).Contains("plan-event-form");
    }

    [Test]
    public async Task Render_SuggestDatesButton_IsPresent()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton(Substitute.For<IEventApi>());
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization();

        var cut = ctx.RenderComponent<PlanEvent>();

        await Assert.That(cut.Markup).Contains("suggest-dates-btn");
        await Assert.That(cut.Markup).Contains("Suggest Dates");
    }

    [Test]
    public async Task Render_SuggestDatesSection_HasDataTestAttribute()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton(Substitute.For<IEventApi>());
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization();

        var cut = ctx.RenderComponent<PlanEvent>();

        await Assert.That(cut.Markup).Contains("suggest-dates-section");
    }

    [Test]
    public async Task Render_StatusBadge_ShowsDraftByDefault()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton(Substitute.For<IEventApi>());
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization();

        var cut = ctx.RenderComponent<PlanEvent>();

        await Assert.That(cut.Markup).Contains("event-status-badge");
        await Assert.That(cut.Markup).Contains("Draft");
    }

    [Test]
    public async Task Render_DualButtons_SaveDraftAndPublishPresent()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton(Substitute.For<IEventApi>());
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization();

        var cut = ctx.RenderComponent<PlanEvent>();

        await Assert.That(cut.Markup).Contains("save-draft-btn");
        await Assert.That(cut.Markup).Contains("publish-event-btn");
        await Assert.That(cut.Markup).Contains("Save Draft");
        await Assert.That(cut.Markup).Contains("Publish Event");
    }
}
