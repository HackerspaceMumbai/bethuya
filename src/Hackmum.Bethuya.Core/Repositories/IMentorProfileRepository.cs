using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Repositories;

/// <summary>
/// Persistence contract for community member mentor profiles.
/// </summary>
public interface IMentorProfileRepository
{
    /// <summary>
    /// Returns the mentor profile for the given community member, or <see langword="null"/> if none exists.
    /// </summary>
    Task<MentorProfile?> GetByMemberIdAsync(CommunityMemberId memberId, CancellationToken ct = default);

    /// <summary>
    /// Returns all mentor profiles that are currently discoverable, with optional expertise-area filtering.
    /// Only members whose <see cref="MentorProfile.IsDiscoverable"/> is <see langword="true"/> and whose
    /// owning <see cref="CommunityMember.IsDiscoverableToCommunity"/> is <see langword="true"/> are included.
    /// Only profiles with status <see cref="MentorshipStatus.OptedIn"/> are returned.
    /// </summary>
    Task<List<MentorProfile>> GetDiscoverableMentorsAsync(
        IReadOnlyList<MentorExpertiseArea>? filterAreas,
        int limit,
        CancellationToken ct = default);

    /// <summary>
    /// Inserts or updates a mentor profile.  If an entry already exists for the member, it is updated;
    /// otherwise a new profile is created.
    /// </summary>
    Task<MentorProfile> UpsertAsync(MentorProfile profile, CancellationToken ct = default);
}
