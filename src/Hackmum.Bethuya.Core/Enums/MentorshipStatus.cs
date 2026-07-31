namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// Mentor programme opt-in lifecycle state for a community member.
/// </summary>
public enum MentorshipStatus
{
    /// <summary>Member is actively accepting mentee connections.</summary>
    OptedIn = 1,
    /// <summary>Member is temporarily unavailable but stays discoverable in the directory.</summary>
    Paused = 2,
    /// <summary>Member has withdrawn from the programme and is not discoverable.</summary>
    OptedOut = 3
}
