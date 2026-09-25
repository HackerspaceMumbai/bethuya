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

        // RetryPreviewAsync also re-fetches the batch itself (not just the preview) so that
        // CanCommit/row counts stay in sync with whatever the server just reported.
        importApi.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(committableBatch));

        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(importApi);
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<Imports>();
        cut.WaitForState(() => cut.FindAll("[data-test='import-file-field']").Count == 1, TimeSpan.FromSeconds(5));

        // Simulate a batch whose preview failed to load: _batch is set (as StartDryRunAsync
        // now does immediately after a successful upload) but _preview is still null.
        var batchField = typeof(Imports).GetField("_batch", BindingFlags.Instance | BindingFlags.NonPublic);
        batchField!.SetValue(cut.Instance, committableBatch);
        cut.Render();

        cut.WaitForState(() => cut.FindAll("[data-test='import-preview-unavailable']").Count == 1, TimeSpan.FromSeconds(5));
        await Assert.That(cut.FindAll("[data-test='commit-import']")).IsEmpty();

        cut.Find("[data-test='import-preview-unavailable'] button").Click();
        cut.WaitForState(() => cut.FindAll("[data-test='commit-import']").Count == 1, TimeSpan.FromSeconds(5));

        await importApi.Received(1).GetPreviewAsync(batchId, Arg.Any<CancellationToken>());
        await importApi.Received(1).GetBatchAsync(batchId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RetryPreview_RefreshesBatch_DisablesCommitWhenServerNowReportsErrors()
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

        // The preview the retry fetches now reports a validation error on one row...
        importApi.GetPreviewAsync(batchId, Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new ImportPreviewReportDto(3, 2, 1, 3, 0,
            [
                new ImportRowPreviewDto(1, "a@example.com", "created", ["Missing required field"])
            ])));

        // ...and the freshly re-fetched batch reflects that same error (ErrorRows = 1, so
        // CanCommit is false), which is what should ultimately gate the Commit button — not
        // whatever _batch looked like before the retry.
        var refreshedBatch = new ImportBatchDto(
            batchId,
            eventId,
            "Registration",
            Guid.CreateVersion7(),
            ImportBatchStatusDto.DryRunCompleted,
            3,
            2,
            1,
            3,
            0,
            null,
            "organizer",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            "sample.csv");
        importApi.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(refreshedBatch));

        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(importApi);
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<Imports>();
        cut.WaitForState(() => cut.FindAll("[data-test='import-file-field']").Count == 1, TimeSpan.FromSeconds(5));

        // Seed a stale, error-free, committable batch as if it were fetched before the retry.
        var staleBatch = new ImportBatchDto(
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
        batchField!.SetValue(cut.Instance, staleBatch);
        cut.Render();

        cut.WaitForState(() => cut.FindAll("[data-test='import-preview-unavailable']").Count == 1, TimeSpan.FromSeconds(5));
        cut.Find("[data-test='import-preview-unavailable'] button").Click();

        // The retry should refresh _batch alongside _preview, so the Commit button stays hidden
        // once the freshly-fetched batch/preview both report an error — even though the batch
        // seeded before the retry was error-free and committable.
        cut.WaitForState(() => cut.Markup.Contains("Missing required field"), TimeSpan.FromSeconds(5));
        await Assert.That(cut.FindAll("[data-test='commit-import']")).IsEmpty();
        await importApi.Received(1).GetBatchAsync(batchId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StartDryRun_FailedReupload_ClearsStaleBatchAndPreviewFromPriorSuccess()
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

        var priorBatchId = Guid.CreateVersion7();
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

        // Seed a fully-committable batch + preview, as if a prior file had already gone
        // through a successful StartDryRunAsync call.
        var priorBatch = new ImportBatchDto(
            priorBatchId,
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
            "prior.csv");
        var batchField = typeof(Imports).GetField("_batch", BindingFlags.Instance | BindingFlags.NonPublic);
        var previewField = typeof(Imports).GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic);
        batchField!.SetValue(cut.Instance, priorBatch);
        previewField!.SetValue(cut.Instance, new ImportPreviewReportDto(3, 3, 0, 3, 0, []));
        cut.Render();
        cut.WaitForState(() => cut.FindAll("[data-test='commit-import']").Count == 1, TimeSpan.FromSeconds(5));

        // Set up a genuinely new (but invalid, from the server's perspective) upload attempt:
        // valid event/template/file selections, but the upload itself throws. Only once inputs
        // are valid should the prior batch/preview be discarded — otherwise the organizer could
        // never recover from a failed re-upload of a different file without losing the
        // previously-validated batch too.
        importApi.UploadAndRunDryRunAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Refit.StreamPart>(), Arg.Any<CancellationToken>())
            .Returns<ImportBatchDto>(_ => throw new HttpRequestException("upload failed"));

        var eventIdTextField = typeof(Imports).GetField("_eventIdText", BindingFlags.Instance | BindingFlags.NonPublic);
        var templateIdTextField = typeof(Imports).GetField("_templateIdText", BindingFlags.Instance | BindingFlags.NonPublic);
        var selectedFileField = typeof(Imports).GetField("_selectedFile", BindingFlags.Instance | BindingFlags.NonPublic);
        eventIdTextField!.SetValue(cut.Instance, eventId.ToString());
        templateIdTextField!.SetValue(cut.Instance, Guid.CreateVersion7().ToString());
        var newFile = Substitute.For<Microsoft.AspNetCore.Components.Forms.IBrowserFile>();
        newFile.Name.Returns("new-file.csv");
        newFile.ContentType.Returns("text/csv");
        newFile.OpenReadStream(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(new MemoryStream("a,b\n1,2"u8.ToArray()));
        selectedFileField!.SetValue(cut.Instance, newFile);

        // Now invoke StartDryRunAsync directly. The prior batch/preview must be cleared
        // immediately once this new, input-valid attempt begins, rather than left rendered
        // alongside a now-stale "Commit import" button that would submit the OLD batch id.
        var startDryRun = typeof(Imports).GetMethod("StartDryRunAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        await cut.InvokeAsync(() => (Task)startDryRun!.Invoke(cut.Instance, null)!);
        cut.Render();

        await Assert.That(batchField.GetValue(cut.Instance)).IsNull();
        await Assert.That(previewField.GetValue(cut.Instance)).IsNull();
        await Assert.That(cut.FindAll("[data-test='commit-import']")).IsEmpty();
    }

    [Test]
    public async Task StartDryRun_InvalidInputs_PreservesExistingBatchAndPreview()
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

        var priorBatchId = Guid.CreateVersion7();
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

        // Seed a fully-committable batch + preview, as if a prior file had already gone
        // through a successful StartDryRunAsync call, but do NOT select a new file this time.
        var priorBatch = new ImportBatchDto(
            priorBatchId,
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
            "prior.csv");
        var batchField = typeof(Imports).GetField("_batch", BindingFlags.Instance | BindingFlags.NonPublic);
        var previewField = typeof(Imports).GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic);
        batchField!.SetValue(cut.Instance, priorBatch);
        previewField!.SetValue(cut.Instance, new ImportPreviewReportDto(3, 3, 0, 3, 0, []));
        cut.Render();
        cut.WaitForState(() => cut.FindAll("[data-test='commit-import']").Count == 1, TimeSpan.FromSeconds(5));

        // Invoke StartDryRunAsync with no file selected (an accidental "Run validation" click,
        // or one made before re-selecting a file). Input validation fails and returns early —
        // this must NOT touch the already-valid, already-committable prior batch (see PR #60
        // review discussion, "Earlier batch becomes inaccessible").
        var startDryRun = typeof(Imports).GetMethod("StartDryRunAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        await (Task)startDryRun!.Invoke(cut.Instance, null)!;
        cut.Render();

        await Assert.That(batchField.GetValue(cut.Instance)).IsEqualTo(priorBatch);
        await Assert.That(previewField.GetValue(cut.Instance)).IsNotNull();
        await Assert.That(cut.FindAll("[data-test='commit-import']").Count).IsEqualTo(1);
        await importApi.DidNotReceive().UploadAndRunDryRunAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Refit.StreamPart>(), Arg.Any<CancellationToken>());
    }
}
