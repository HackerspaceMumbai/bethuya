namespace Hackmum.Bethuya.Infrastructure.Services;

/// <summary>Configuration for the Cloudinary image upload provider.</summary>
public sealed class CloudinaryOptions
{
    /// <summary>The configuration section name in appsettings.</summary>
    public const string SectionName = "Cloudinary";

    /// <summary>Cloudinary cloud name.</summary>
    public required string CloudName { get; set; }

    /// <summary>Cloudinary API key.</summary>
    public required string ApiKey { get; set; }

    /// <summary>Cloudinary API secret.</summary>
    public required string ApiSecret { get; set; }
}
