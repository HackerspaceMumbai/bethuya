using Hackmum.Bethuya.Core.Services;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Backend.Endpoints;

/// <summary>Endpoints for signed direct image uploads and temporary image cleanup.</summary>
public static class ImageEndpoints
{
    public static void MapImageEndpoints(this WebApplication app)
    {
        MapImageRoutes(app.MapGroup("/api/organizer/images")
            .WithTags("Images")
            .RequireRateLimiting(RateLimitPolicies.Ai)
            .RequireAuthorization(BethuyaPolicyNames.RequireOrganizer));
        MapImageRoutes(app.MapGroup("/api/images")
            .WithTags("Images")
            .RequireRateLimiting(RateLimitPolicies.Ai)
            .RequireAuthorization(BethuyaPolicyNames.RequireOrganizer));
    }

    private static void MapImageRoutes(RouteGroupBuilder group)
    {
        group.MapPost("/direct-upload/session", static async (
            CreateDirectUploadSessionRequest request,
            IImageUploadService uploadService,
            CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(request.FileName))
            {
                errors[nameof(request.FileName)] = ["A file name is required."];
            }

            if (string.IsNullOrWhiteSpace(request.ContentType))
            {
                errors[nameof(request.ContentType)] = ["A content type is required."];
            }

            if (request.FileSize <= 0)
            {
                errors[nameof(request.FileSize)] = ["A file size greater than zero is required."];
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            try
            {
                var session = await uploadService.CreateDirectUploadSessionAsync(
                    request.FileName,
                    request.ContentType,
                    request.FileSize,
                    ct);

                return Results.Ok(new DirectUploadSessionResponse(
                    session.UploadUrl,
                    session.CloudName,
                    session.ApiKey,
                    session.PublicId,
                    session.Timestamp,
                    session.Signature,
                    session.DeleteToken,
                    session.UploadPreset,
                    session.MaxFileSize,
                    session.AllowedFormats));
            }
            catch (ArgumentException ex)
            {
                errors[ex.ParamName ?? "file"] = [ex.Message];
                return Results.ValidationProblem(errors);
            }
        });

        group.MapPost("/direct-upload/delete", static async (
            DeletePendingDirectUploadRequest request,
            IImageUploadService uploadService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.PublicId) || string.IsNullOrWhiteSpace(request.DeleteToken))
            {
                var errors = new Dictionary<string, string[]>();
                if (string.IsNullOrWhiteSpace(request.PublicId))
                {
                    errors[nameof(request.PublicId)] = ["Public ID is required."];
                }

                if (string.IsNullOrWhiteSpace(request.DeleteToken))
                {
                    errors[nameof(request.DeleteToken)] = ["Delete token is required."];
                }

                return Results.ValidationProblem(errors);
            }

            var deleted = await uploadService.DeletePendingUploadAsync(request.PublicId, request.DeleteToken, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }

    /// <summary>Request body for a signed direct-upload session.</summary>
    public sealed record CreateDirectUploadSessionRequest(string FileName, string ContentType, long FileSize);

    /// <summary>Response body for a signed direct-upload session.</summary>
    public sealed record DirectUploadSessionResponse(
        string UploadUrl,
        string CloudName,
        string ApiKey,
        string PublicId,
        long Timestamp,
        string Signature,
        string DeleteToken,
        string? UploadPreset,
        long MaxFileSize,
        string AllowedFormats);

    /// <summary>Deletes a pending direct upload before it is attached to an event.</summary>
    public sealed record DeletePendingDirectUploadRequest(string PublicId, string DeleteToken);
}
