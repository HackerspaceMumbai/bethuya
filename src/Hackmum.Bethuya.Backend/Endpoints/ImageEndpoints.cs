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

            if (!await IsValidImageContentAsync(stream))
            {
                errors["file"] = ["File content does not match a supported image format (JPEG, PNG, WebP, GIF)."];
                return Results.ValidationProblem(errors);
            }

            stream.Position = 0;
            var safeFileName = SanitizeFileName(file.FileName);
            var url = await uploadService.UploadAsync(stream, safeFileName, ct);
            return Results.Ok(new ImageUploadResponse(url));
        }).DisableAntiforgery();
    }

    /// <summary>Response returned after a successful image upload.</summary>
    public sealed record ImageUploadResponse(string Url);

    /// <summary>Validates file content via magic bytes to prevent Content-Type spoofing.</summary>
    private static async Task<bool> IsValidImageContentAsync(Stream stream)
    {
        // Longest signature we check is 12 bytes (WebP: RIFF....WEBP)
        var header = new byte[12];
        var bytesRead = await stream.ReadAsync(header);
        if (bytesRead < 4) return false;

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return true;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytesRead >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return true;

        // GIF: GIF87a or GIF89a
        if (bytesRead >= 6
            && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38
            && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61)
            return true;

        // WebP: RIFF....WEBP (bytes 0-3 = "RIFF", bytes 8-11 = "WEBP")
        if (bytesRead >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return true;

        return false;
    }

    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    /// <summary>
    /// Generates a safe filename from a potentially malicious client-supplied name.
    /// Uses GUID + validated original extension to eliminate path traversal and special chars.
    /// </summary>
    private static string SanitizeFileName(string? clientFileName)
    {
        var extension = ".png"; // default

        if (!string.IsNullOrWhiteSpace(clientFileName))
        {
            // Normalize separators so Path.GetFileName works cross-platform
            var normalized = clientFileName.Replace('\\', '/');
            var nameOnly = Path.GetFileName(normalized);
            var ext = Path.GetExtension(nameOnly).ToLowerInvariant();

            if (AllowedExtensions.Contains(ext))
                extension = ext;
        }

        return $"{Guid.NewGuid():N}{extension}";
    }
}
