namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Provider-agnostic storage for original import file bytes (and, if needed, other import
/// artifacts). Decoupled from Cloudinary (image-only). Implementations may back onto Azure
/// Blob Storage, an S3-compatible store, or local disk for local development.
/// </summary>
public interface IImportArtifactStore
{
    /// <summary>Persists <paramref name="content"/> and returns an opaque storage key for later retrieval.</summary>
    Task<string> SaveAsync(byte[] content, string fileName, CancellationToken ct = default);

    Task<byte[]> ReadAsync(string storageKey, CancellationToken ct = default);
}
