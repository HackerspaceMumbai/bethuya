namespace Hackmum.Bethuya.Core.Models;

/// <summary>Tracks temporary direct-to-Cloudinary uploads until they are attached to a saved event.</summary>
public sealed class PendingImageUpload
{
    public required string PublicId { get; init; }
    public required string DeleteTokenHash { get; set; }
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AttachedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
