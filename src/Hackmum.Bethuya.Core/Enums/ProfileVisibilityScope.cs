namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// Visibility levels for a member's passport profile.
/// </summary>
public enum ProfileVisibilityScope
{
    /// <summary>
    /// Visible to everyone.
    /// </summary>
    Public,
    /// <summary>
    /// Visible only to authenticated community members.
    /// </summary>
    CommunityOnly,
    /// <summary>
    /// Visible only to organizers.
    /// </summary>
    OrganizerOnly,
    /// <summary>
    /// Visible only to the owning member.
    /// </summary>
    Private
}
