namespace Hackmum.Bethuya.Infrastructure.Services;

/// <summary>Configuration for the Cloudinary image upload provider.</summary>
public sealed class CloudinaryOptions
{
    /// <summary>The configuration section name in appsettings.</summary>
    public const string SectionName = "Cloudinary";

    /// <summary>Stable root folder for Bethuya event cover images.</summary>
    public const string DefaultEventCoverRootFolder = "bethuya/events";

    /// <summary>Cloudinary cloud name.</summary>
    public string CloudName { get; set; } = string.Empty;

    /// <summary>Cloudinary API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Cloudinary API secret.</summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Optional Cloudinary upload preset used for direct browser uploads.</summary>
    public string? UploadPreset { get; set; }

    /// <summary>Top-level Cloudinary folder for event cover images.</summary>
    public string EventCoverRootFolder { get; set; } = DefaultEventCoverRootFolder;

    /// <summary>Folder used for uploads that have not yet been attached to a saved event.</summary>
    public string PendingUploadFolder { get; set; } = $"{DefaultEventCoverRootFolder}/pending";

    /// <summary>How long an unattached direct upload can remain before cleanup deletes it.</summary>
    public int PendingUploadLifetimeHours { get; set; } = 24;

    /// <summary>How frequently the backend checks for expired pending uploads.</summary>
    public int PendingUploadCleanupIntervalMinutes { get; set; } = 60;
}
