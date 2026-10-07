using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Centralizes Community Passport visibility and organizer redaction decisions.
/// </summary>
public sealed class CommunityPassportAccessPolicy
{
    public bool CanOrganizerView(CommunityMember member)
        => member.Visibility != ProfileVisibilityScope.Private;

    public bool ShouldRedactParticipation(CommunityMember member, bool isOwner)
        => !isOwner && !member.ShareParticipationWithOrganizers;
}
