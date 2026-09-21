using Hackmum.Bethuya.Core.Services;

namespace Hackmum.Bethuya.Infrastructure.Services;

/// <summary>
/// Local-disk implementation of <see cref="IImportArtifactStore"/>, suitable for local
/// development. Production deployments can swap in an Azure Blob Storage or S3-compatible
/// implementation behind the same interface.
/// </summary>
public sealed class LocalDiskImportArtifactStore : IImportArtifactStore
{
    private readonly string _rootDirectory;

    public LocalDiskImportArtifactStore(string? rootDirectory = null)
    {
        _rootDirectory = rootDirectory ?? Path.Combine(Path.GetTempPath(), "bethuya-import-artifacts");
        Directory.CreateDirectory(_rootDirectory);
    }

    public async Task<string> SaveAsync(byte[] content, string fileName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var safeExtension = Path.GetExtension(fileName);
        var storageKey = $"{Guid.CreateVersion7():N}{safeExtension}";
        var path = Path.Combine(_rootDirectory, storageKey);

        await File.WriteAllBytesAsync(path, content, ct);
        return storageKey;
    }

    public async Task<byte[]> ReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = Path.Combine(_rootDirectory, storageKey);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Import artifact '{storageKey}' was not found.", path);
        }

        return await File.ReadAllBytesAsync(path, ct);
    }
}
