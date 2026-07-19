using System.Security.Cryptography;
using System.Text;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Infrastructure.Services;

/// <summary>Creates signed Cloudinary direct-upload sessions and manages temporary upload lifecycle.</summary>
public sealed partial class CloudinaryImageUploadService(
    BethuyaDbContext db,
    IOptions<CloudinaryOptions> options,
    TimeProvider timeProvider,
    ILogger<CloudinaryImageUploadService> logger) : IImageUploadService
{
    private const long MaxFileSize = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    ];

    private static readonly HashSet<string> AllowedExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp"
    ];

    private static readonly string AllowedFormatsCsv = "jpg,jpeg,png,gif,webp";

    private readonly CloudinaryOptions _options = options.Value;

    public async Task<DirectImageUploadSession> CreateDirectUploadSessionAsync(
        string fileName,
        string contentType,
        long fileSize,
        CancellationToken ct = default)
    {
        ValidateFileMetadata(fileName, contentType, fileSize);
        EnsureCloudinaryConfigured();

        var publicId = $"{_options.PendingUploadFolder.TrimEnd('/')}/{Guid.CreateVersion7():N}";
        var timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var deleteToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var pendingUpload = new PendingImageUpload
        {
            PublicId = publicId,
            DeleteTokenHash = ComputeDeleteTokenHash(deleteToken),
            RequestedAt = timeProvider.GetUtcNow()
        };

        db.PendingImageUploads.Add(pendingUpload);
        await db.SaveChangesAsync(ct);

        return new DirectImageUploadSession(
            UploadUrl: $"https://api.cloudinary.com/v1_1/{_options.CloudName}/image/upload",
            CloudName: _options.CloudName,
            ApiKey: _options.ApiKey,
            PublicId: publicId,
            Timestamp: timestamp,
            Signature: CreateSignature(publicId, timestamp),
            DeleteToken: deleteToken,
            UploadPreset: string.IsNullOrWhiteSpace(_options.UploadPreset) ? null : _options.UploadPreset,
            MaxFileSize: MaxFileSize,
            AllowedFormats: AllowedFormatsCsv);
    }

    public async Task<bool> DeletePendingUploadAsync(string publicId, string deleteToken, CancellationToken ct = default)
    {
        var tokenHash = ComputeDeleteTokenHash(deleteToken);
        var pendingUpload = await db.PendingImageUploads
            .SingleOrDefaultAsync(
                upload => upload.PublicId == publicId
                    && upload.DeleteTokenHash == tokenHash
                    && upload.AttachedAt == null
                    && upload.DeletedAt == null,
                ct);

        if (pendingUpload is null)
        {
            return false;
        }

        await DeleteStoredImageAsync(publicId, ct);
        pendingUpload.DeletedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task MarkUploadAttachedAsync(string publicId, CancellationToken ct = default)
    {
        var pendingUpload = await db.PendingImageUploads
            .SingleOrDefaultAsync(upload => upload.PublicId == publicId && upload.DeletedAt == null, ct);

        if (pendingUpload is null)
        {
            return;
        }

        pendingUpload.AttachedAt ??= timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public Task<bool> IsPendingUploadAsync(string publicId, CancellationToken ct = default) =>
        db.PendingImageUploads.AnyAsync(
            upload => upload.PublicId == publicId
                && upload.DeletedAt == null
                && upload.AttachedAt == null,
            ct);

    public async Task<bool> UploadedAssetExistsAsync(string publicId, CancellationToken ct = default)
    {
        var result = await CreateCloudinaryClient().GetResourceAsync(publicId, ct);
        if (result.Error is null)
        {
            return true;
        }

        if (result.Error.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new InvalidOperationException($"Cloudinary resource lookup failed: {result.Error.Message}");
    }

    public async Task DeleteStoredImageAsync(string publicId, CancellationToken ct = default)
    {
        var result = await CreateCloudinaryClient().DestroyAsync(new DeletionParams(publicId));

        if (result.Error is not null)
        {
            throw new InvalidOperationException($"Cloudinary delete failed: {result.Error.Message}");
        }

        if (!string.Equals(result.Result, "ok", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(result.Result, "not found", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cloudinary delete failed: {result.Result}");
        }
    }

    public bool TryGetPublicId(string? imageUrl, out string publicId)
    {
        publicId = string.Empty;

        if (string.IsNullOrWhiteSpace(imageUrl)
            || string.IsNullOrWhiteSpace(_options.CloudName)
            || !Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Host, "res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 4
            || !string.Equals(segments[0], _options.CloudName, StringComparison.Ordinal)
            || !string.Equals(segments[1], "image", StringComparison.Ordinal)
            || !string.Equals(segments[2], "upload", StringComparison.Ordinal))
        {
            return false;
        }

        var publicIdSegments = segments[3..];
        if (publicIdSegments.Length == 0)
        {
            return false;
        }

        if (publicIdSegments[0].Length > 1
            && publicIdSegments[0][0] == 'v'
            && publicIdSegments[0][1..].All(char.IsDigit))
        {
            publicIdSegments = publicIdSegments[1..];
        }

        if (publicIdSegments.Length == 0)
        {
            return false;
        }

        var lastSegment = publicIdSegments[^1];
        var extensionIndex = lastSegment.LastIndexOf('.');
        if (extensionIndex <= 0)
        {
            return false;
        }

        publicIdSegments[^1] = lastSegment[..extensionIndex];
        publicId = string.Join('/', publicIdSegments);
        return string.Equals(publicId, _options.EventCoverRootFolder, StringComparison.Ordinal)
            || publicId.StartsWith($"{_options.EventCoverRootFolder}/", StringComparison.Ordinal);
    }

    public async Task<int> CleanupExpiredPendingUploadsAsync(CancellationToken ct = default)
    {
        if (!IsCloudinaryConfigured())
        {
            return 0;
        }

        var cutoff = timeProvider.GetUtcNow().AddHours(-Math.Max(_options.PendingUploadLifetimeHours, 1));
        var expiredUploads = await db.PendingImageUploads
            .Where(upload => upload.AttachedAt == null
                && upload.DeletedAt == null
                && upload.RequestedAt < cutoff)
            .OrderBy(upload => upload.RequestedAt)
            .Take(25)
            .ToListAsync(ct);

        var cleanedCount = 0;

        foreach (var upload in expiredUploads)
        {
            try
            {
                await DeleteStoredImageAsync(upload.PublicId, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                LogPendingImageCleanupFailed(logger, upload.PublicId, ex);
                continue;
            }

            upload.DeletedAt = timeProvider.GetUtcNow();
            cleanedCount++;
        }

        if (cleanedCount > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return cleanedCount;
    }

    private bool IsCloudinaryConfigured() =>
        !string.IsNullOrWhiteSpace(_options.CloudName)
        && !string.IsNullOrWhiteSpace(_options.ApiKey)
        && !string.IsNullOrWhiteSpace(_options.ApiSecret);

    private void EnsureCloudinaryConfigured()
    {
        if (!IsCloudinaryConfigured())
        {
            throw new InvalidOperationException("Cloudinary image uploads are not configured.");
        }
    }

    private Cloudinary CreateCloudinaryClient()
    {
        EnsureCloudinaryConfigured();
        return new Cloudinary(new Account(_options.CloudName, _options.ApiKey, _options.ApiSecret));
    }

    private static void ValidateFileMetadata(string fileName, string contentType, long fileSize)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("A file name is required.", nameof(fileName));
        }

        if (fileSize <= 0)
        {
            throw new ArgumentException("File size must be greater than zero.", nameof(fileSize));
        }

        if (fileSize > MaxFileSize)
        {
            throw new ArgumentException("File size must not exceed 5 MB.", nameof(fileSize));
        }

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new ArgumentException("Only JPEG, PNG, WebP, and GIF images are accepted.", nameof(contentType));
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Only JPEG, PNG, WebP, and GIF file extensions are accepted.", nameof(fileName));
        }
    }

    #pragma warning disable CA5350 // Cloudinary signed uploads require SHA1 for request signing.
    private string CreateSignature(string publicId, long timestamp)
    {
        var signatureParts = new List<string>
        {
            $"allowed_formats={AllowedFormatsCsv}",
            $"public_id={publicId}",
            $"timestamp={timestamp}"
        };

        signatureParts.Sort(StringComparer.Ordinal);
        var stringToSign = $"{string.Join("&", signatureParts)}{_options.ApiSecret}";
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(stringToSign));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    #pragma warning restore CA5350

    private static string ComputeDeleteTokenHash(string deleteToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(deleteToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Failed to delete expired pending image upload {PublicId}")]
    private static partial void LogPendingImageCleanupFailed(ILogger logger, string publicId, Exception exception);
}
