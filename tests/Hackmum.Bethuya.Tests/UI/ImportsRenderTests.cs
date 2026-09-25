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

        // Seed a committable batch (DryRunCompleted, no errors, no prior failure reason) so the
        // rendered Commit button is what actually drives the assertion, rather than reflection
        // invoking CommitAsync directly while _batch.CanCommit would have hidden the button.
        var batchField = typeof(Imports).GetField("_batch", BindingFlags.Instance | BindingFlags.NonPublic);
        var committableBatch = new ImportBatchDto(
            failedBatchId,
            eventId,
            "Registration",
            Guid.CreateVersion7(),
            ImportBatchStatusDto.DryRunCompleted,
            3,
            3,
            0,
            3,
            0,
            null,
            "organizer",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            "sample.csv");
        batchField!.SetValue(cut.Instance, committableBatch);

        // The Commit button also requires a loaded preview (see Imports.razor: CanCommit &&
        // _preview is not null), so seed one here too — otherwise the button never renders.
        var previewField = typeof(Imports).GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic);
        previewField!.SetValue(cut.Instance, new ImportPreviewReportDto(3, 3, 0, 3, 0, []));
        cut.Render();

        cut.WaitForState(() => cut.FindAll("[data-test='commit-import']").Count == 1, TimeSpan.FromSeconds(5));
        cut.Find("[data-test='commit-import'] button").Click();
        cut.WaitForState(() => cut.Markup.Contains("Two rows failed validation."), TimeSpan.FromSeconds(5));

        await importApi.Received(1).CommitAsync(failedBatchId, Arg.Any<CancellationToken>());
        await Assert.That(cut.Markup).Contains("Two rows failed validation.");
        await Assert.That(cut.Markup).DoesNotContain("Import committed successfully");
    }

    [Test]
    public async Task RetryPreview_LoadsPreviewAndEnablesCommit_WithoutReupload()
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

        var batchId = Guid.CreateVersion7();
        var importApi = Substitute.For<IImportApi>();
        importApi.ListTemplatesAsync("Registration", Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new List<ImportTemplateDto>
            {
                new(Guid.CreateVersion7(), "Luma registrations", "System", "Luma", "Registration", null, null, [])
            }));
        importApi.GetPreviewAsync(batchId, Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new ImportPreviewReportDto(3, 3, 0, 3, 0, [])));

        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(importApi);
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<Imports>();
        cut.WaitForState(() => cut.FindAll("[data-test='import-file-field']").Count == 1, TimeSpan.FromSeconds(5));

        // Simulate a batch whose preview failed to load: _batch is set (as StartDryRunAsync
        // now does immediately after a successful upload) but _preview is still null.
        var committableBatch = new ImportBatchDto(
            batchId,
            eventId,
            "Registration",
            Guid.CreateVersion7(),
            ImportBatchStatusDto.DryRunCompleted,
            3,
            3,
            0,
            3,
            0,
            null,
            "organizer",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            "sample.csv");
        var batchField = typeof(Imports).GetField("_batch", BindingFlags.Instance | BindingFlags.NonPublic);
        batchField!.SetValue(cut.Instance, committableBatch);
        cut.Render();

        cut.WaitForState(() => cut.FindAll("[data-test='import-preview-unavailable']").Count == 1, TimeSpan.FromSeconds(5));
        await Assert.That(cut.FindAll("[data-test='commit-import']")).IsEmpty();

        cut.Find("[data-test='import-preview-unavailable'] button").Click();
        cut.WaitForState(() => cut.FindAll("[data-test='commit-import']").Count == 1, TimeSpan.FromSeconds(5));

        await importApi.Received(1).GetPreviewAsync(batchId, Arg.Any<CancellationToken>());
    }
}
