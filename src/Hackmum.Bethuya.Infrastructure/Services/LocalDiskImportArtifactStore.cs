using Hackmum.Bethuya.Core.Services;

namespace Hackmum.Bethuya.Infrastructure.Services;

/// <summary>
/// Local-disk implementation of <see cref="IImportArtifactStore"/>, suitable for local
/// development. Production deployments can swap in an Azure Blob Storage or S3-compatible
/// implementation behind the same interface.
/// </summary>
public sealed class LocalDiskImportArtifactStore : IImportArtifactStore
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv",
        ".xlsx"
    };

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
        if (!AllowedExtensions.Contains(safeExtension))
        {
            throw new InvalidOperationException("Import artifacts must use a CSV or XLSX extension.");
        }

        var storageKey = $"{Guid.CreateVersion7():N}{safeExtension}";
        var path = GetArtifactPath(storageKey);

        await File.WriteAllBytesAsync(path, content, ct);
        return storageKey;
    }

    public async Task<byte[]> ReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = GetArtifactPath(storageKey);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Import artifact '{storageKey}' was not found.", path);
        }

        return await File.ReadAllBytesAsync(path, ct);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = GetArtifactPath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetArtifactPath(string storageKey)
    {
        if (Path.IsPathFullyQualified(storageKey) ||
            storageKey.Contains(Path.DirectorySeparatorChar) ||
            storageKey.Contains(Path.AltDirectorySeparatorChar) ||
            !string.Equals(storageKey, Path.GetFileName(storageKey), StringComparison.Ordinal) ||
            !AllowedExtensions.Contains(Path.GetExtension(storageKey)))
        {
            throw new InvalidOperationException("The import artifact key is invalid.");
        }

        return Path.Combine(_rootDirectory, storageKey);
    }
}
