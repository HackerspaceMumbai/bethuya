namespace Hackmum.Bethuya.Core.Enums;

/// <summary>Tracks the explicitly requested archive publication lifecycle.</summary>
public enum EventArchivePublicationStatus
{
    None,
    Pending,
    DraftPrPending,
    Published,
    Failed,
    Closed
}
