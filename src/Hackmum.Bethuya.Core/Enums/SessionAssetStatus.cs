namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// Post-event asset collection status for a scheduled session.
/// </summary>
public enum SessionAssetStatus
{
    AwaitingEvent,
    PendingUpload,
    UploadedToVault,
    NoAssetsRequired
}
