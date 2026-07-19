using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static partial class EventEndpoints
{
    private static readonly Regex HashtagPattern = new(@"^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static void MapEventEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/events").WithTags("Events");

        group.MapGet("/", async (IEventRepository repo, CancellationToken ct) =>
        {
            var events = await repo.GetAllAsync(ct);
            var response = events.Select(MapToResponse).ToList();
            return Results.Ok(response);
        });

        group.MapGet("/{id:guid}", async (Guid id, IEventRepository repo, CancellationToken ct) =>
        {
            var evt = await repo.GetByIdAsync(id, ct);
            return evt is not null
                ? Results.Ok(MapToResponse(evt))
                : Results.NotFound();
        });

        group.MapGet("/slug/{hashtag}", async (string hashtag, IEventRepository repo, CancellationToken ct) =>
        {
            var evt = await repo.GetByHashtagAsync(hashtag, ct);
            return evt is not null
                ? Results.Ok(MapToResponse(evt))
                : Results.NotFound();
        });

        group.MapPost("/", async (PlanEventRequest request, IEventRepository repo, IImageUploadService imageUploadService, BethuyaDbContext dbContext, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                errors[nameof(request.Title)] = ["Title is required."];
            }
            else if (request.Title.Length > 200)
            {
                errors[nameof(request.Title)] = ["Title must be 200 characters or fewer."];
            }

            if (request.Capacity < 1 || request.Capacity > 10_000)
            {
                errors[nameof(request.Capacity)] = ["Capacity must be between 1 and 10,000."];
            }

            if (request.EndDate < request.StartDate)
            {
                errors[nameof(request.EndDate)] = ["End date must be on or after the start date."];
            }

            if (string.IsNullOrWhiteSpace(request.CreatedBy))
            {
                errors[nameof(request.CreatedBy)] = ["CreatedBy is required."];
            }

            if (!string.IsNullOrEmpty(request.Hashtag))
            {
                if (request.Hashtag.Length > 100)
                {
                    errors[nameof(request.Hashtag)] = ["Hashtag must be 100 characters or fewer."];
                }
                else if (!HashtagPattern.IsMatch(request.Hashtag))
                {
                    errors[nameof(request.Hashtag)] = ["Hashtag must start with a letter and contain only letters, digits, and underscores."];
                }
                else
                {
                    var existing = await repo.GetByHashtagAsync(request.Hashtag, ct);
                    if (existing is not null)
                        errors[nameof(request.Hashtag)] = [$"Hashtag '{request.Hashtag}' is already taken."];
                }
            }

            var newCoverPublicId = await ValidateCoverImageUrlAsync(request.CoverImageUrl, imageUploadService, errors, ct);
            ValidateOptionalHttpsUrl(request.GitHubFolderUrl, nameof(request.GitHubFolderUrl), errors);
            ValidateOptionalHttpsUrl(request.RegistrationUrl, nameof(request.RegistrationUrl), errors);

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var evt = new Event
            {
                Title = request.Title,
                Description = request.Description,
                Type = request.Type,
                Capacity = request.Capacity,
                StartDate = ToUtc(request.StartDate),
                EndDate = ToUtc(request.EndDate),
                Location = request.Location,
                Hashtag = string.IsNullOrEmpty(request.Hashtag) ? null : request.Hashtag,
                CreatedBy = request.CreatedBy,
                Status = request.Status,
                CoverImageUrl = request.CoverImageUrl,
                SessionizeEventId = NormalizeOptional(request.SessionizeEventId),
                GitHubFolderUrl = NormalizeOptional(request.GitHubFolderUrl),
                TeamsAnnouncementMessageId = NormalizeOptional(request.TeamsAnnouncementMessageId),
                RegistrationUrl = NormalizeOptional(request.RegistrationUrl),
                FairnessTargets = ToModel(request.FairnessTargets)
            };

            var executionStrategy = dbContext.Database.CreateExecutionStrategy();
            Event? created = null;
            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
                created = await repo.CreateAsync(evt, ct);
                if (newCoverPublicId is not null)
                {
                    await imageUploadService.MarkUploadAttachedAsync(newCoverPublicId, ct);
                }
                await transaction.CommitAsync(ct);
            });

            if (created is null)
            {
                throw new InvalidOperationException("Event creation failed to produce a persisted event.");
            }

            var response = MapToResponse(created);
            return Results.Created($"/api/events/{created.Id}", response);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateEventRequest request, IEventRepository repo, IImageUploadService imageUploadService, BethuyaDbContext dbContext, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger(nameof(EventEndpoints));
            var evt = await repo.GetByIdAsync(id, ct);
            if (evt is null) return Results.NotFound();

            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                errors[nameof(request.Title)] = ["Title is required."];
            }
            else if (request.Title.Length > 200)
            {
                errors[nameof(request.Title)] = ["Title must be 200 characters or fewer."];
            }

            if (request.Capacity < 1 || request.Capacity > 10_000)
            {
                errors[nameof(request.Capacity)] = ["Capacity must be between 1 and 10,000."];
            }

            if (request.EndDate < request.StartDate)
            {
                errors[nameof(request.EndDate)] = ["End date must be on or after the start date."];
            }

            var newCoverPublicId = await ValidateCoverImageUrlAsync(
                request.CoverImageUrl,
                imageUploadService,
                errors,
                ct,
                evt.CoverImageUrl);
            ValidateOptionalHttpsUrl(request.GitHubFolderUrl, nameof(request.GitHubFolderUrl), errors);
            ValidateOptionalHttpsUrl(request.RegistrationUrl, nameof(request.RegistrationUrl), errors);

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var previousCoverImageUrl = evt.CoverImageUrl;
            evt.Title = request.Title;
            evt.Description = request.Description;
            evt.Type = request.Type;
            evt.Capacity = request.Capacity;
            evt.StartDate = ToUtc(request.StartDate);
            evt.EndDate = ToUtc(request.EndDate);
            evt.Location = request.Location;
            evt.Status = request.Status;
            evt.CoverImageUrl = request.CoverImageUrl;
            evt.SessionizeEventId = NormalizeOptional(request.SessionizeEventId);
            evt.GitHubFolderUrl = NormalizeOptional(request.GitHubFolderUrl);
            evt.TeamsAnnouncementMessageId = NormalizeOptional(request.TeamsAnnouncementMessageId);
            evt.RegistrationUrl = NormalizeOptional(request.RegistrationUrl);
            evt.FairnessTargets = request.FairnessTargets is null
                ? evt.FairnessTargets
                : ToModel(request.FairnessTargets);

            var executionStrategy = dbContext.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
                await repo.UpdateAsync(evt, ct);
                if (newCoverPublicId is not null)
                {
                    await imageUploadService.MarkUploadAttachedAsync(newCoverPublicId, ct);
                }
                await transaction.CommitAsync(ct);
            });

            await DeletePreviousCoverImageIfChangedAsync(previousCoverImageUrl, request.CoverImageUrl, imageUploadService, logger, ct);
            return Results.Ok(MapToResponse(evt));
        });

        group.MapGet("/{id:guid}/fairness-targets", async (Guid id, IEventRepository repo, CancellationToken ct) =>
        {
            var evt = await repo.GetByIdAsync(id, ct);
            return evt is null
                ? Results.NotFound()
                : Results.Ok(ToContract(evt.FairnessTargets));
        });

        group.MapPut("/{id:guid}/fairness-targets", async (Guid id, EventFairnessTargetsContract request, IEventRepository repo, CancellationToken ct) =>
        {
            var evt = await repo.GetByIdAsync(id, ct);
            if (evt is null)
            {
                return Results.NotFound();
            }

            evt.FairnessTargets = ToModel(request);
            await repo.UpdateAsync(evt, ct);
            return Results.Ok(ToContract(evt.FairnessTargets));
        });

        group.MapDelete("/{id:guid}", async (Guid id, IEventRepository repo, IImageUploadService imageUploadService, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger(nameof(EventEndpoints));
            var evt = await repo.GetByIdAsync(id, ct);
            if (evt is null)
            {
                return Results.NoContent();
            }

            await repo.DeleteAsync(id, ct);
            await DeletePreviousCoverImageIfChangedAsync(evt.CoverImageUrl, null, imageUploadService, logger, ct);
            return Results.NoContent();
        });

        group.MapGet("/{id:guid}/sessionize/preview", async (Guid id, [FromServices] ISessionIngestionService ingestionService, CancellationToken ct) =>
        {
            try
            {
                var sessions = await ingestionService.PreviewSessionizeAsync(id, ct);
                return Results.Ok(sessions.Select(ToContract).ToArray());
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["sessionize"] = [ex.Message] });
            }
            catch (InvalidOperationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["sessionize"] = [ex.Message] });
            }
        });

        group.MapPost("/{id:guid}/sessionize/import", async (Guid id, [FromServices] ISessionIngestionService ingestionService, CancellationToken ct) =>
        {
            try
            {
                var importedCount = await ingestionService.ImportSessionizeAsync(id, ct);
                return Results.Ok(new SessionizeImportResponse(importedCount));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["sessionize"] = [ex.Message] });
            }
            catch (InvalidOperationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["sessionize"] = [ex.Message] });
            }
        });

        group.MapPost("/{id:guid}/lifecycle", async (Guid id, [FromBody] EventLifecycleTransitionRequest request, [FromServices] IEventLifecycleOrchestrator orchestrator, CancellationToken ct) =>
            await ExecuteLifecycleOperationAsync(() => orchestrator.TransitionAsync(id, request.TargetState, request.Actor, ct)));

        group.MapPost("/{id:guid}/publish", async (Guid id, [FromBody] PublishEventRequest request, [FromServices] IEventLifecycleOrchestrator orchestrator, CancellationToken ct) =>
            await ExecuteLifecycleOperationAsync(() => orchestrator.PublishAsync(id, request.Actor, request.RegistrationUrl, ct)));

        group.MapPost("/{id:guid}/schedule-alterations", async (Guid id, [FromBody] ScheduleAlterationRequest request, [FromServices] IEventLifecycleOrchestrator orchestrator, CancellationToken ct) =>
            await ExecuteLifecycleOperationAsync(() => orchestrator.AlterScheduleAsync(id, request.Actor, request.Reason, ct)));

        group.MapPost("/{id:guid}/complete", async (Guid id, [FromBody] CompleteEventRequest request, [FromServices] IEventLifecycleOrchestrator orchestrator, CancellationToken ct) =>
            await ExecuteLifecycleOperationAsync(() => orchestrator.CompleteAsync(id, request.Actor, request.AssetDueAt, ct)));

        group.MapPost("/{id:guid}/archive", async (Guid id, [FromBody] ArchiveEventRequest request, [FromServices] IEventLifecycleOrchestrator orchestrator, CancellationToken ct) =>
            await ExecuteLifecycleOperationAsync(() => orchestrator.ArchiveAsync(id, request.Actor, request.OverrideMissingAssets, ct)));
    }

    private static async Task<IResult> ExecuteLifecycleOperationAsync(
        Func<Task<EventLifecycleOperationResult>> operation)
    {
        try
        {
            return Results.Ok(ToContract(await operation()));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [ex.Message] });
        }
        catch (InvalidOperationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["lifecycle"] = [ex.Message] });
        }
    }

    private static async Task<string?> ValidateCoverImageUrlAsync(
        string? coverImageUrl,
        IImageUploadService imageUploadService,
        Dictionary<string, string[]> errors,
        CancellationToken ct,
        string? existingCoverImageUrl = null)
    {
        if (string.IsNullOrEmpty(coverImageUrl))
            return null;

        if (coverImageUrl.Length > 2048)
        {
            errors[nameof(coverImageUrl)] = ["Cover image URL must be 2,048 characters or fewer."];
            return null;
        }

        if (string.Equals(coverImageUrl, existingCoverImageUrl, StringComparison.Ordinal))
        {
            return null;
        }

        if (!Uri.TryCreate(coverImageUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            errors[nameof(coverImageUrl)] = ["Cover image URL must be a valid absolute HTTPS URL."];
            return null;
        }

        if (!string.Equals(uri.Host, "res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            errors[nameof(coverImageUrl)] = ["Cover image URL must point to Cloudinary."];
            return null;
        }

        if (!imageUploadService.TryGetPublicId(coverImageUrl, out var publicId))
        {
            errors[nameof(coverImageUrl)] = ["Cover image URL is not a valid Bethuya Cloudinary image URL."];
            return null;
        }

        if (!await imageUploadService.IsPendingUploadAsync(publicId, ct))
        {
            errors[nameof(coverImageUrl)] = ["Cover image upload has expired or is not recognized. Please upload it again."];
            return null;
        }

        if (!await imageUploadService.UploadedAssetExistsAsync(publicId, ct))
        {
            errors[nameof(coverImageUrl)] = ["Cover image upload is incomplete. Please wait for the upload to finish or try again."];
            return null;
        }

        return publicId;
    }

    private static async Task DeletePreviousCoverImageIfChangedAsync(
        string? previousCoverImageUrl,
        string? currentCoverImageUrl,
        IImageUploadService imageUploadService,
        ILogger logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(previousCoverImageUrl)
            || string.Equals(previousCoverImageUrl, currentCoverImageUrl, StringComparison.Ordinal)
            || !imageUploadService.TryGetPublicId(previousCoverImageUrl, out var previousPublicId))
        {
            return;
        }

        try
        {
            await imageUploadService.DeleteStoredImageAsync(previousPublicId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            LogFailedToDeletePreviousCoverImage(logger, previousPublicId, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            LogFailedToDeletePreviousCoverImage(logger, previousPublicId, ex);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Failed to delete previous cover image {PublicId} after DB operation.")]
    private static partial void LogFailedToDeletePreviousCoverImage(ILogger logger, string publicId, Exception exception);

    private static EventResponse MapToResponse(Event evt) =>
        new(
            evt.Id,
            evt.Title,
            evt.Description,
            evt.Type.ToString(),
            evt.Status.ToString(),
            evt.Capacity,
            evt.StartDate,
            evt.EndDate,
            evt.Location,
            evt.CreatedBy,
            evt.CreatedAt,
            evt.Hashtag,
            evt.CoverImageUrl,
            evt.LifecycleState.ToString(),
            evt.SessionizeEventId,
            evt.GitHubFolderUrl,
            evt.TeamsAnnouncementMessageId,
            evt.RegistrationUrl,
            evt.PublishedAt,
            evt.CompletedAt,
            evt.ArchivedAt,
            ToContract(evt.FairnessTargets));

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTimeOffset ToUtc(DateTimeOffset value) => value.ToUniversalTime();

    private static void ValidateOptionalHttpsUrl(
        string? value,
        string fieldName,
        Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (value.Length > 2048)
        {
            errors[fieldName] = [$"{fieldName} must be 2,048 characters or fewer."];
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            errors[fieldName] = [$"{fieldName} must be a valid absolute HTTPS URL."];
        }
    }

    private static EventLifecycleOperationResponse ToContract(EventLifecycleOperationResult result)
        => new(
            result.EventId,
            result.LifecycleState.ToString(),
            result.GitHubFolderUrl,
            result.RegistrationUrl,
            result.Message);

    private static NormalizedSessionContract ToContract(NormalizedSession session)
        => new(
            session.Title,
            session.Description,
            session.Speakers.Select(s => new NormalizedSpeakerContract(
                s.Name,
                s.GitHubHandle,
                s.TwitterHandle,
                s.AvatarUrl)).ToArray(),
            session.Source.ToString(),
            session.SourceSessionId,
            session.PreferredStartTime,
            session.Duration);

    private static EventFairnessTargetsContract ToContract(EventFairnessTargets? source)
    {
        var settings = source ?? EventFairnessTargets.Default;
        return new EventFairnessTargetsContract(
            GeoOutsideDominantMinPercent: settings.GeoOutsideDominantMinPercent,
            LocalLanguageMinPercent: settings.LocalLanguageMinPercent,
            UnderrepresentedEducationMinPercent: settings.UnderrepresentedEducationMinPercent,
            EnableSocioeconomicDimension: settings.EnableSocioeconomicDimension,
            UnderrepresentedSocioeconomicMinPercent: settings.UnderrepresentedSocioeconomicMinPercent,
            KAnonymityThreshold: settings.KAnonymityThreshold,
            GenderDiversityMinPercent: settings.GenderDiversityMinPercent);
    }

    private static EventFairnessTargets ToModel(EventFairnessTargetsContract? source)
    {
        var settings = source ?? new EventFairnessTargetsContract();
        return new EventFairnessTargets
        {
            GeoOutsideDominantMinPercent = ClampPercent(settings.GeoOutsideDominantMinPercent),
            LocalLanguageMinPercent = ClampPercent(settings.LocalLanguageMinPercent),
            UnderrepresentedEducationMinPercent = ClampPercent(settings.UnderrepresentedEducationMinPercent),
            EnableSocioeconomicDimension = settings.EnableSocioeconomicDimension,
            UnderrepresentedSocioeconomicMinPercent = settings.UnderrepresentedSocioeconomicMinPercent is null
                ? null
                : ClampPercent(settings.UnderrepresentedSocioeconomicMinPercent.Value),
            GenderDiversityMinPercent = ClampPercent(settings.GenderDiversityMinPercent),
            KAnonymityThreshold = Math.Max(EventFairnessTargets.DefaultKAnonymityThreshold, settings.KAnonymityThreshold)
        };
    }

    private static double ClampPercent(double value)
        => value switch
        {
            < 0 => 0,
            > 1 => 1,
            _ => value
        };
}
