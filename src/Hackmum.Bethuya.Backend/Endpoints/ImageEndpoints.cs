using Hackmum.Bethuya.Core.Services;

namespace Hackmum.Bethuya.Backend.Endpoints;

/// <summary>Endpoints for uploading images to cloud storage.</summary>
public static class ImageEndpoints
{
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    ];

    public static void MapImageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/images").WithTags("Images");

        group.MapPost("/upload", async (IFormFile file, IImageUploadService uploadService, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();

            if (file is null || file.Length == 0)
            {
                errors["file"] = ["A file is required."];
                return Results.ValidationProblem(errors);
            }

            if (file.Length > MaxFileSize)
            {
                errors["file"] = ["File size must not exceed 5 MB."];
                return Results.ValidationProblem(errors);
            }

            if (!AllowedContentTypes.Contains(file.ContentType))
            {
                errors["file"] = ["Only JPEG, PNG, WebP, and GIF images are accepted."];
                return Results.ValidationProblem(errors);
            }

            await using var stream = file.OpenReadStream();
            var url = await uploadService.UploadAsync(stream, file.FileName, ct);

            return Results.Ok(new ImageUploadResponse(url));
        }).DisableAntiforgery();
    }

    /// <summary>Response returned after a successful image upload.</summary>
    public sealed record ImageUploadResponse(string Url);
}
