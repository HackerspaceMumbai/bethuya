namespace Hackmum.Bethuya.Core.Services;

/// <summary>Coordinates direct-to-cloud image uploads and lifecycle management.</summary>
public interface IImageUploadService
{
    /// <summary>Creates a signed direct-upload session for an image selected in the browser.</summary>
    Task<DirectImageUploadSession> CreateDirectUploadSessionAsync(
        string fileName,
        string contentType,
        long fileSize,
        CancellationToken ct = default);

    /// <summary>Deletes a pending upload when the caller proves possession of its delete token.</summary>
    Task<bool> DeletePendingUploadAsync(string publicId, string deleteToken, CancellationToken ct = default);

    /// <summary>Marks a pending upload as attached to a saved event so cleanup will ignore it.</summary>
    Task MarkUploadAttachedAsync(string publicId, CancellationToken ct = default);

    /// <summary>Checks whether a Cloudinary public ID belongs to a pending upload created by this app.</summary>
    Task<bool> IsPendingUploadAsync(string publicId, CancellationToken ct = default);

    /// <summary>Checks whether the uploaded asset currently exists in Cloudinary.</summary>
    Task<bool> UploadedAssetExistsAsync(string publicId, CancellationToken ct = default);

    /// <summary>Deletes a stored image by Cloudinary public ID.</summary>
    Task DeleteStoredImageAsync(string publicId, CancellationToken ct = default);

    /// <summary>Tries to parse the Cloudinary public ID from a secure delivery URL.</summary>
    bool TryGetPublicId(string? imageUrl, out string publicId);

    /// <summary>Deletes expired pending uploads that were never attached to an event.</summary>
    Task<int> CleanupExpiredPendingUploadsAsync(CancellationToken ct = default);
}

/// <summary>Raised when image uploads cannot be used because the configured provider is unavailable.</summary>
public sealed class ImageUploadProviderUnavailableException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="ImageUploadProviderUnavailableException"/> class.</summary>
    public ImageUploadProviderUnavailableException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ImageUploadProviderUnavailableException"/> class.</summary>
    public ImageUploadProviderUnavailableException(string message) : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ImageUploadProviderUnavailableException"/> class.</summary>
    public ImageUploadProviderUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>Signed parameters the browser needs to upload directly to Cloudinary.</summary>
public sealed record DirectImageUploadSession(
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
