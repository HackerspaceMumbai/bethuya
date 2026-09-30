namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// External connector sources that can emit participation history.
/// </summary>
public enum ParticipationConnectorKind
{
    Luma,
    Eventbrite,
    Meetup,
    GitHub,
    Forms,
    Discord,
    /// <summary>MLH / OrganizerHQ hackathon registration and attendance exports.</summary>
    MLH,
    /// <summary>Organizer-defined source with no dedicated platform (e.g. sponsor or community spreadsheets).</summary>
    Custom
}
