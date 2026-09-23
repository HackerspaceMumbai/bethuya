using System.Reflection;
using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

public class ImportsRenderTests
{
    [Test]
    public async Task Render_LoadsOrganizerImportWizard()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddTestAuthorization().SetAuthorized("Organizer").SetRoles(BethuyaRoles.Organizer);

        var eventId = Guid.CreateVersion7();
        var eventApi = Substitute.For<IEventApi>();
        eventApi.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<EventDto>
        {
            new(
                Id: eventId,
                Title: "Community Meetup",
                Description: null,
                Type: "Meetup",
                Status: "Draft",
                Capacity: 50,
                StartDate: DateTimeOffset.UtcNow,
                EndDate: DateTimeOffset.UtcNow.AddHours(2),
                Location: null,
                CreatedBy: "organizer",
                CreatedAt: DateTimeOffset.UtcNow,
                Hashtag: null,
                CoverImageUrl: null,
                SessionizeEventId: "",
                GitHubFolderUrl: null,
                TeamsAnnouncementMessageId: null,
                RegistrationUrl: null,
                LifecycleState: "Drafted",
                PublishedAt: null,
                CompletedAt: null,
                ArchivedAt: null,
                FairnessTargets: new EventFairnessTargetsDto())
        }));

        var importApi = Substitute.For<IImportApi>();
        importApi.ListTemplatesAsync("Registration", Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new List<ImportTemplateDto>
            {
                new(Guid.CreateVersion7(), "Luma registrations", "System", "Luma", "Registration", null, null, [])
            }));

        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(importApi);
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<Imports>();
        cut.WaitForState(() => cut.FindAll("[data-test='import-file-field']").Count == 1, TimeSpan.FromSeconds(5));

        await Assert.That(cut.Markup).Contains("Import registrations or attendance");
        await Assert.That(cut.Markup).Contains("Run validation");
        await importApi.Received(1).ListTemplatesAsync("Registration", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Commit_FailedBatch_ShowsFailureReasonInsteadOfSuccessMessage()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddTestAuthorization().SetAuthorized("Organizer").SetRoles(BethuyaRoles.Organizer);

        var eventId = Guid.CreateVersion7();
        var eventApi = Substitute.For<IEventApi>();
        eventApi.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<EventDto>
        {
            new(
                Id: eventId,
                Title: "Community Meetup",
                Description: null,
                Type: "Meetup",
                Status: "Draft",
                Capacity: 50,
                StartDate: DateTimeOffset.UtcNow,
                EndDate: DateTimeOffset.UtcNow.AddHours(2),
                Location: null,
                CreatedBy: "organizer",
                CreatedAt: DateTimeOffset.UtcNow,
                Hashtag: null,
                CoverImageUrl: null,
                SessionizeEventId: "",
                GitHubFolderUrl: null,
                TeamsAnnouncementMessageId: null,
                RegistrationUrl: null,
                LifecycleState: "Drafted",
                PublishedAt: null,
                CompletedAt: null,
                ArchivedAt: null,
                FairnessTargets: new EventFairnessTargetsDto())
        }));

        var failedBatchId = Guid.CreateVersion7();
        var importApi = Substitute.For<IImportApi>();
        importApi.ListTemplatesAsync("Registration", Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new List<ImportTemplateDto>
            {
                new(Guid.CreateVersion7(), "Luma registrations", "System", "Luma", "Registration", null, null, [])
            }));
        importApi.CommitAsync(failedBatchId, Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new ImportBatchDto(
                failedBatchId,
                eventId,
                "Registration",
                Guid.CreateVersion7(),
                ImportBatchStatusDto.Failed,
                3,
                0,
                3,
                0,
                0,
                "Two rows failed validation.",
                "organizer",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null,
                "sample.csv")));

        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(importApi);
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<Imports>();
        cut.WaitForState(() => cut.FindAll("[data-test='import-file-field']").Count == 1, TimeSpan.FromSeconds(5));

        var batchField = typeof(Imports).GetField("_batch", BindingFlags.Instance | BindingFlags.NonPublic);
        var batch = new ImportBatchDto(
            failedBatchId,
            eventId,
            "Registration",
            Guid.CreateVersion7(),
            ImportBatchStatusDto.Failed,
            3,
            0,
            3,
            0,
            0,
            "Two rows failed validation.",
            "organizer",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            "sample.csv");
        batchField!.SetValue(cut.Instance, batch);

        var method = typeof(Imports).GetMethod("CommitAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        await cut.InvokeAsync(async () => await (Task)method!.Invoke(cut.Instance, null)!);

        await Assert.That(cut.Markup).Contains("Two rows failed validation.");
        await Assert.That(cut.Markup).DoesNotContain("Import committed successfully");
    }
}
