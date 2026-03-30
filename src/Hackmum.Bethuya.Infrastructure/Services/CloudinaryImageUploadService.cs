using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Hackmum.Bethuya.Core.Services;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Infrastructure.Services;

/// <summary>Uploads images to Cloudinary and returns the secure URL.</summary>
public sealed class CloudinaryImageUploadService(IOptions<CloudinaryOptions> options) : IImageUploadService
{
    private readonly Cloudinary _cloudinary = new(new Account(
        options.Value.CloudName,
        options.Value.ApiKey,
        options.Value.ApiSecret));

    /// <inheritdoc />
    public async Task<string> UploadAsync(Stream imageStream, string fileName, CancellationToken ct = default)
    {
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, imageStream),
            Folder = "bethuya/events"
        };

        var result = await _cloudinary.UploadAsync(uploadParams, ct);

        if (result.Error is not null)
        {
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
        }

        return result.SecureUrl.ToString();
    }
}
