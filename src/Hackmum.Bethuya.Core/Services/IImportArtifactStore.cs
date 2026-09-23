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

    /// <summary>
    /// Removes a previously saved artifact. Used to clean up orphaned files when the
    /// surrounding database write (e.g. persisting the <see cref="ImportBatch"/> record)
    /// fails after the artifact bytes were already saved. Safe to call even if the key
    /// does not exist.
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
