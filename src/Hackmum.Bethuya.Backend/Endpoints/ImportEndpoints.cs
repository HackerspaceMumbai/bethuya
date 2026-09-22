using System.Security.Claims;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Backend.Endpoints;

/// <summary>
/// Registration/Attendance import API: upload -&gt; Dry Run -&gt; Commit, plus Import Template CRUD.
/// All endpoints require the Organizer (or Admin) role.
/// </summary>
public static class ImportEndpoints
{
    private const long MaxUploadSizeBytes = 10 * 1024 * 1024;

    public static void MapImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/import")
            .WithTags("Import")
            .RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);

        group.MapPost("/batches", UploadAndRunDryRunAsync)
            .DisableAntiforgery();

        group.MapPost("/batches/{importBatchId:guid}/dry-run", RerunDryRunAsync);
        group.MapGet("/batches/{importBatchId:guid}", GetBatchAsync);
        group.MapGet("/batches/{importBatchId:guid}/preview", GetPreviewAsync);
        group.MapPost("/batches/{importBatchId:guid}/commit", CommitAsync);
        group.MapGet("/events/{eventId:guid}/batches", ListBatchesForEventAsync);

        group.MapGet("/templates", ListTemplatesAsync);
        group.MapGet("/templates/{templateId:guid}", GetTemplateAsync);
        group.MapPost("/templates", CreateTemplateAsync);
        group.MapPost("/templates/{templateId:guid}/clone", CloneTemplateAsync);
        group.MapPut("/templates/{templateId:guid}", UpdateTemplateAsync);
    }

    private static async Task<IResult> UploadAndRunDryRunAsync(
        Guid eventId,
        Guid importTemplateId,
        ImportKind importKind,
        IFormFile file,
        ClaimsPrincipal user,
        BethuyaDbContext db,
        ImportTemplateService templateService,
        ImportDryRunService dryRunService,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["An import file is required."]
            });
        }

        if (file.Length > MaxUploadSizeBytes)
        {
            return Results.Problem("File exceeds the 10 MB limit.", statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var subject = GetSubject(user);
        if (subject is null)
        {
            return Results.Unauthorized();
        }

        if (!await CanAccessEventAsync(eventId, user, db, ct))
        {
            return Results.NotFound();
        }

        try
        {
            await templateService.GetForUserAsync(
                importTemplateId,
                subject.UserId,
                user.IsInRole(BethuyaRoleNames.Admin),
                ct);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.NotFound();
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);

        try
        {
            var batch = await dryRunService.StartAsync(
                eventId,
                importTemplateId,
                importKind,
                Path.GetFileName(file.FileName),
                file.ContentType,
                buffer.ToArray(),
                subject.UserId,
                ct);

            return Results.Ok(ImportBatchResponse.FromModel(batch, file.FileName));
        }
        catch (ImportFileParseException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> RerunDryRunAsync(
        Guid importBatchId,
        ClaimsPrincipal user,
        BethuyaDbContext db,
        ImportDryRunService dryRunService,
        CancellationToken ct)
    {
        if (!await CanAccessBatchAsync(importBatchId, user, db, ct))
        {
            return Results.NotFound();
        }

        try
        {
            var batch = await dryRunService.RerunAsync(importBatchId, ct);
            return Results.Ok(ImportBatchResponse.FromModel(batch));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetBatchAsync(
        Guid importBatchId,
        ClaimsPrincipal user,
        BethuyaDbContext db,
        CancellationToken ct)
    {
        if (!await CanAccessBatchAsync(importBatchId, user, db, ct))
        {
            return Results.NotFound();
        }

        var batch = await db.ImportBatches
            .Include(b => b.ImportArtifact)
            .AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == importBatchId, ct);

        return batch is null ? Results.NotFound() : Results.Ok(ImportBatchResponse.FromModel(batch));
    }

    private static async Task<IResult> GetPreviewAsync(
        Guid importBatchId,
        ClaimsPrincipal user,
        BethuyaDbContext db,
        ImportDryRunService dryRunService,
        CancellationToken ct)
    {
        if (!await CanAccessBatchAsync(importBatchId, user, db, ct))
        {
            return Results.NotFound();
        }

        try
        {
            var preview = await dryRunService.GetPreviewAsync(importBatchId, ct);
            return Results.Ok(preview);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(ex.Message);
        }
    }

    private static async Task<IResult> CommitAsync(
        Guid importBatchId,
        ClaimsPrincipal user,
        BethuyaDbContext db,
        ImportCommitService commitService,
        CancellationToken ct)
    {
        if (!await CanAccessBatchAsync(importBatchId, user, db, ct))
        {
            return Results.NotFound();
        }

        try
        {
            var batch = await commitService.CommitAsync(importBatchId, ct);
            return Results.Ok(ImportBatchResponse.FromModel(batch));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> ListBatchesForEventAsync(
        Guid eventId,
        ClaimsPrincipal user,
        BethuyaDbContext db,
        CancellationToken ct)
    {
        if (!await CanAccessEventAsync(eventId, user, db, ct))
        {
            return Results.NotFound();
        }

        var batches = await db.ImportBatches
            .Include(b => b.ImportArtifact)
            .AsNoTracking()
            .Where(b => b.EventId == eventId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        return Results.Ok(batches.Select(b => ImportBatchResponse.FromModel(b)).ToList());
    }

    private static async Task<IResult> ListTemplatesAsync(
        ImportKind? importKind,
        ClaimsPrincipal user,
        ImportTemplateService templateService,
        CancellationToken ct)
    {
        var subject = GetSubject(user);
        if (subject is null)
        {
            return Results.Unauthorized();
        }

        var templates = await templateService.ListAsync(
            subject.UserId,
            importKind,
            user.IsInRole(BethuyaRoleNames.Admin),
            ct);
        return Results.Ok(templates.Select(ImportTemplateResponse.FromModel).ToList());
    }

    private static async Task<IResult> GetTemplateAsync(
        Guid templateId,
        ClaimsPrincipal user,
        ImportTemplateService templateService,
        CancellationToken ct)
    {
        var subject = GetSubject(user);
        if (subject is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var template = await templateService.GetForUserAsync(
                templateId,
                subject.UserId,
                user.IsInRole(BethuyaRoleNames.Admin),
                ct);
            return Results.Ok(ImportTemplateResponse.FromModel(template));
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> CreateTemplateAsync(
        CreateImportTemplateRequest request,
        ClaimsPrincipal user,
        ImportTemplateService templateService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.ColumnMappings.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = string.IsNullOrWhiteSpace(request.Name) ? ["Name is required."] : [],
                ["columnMappings"] = request.ColumnMappings.Count == 0 ? ["At least one column mapping is required."] : []
            });
        }

        var subject = GetSubject(user);
        if (subject is null)
        {
            return Results.Unauthorized();
        }

        var template = await templateService.CreateAsync(
            request.Name,
            request.SourceKind,
            request.ImportKind,
            subject.UserId,
            request.ColumnMappings.Select(m => new ImportColumnMappingInput(m.SourceColumnName, m.TargetField)).ToList(),
            ct);

        return Results.Ok(ImportTemplateResponse.FromModel(template));
    }

    private static async Task<IResult> CloneTemplateAsync(
        Guid templateId,
        CloneImportTemplateRequest request,
        ClaimsPrincipal user,
        ImportTemplateService templateService,
        CancellationToken ct)
    {
        var subject = GetSubject(user);
        if (subject is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var clone = await templateService.CloneAsync(
                templateId,
                subject.UserId,
                user.IsInRole(BethuyaRoleNames.Admin),
                request.NewName,
                ct);
            return Results.Ok(ImportTemplateResponse.FromModel(clone));
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> UpdateTemplateAsync(
        Guid templateId,
        UpdateImportTemplateRequest request,
        ClaimsPrincipal user,
        ImportTemplateService templateService,
        CancellationToken ct)
    {
        var subject = GetSubject(user);
        if (subject is null)
        {
            return Results.Unauthorized();
        }

        var isAdmin = user.IsInRole(BethuyaRoleNames.Admin);
        if (string.IsNullOrWhiteSpace(request.Name) || request.ColumnMappings is null || request.ColumnMappings.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["template"] = ["A template name and at least one column mapping are required."]
            });
        }

        try
        {
            var template = await templateService.UpdateAsync(
                templateId,
                subject.UserId,
                isAdmin,
                request.Name,
                request.ColumnMappings.Select(m => new ImportColumnMappingInput(m.SourceColumnName, m.TargetField)).ToList(),
                ct);

            return Results.Ok(ImportTemplateResponse.FromModel(template));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static CommunitySubjectContext? GetSubject(ClaimsPrincipal user) => user.GetSubject();

    private static async Task<bool> CanAccessBatchAsync(
        Guid importBatchId,
        ClaimsPrincipal user,
        BethuyaDbContext db,
        CancellationToken ct)
    {
        var subject = GetSubject(user);
        if (subject is null)
        {
            return false;
        }

        if (user.IsInRole(BethuyaRoleNames.Admin))
        {
            return await db.ImportBatches.AnyAsync(b => b.Id == importBatchId, ct);
        }

        return await db.ImportBatches
            .Where(b => b.Id == importBatchId)
            .Join(db.Events, batch => batch.EventId, evt => evt.Id, (batch, evt) =>
                batch.CreatedByUserId == subject.UserId || evt.CreatedBy == subject.UserId)
            .AnyAsync(ct);
    }

    private static async Task<bool> CanAccessEventAsync(
        Guid eventId,
        ClaimsPrincipal user,
        BethuyaDbContext db,
        CancellationToken ct)
    {
        var subject = GetSubject(user);
        if (subject is null)
        {
            return false;
        }

        return user.IsInRole(BethuyaRoleNames.Admin) ||
            await db.Events.AnyAsync(e => e.Id == eventId && e.CreatedBy == subject.UserId, ct);
    }
}
