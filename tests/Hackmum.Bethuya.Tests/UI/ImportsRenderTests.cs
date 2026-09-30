using System.Reflection;
using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
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
        await Assert.That(cut.Markup).Contains("Registrations and approval updates");
        await Assert.That(cut.Find("[data-test='import-template-select']").TextContent).Contains("Luma registrations");
        await Assert.That(cut.Markup).Contains("Use this for registration requests and approval decisions.");
        await Assert.That(cut.Markup).Contains("Re-import an updated export, or upload a separate approval file");
        await Assert.That(cut.Markup).Contains("Run validation");
        await importApi.Received(1).ListTemplatesAsync("Registration", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ChangingImportKind_WhenTemplateLoadFails_ClearsOldTemplateAndShowsError()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddTestAuthorization().SetAuthorized("Organizer").SetRoles(BethuyaRoles.Organizer);

        var eventApi = Substitute.For<IEventApi>();
        eventApi.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<EventDto>()));
        var template = new ImportTemplateDto(Guid.CreateVersion7(), "Luma registrations", "System", "Luma", "Registration", null, null, []);
        var importApi = Substitute.For<IImportApi>();
        importApi.ListTemplatesAsync("Registration", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ImportTemplateDto> { template }));
        importApi.ListTemplatesAsync("Attendance", Arg.Any<CancellationToken>())
            .Returns<Task<List<ImportTemplateDto>>>(_ => throw new HttpRequestException("service unavailable"));
        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(importApi);
        ctx.Services.AddLogging();
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<Imports>();
        var templatesProperty = typeof(Imports).GetProperty("Templates", BindingFlags.Instance | BindingFlags.NonPublic)!;
        cut.WaitForState(() => ((List<ImportTemplateDto>)templatesProperty.GetValue(cut.Instance)!).Count == 1, TimeSpan.FromSeconds(5));

        var changeKind = typeof(Imports).GetMethod("HandleKindChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await cut.InvokeAsync(() => (Task)changeKind.Invoke(cut.Instance,
            [new ChangeEventArgs { Value = "Attendance" }])!);
        cut.Render();

        await Assert.That((List<ImportTemplateDto>)templatesProperty.GetValue(cut.Instance)!).IsEmpty();
        var templateIdField = typeof(Imports).GetField("_templateIdText", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await Assert.That(templateIdField.GetValue(cut.Instance)).IsNull();
        await Assert.That(cut.Markup).Contains("Unable to load import templates. Please try again.");
    }

    [Test]
    public async Task Render_WithEventIdQuery_PreselectsThatEvent()
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
            Task.FromResult(new List<ImportTemplateDto>()));

        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(importApi);
        ctx.Services.AddBlazorBlueprintComponents();

        var navigationManager = ctx.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo(navigationManager.GetUriWithQueryParameter("eventId", eventId.ToString()));

        var cut = ctx.RenderComponent<Imports>();
        cut.WaitForState(() => cut.FindAll("[data-test='import-file-field']").Count == 1, TimeSpan.FromSeconds(5));

        var eventIdTextField = typeof(Imports).GetField("_eventIdText", BindingFlags.Instance | BindingFlags.NonPublic);
        await Assert.That(eventIdTextField!.GetValue(cut.Instance)).IsEqualTo(eventId.ToString());
        await Assert.That(cut.Markup).Contains("Community Meetup");
    }

    [Test]
    public async Task FileUpload_SelectingSecondFile_ReplacesFirstFile()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddTestAuthorization().SetAuthorized("Organizer").SetRoles(BethuyaRoles.Organizer);

        var eventApi = Substitute.For<IEventApi>();
        eventApi.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<EventDto>()));
        var importApi = Substitute.For<IImportApi>();
        importApi.ListTemplatesAsync("Registration", Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new List<ImportTemplateDto>()));

        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(importApi);
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<Imports>();
        cut.WaitForState(() => cut.FindAll("[data-test='import-file-field']").Count == 1, TimeSpan.FromSeconds(5));

        var inputFile = cut.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromText("email\na@example.com", "approved.csv"));
        cut.WaitForState(() => cut.Markup.Contains("Selected: approved.csv"), TimeSpan.FromSeconds(5));

        inputFile.UploadFiles(InputFileContent.CreateFromText("email\nb@example.com", "checked-in.csv"));
        cut.WaitForState(() => cut.Markup.Contains("Selected: checked-in.csv"), TimeSpan.FromSeconds(5));

        await Assert.That(cut.Markup).DoesNotContain("Maximum");
        await Assert.That(cut.Markup).DoesNotContain("Selected: approved.csv");
    }

    [Test]
    public async Task Preview_WithManyValidRows_ShowsPassedResultCommitAboveTableAndPaginates()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddTestAuthorization().SetAuthorized("Organizer").SetRoles(BethuyaRoles.Organizer);

        var eventApi = Substitute.For<IEventApi>();
        eventApi.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<EventDto>()));
        var importApi = Substitute.For<IImportApi>();
        importApi.ListTemplatesAsync("Registration", Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new List<ImportTemplateDto>()));

        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(importApi);
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<Imports>();
        cut.WaitForState(() => cut.FindAll("[data-test='import-file-field']").Count == 1, TimeSpan.FromSeconds(5));

        const int rowCount = 106;
        var batch = new ImportBatchDto(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Registration", Guid.CreateVersion7(),
            ImportBatchStatusDto.DryRunCompleted, rowCount, rowCount, 0, rowCount, 0, null, "organizer",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "luma.csv");
        var rows = Enumerable.Range(0, rowCount)
            .Select(i => new ImportRowPreviewDto(i, $"person{i}@example.com", "WillCreate", []))
            .ToList();

        typeof(Imports).GetField("_batch", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cut.Instance, batch);
        typeof(Imports).GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cut.Instance, new ImportPreviewReportDto(rowCount, rowCount, 0, rowCount, 0, rows));
        cut.Render();

        cut.WaitForState(() => cut.FindAll("[data-test='dry-run-result']").Count == 1, TimeSpan.FromSeconds(5));
        await Assert.That(cut.Find("[data-test='dry-run-result']").TextContent).Contains("Validation passed");
        await Assert.That(cut.Find("[data-test='validated-file-name']").TextContent).Contains("luma.csv");
        await Assert.That(cut.Find("[data-test='commit-import']").TextContent).Contains($"({rowCount} rows)");

        var markup = cut.Markup;
        await Assert.That(markup.IndexOf("data-test=\"commit-import\"", StringComparison.Ordinal))
            .IsLessThan(markup.IndexOf("data-test=\"import-preview-table\"", StringComparison.Ordinal));

        var renderedRows = cut.FindAll("[data-test='import-preview-table'] tbody tr");
        await Assert.That(renderedRows.Count).IsEqualTo(25);

        var differentFile = Substitute.For<Microsoft.AspNetCore.Components.Forms.IBrowserFile>();
        differentFile.Name.Returns("checked-in.csv");
        var handleFileSelected = typeof(Imports).GetMethod("HandleFileSelected", BindingFlags.Instance | BindingFlags.NonPublic);
        await cut.InvokeAsync(() => handleFileSelected!.Invoke(cut.Instance, [new List<FileUploadItem> { new() { File = differentFile } }]));
        cut.Render();

        await Assert.That(cut.FindAll("[data-test='dry-run-result']")).IsEmpty();
        await Assert.That(cut.FindAll("[data-test='commit-import']")).IsEmpty();
        await Assert.That(cut.Markup).Contains("Selected: checked-in.csv");
    }

    [Test]
    public async Task Preview_WithErrorRows_ShowsWarningResultAndHidesCommit()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddTestAuthorization().SetAuthorized("Organizer").SetRoles(BethuyaRoles.Organizer);

        var eventApi = Substitute.For<IEventApi>();
        eventApi.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<EventDto>()));
        var importApi = Substitute.For<IImportApi>();
        importApi.ListTemplatesAsync("Registration", Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new List<ImportTemplateDto>()));

        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(importApi);
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<Imports>();
        cut.WaitForState(() => cut.FindAll("[data-test='import-file-field']").Count == 1, TimeSpan.FromSeconds(5));

        var batch = new ImportBatchDto(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Registration", Guid.CreateVersion7(),
            ImportBatchStatusDto.DryRunCompleted, 2, 1, 1, 1, 0, null, "organizer",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "luma.csv");
        List<ImportRowPreviewDto> rows =
        [
            new(0, "ok@example.com", "WillCreate", []),
            new(1, null, "Error", ["Email is required."]),
        ];

        typeof(Imports).GetField("_batch", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cut.Instance, batch);
        typeof(Imports).GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cut.Instance, new ImportPreviewReportDto(2, 1, 1, 1, 0, rows));
        cut.Render();

        cut.WaitForState(() => cut.FindAll("[data-test='dry-run-result']").Count == 1, TimeSpan.FromSeconds(5));
        await Assert.That(cut.Find("[data-test='dry-run-result']").TextContent).Contains("1 of 2 rows with errors");
        await Assert.That(cut.FindAll("[data-test='commit-import']")).IsEmpty();
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
        var uploadStream = new MemoryStream("a,b\n1,2"u8.ToArray());
        newFile.OpenReadStream(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(uploadStream);
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
        await Assert.That(uploadStream.CanRead).IsFalse();
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

    [Test]
    public async Task Render_WithStaleEventIdQuery_ShowsNoticeAndInlineGuardNamesMissingInputs()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddTestAuthorization().SetAuthorized("Organizer").SetRoles(BethuyaRoles.Organizer);

        var eventApi = Substitute.For<IEventApi>();
        eventApi.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<EventDto>
        {
            CreateEvent(Guid.CreateVersion7(), "Meetup A"),
            CreateEvent(Guid.CreateVersion7(), "Meetup B")
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

        var navigationManager = ctx.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo(navigationManager.GetUriWithQueryParameter("eventId", Guid.CreateVersion7().ToString()));

        var cut = ctx.RenderComponent<Imports>();
        cut.WaitForState(() => cut.FindAll("[data-test='import-stale-event']").Count == 1, TimeSpan.FromSeconds(5));

        var eventIdTextField = typeof(Imports).GetField("_eventIdText", BindingFlags.Instance | BindingFlags.NonPublic);
        await Assert.That(eventIdTextField!.GetValue(cut.Instance)).IsNull();

        var startDryRun = typeof(Imports).GetMethod("StartDryRunAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        await cut.InvokeAsync(() => (Task)startDryRun!.Invoke(cut.Instance, null)!);
        cut.Render();

        var hint = cut.Find("[data-test='dry-run-missing-inputs']").TextContent;
        await Assert.That(hint).Contains("an event");
        await Assert.That(hint).Contains("a file");
        await Assert.That(hint).DoesNotContain("template");
    }

    [Test]
    public async Task Render_WithStaleEventIdQueryAndSingleEvent_RequiresExplicitSelection()
    {
        using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddTestAuthorization().SetAuthorized("Organizer").SetRoles(BethuyaRoles.Organizer);

        var onlyEventId = Guid.CreateVersion7();
        var eventApi = Substitute.For<IEventApi>();
        eventApi.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<EventDto> { CreateEvent(onlyEventId, "Only Meetup") }));
        var importApi = Substitute.For<IImportApi>();
        importApi.ListTemplatesAsync("Registration", Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<ImportTemplateDto>()));

        ctx.Services.AddSingleton(eventApi);
        ctx.Services.AddSingleton(importApi);
        ctx.Services.AddBlazorBlueprintComponents();

        var navigationManager = ctx.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo(navigationManager.GetUriWithQueryParameter("eventId", Guid.CreateVersion7().ToString()));

        var cut = ctx.RenderComponent<Imports>();
        cut.WaitForState(() => cut.FindAll("[data-test='import-file-field']").Count == 1, TimeSpan.FromSeconds(5));

        var eventIdTextField = typeof(Imports).GetField("_eventIdText", BindingFlags.Instance | BindingFlags.NonPublic);
        await Assert.That(eventIdTextField!.GetValue(cut.Instance)).IsNull();
        await Assert.That(cut.FindAll("[data-test='import-stale-event']").Count).IsEqualTo(1);
    }

    private static EventDto CreateEvent(Guid id, string title) => new(
        Id: id,
        Title: title,
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
        FairnessTargets: new EventFairnessTargetsDto());
}
