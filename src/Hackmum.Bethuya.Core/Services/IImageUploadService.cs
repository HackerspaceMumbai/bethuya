namespace Hackmum.Bethuya.Core.Services;

/// <summary>Uploads images to a cloud storage provider and returns the public URL.</summary>
public interface IImageUploadService
{
    /// <summary>Uploads an image stream and returns its publicly accessible URL.</summary>
    /// <param name="imageStream">The image file stream.</param>
    /// <param name="fileName">Original file name (used for format detection).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The public URL of the uploaded image.</returns>
    Task<string> UploadAsync(Stream imageStream, string fileName, CancellationToken ct = default);
}
