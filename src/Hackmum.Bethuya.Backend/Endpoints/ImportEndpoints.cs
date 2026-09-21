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
        ImportDryRunService dryRunService,
        CancellationToken ct)
    {
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
        BethuyaDbContext db,
        CancellationToken ct)
    {
        var batch = await db.ImportBatches
            .Include(b => b.ImportArtifact)
            .AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == importBatchId, ct);

        return batch is null ? Results.NotFound() : Results.Ok(ImportBatchResponse.FromModel(batch));
    }

    private static async Task<IResult> GetPreviewAsync(
        Guid importBatchId,
        ImportDryRunService dryRunService,
        CancellationToken ct)
    {
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
        ImportCommitService commitService,
        CancellationToken ct)
    {
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
        BethuyaDbContext db,
        CancellationToken ct)
    {
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

        var templates = await templateService.ListAsync(subject.UserId, importKind, ct);
        return Results.Ok(templates.Select(ImportTemplateResponse.FromModel).ToList());
    }

    private static async Task<IResult> GetTemplateAsync(
        Guid templateId,
        ImportTemplateService templateService,
        CancellationToken ct)
    {
        try
        {
            var template = await templateService.GetAsync(templateId, ct);
            return Results.Ok(ImportTemplateResponse.FromModel(template));
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(ex.Message);
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
            var clone = await templateService.CloneAsync(templateId, subject.UserId, request.NewName, ct);
            return Results.Ok(ImportTemplateResponse.FromModel(clone));
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(ex.Message);
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
}
