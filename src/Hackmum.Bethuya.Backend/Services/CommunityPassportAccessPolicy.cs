using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Centralizes Community Passport visibility and organizer redaction decisions.
/// </summary>
public sealed class CommunityPassportAccessPolicy
{
    /// <summary>Determines whether an organizer may open a member Passport.</summary>
    public bool CanOrganizerView(CommunityMember member)
        => member.Visibility != ProfileVisibilityScope.Private;

    /// <summary>Determines whether participation details must be hidden from the current viewer.</summary>
    public bool ShouldRedactParticipation(CommunityMember member, bool isOwner)
        => !isOwner && !member.ShareParticipationWithOrganizers;
}
