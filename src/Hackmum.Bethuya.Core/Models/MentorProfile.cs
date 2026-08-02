using System.Text.Json;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Persisted opt-in record for a community member participating in the mentorship programme.
/// Scoped to a single community member and stores availability, expertise areas, and discovery consent.
/// </summary>
public sealed class MentorProfile
{
    /// <summary>Unique mentor profile identifier.</summary>
    public MentorProfileId Id { get; init; } = MentorProfileId.From(Guid.CreateVersion7());

    /// <summary>Community member who owns this mentor profile.</summary>
    public CommunityMemberId MemberId { get; init; }

    /// <summary>Current opt-in lifecycle status.</summary>
    public MentorshipStatus Status { get; set; } = MentorshipStatus.OptedIn;

    /// <summary>JSON-serialized list of expertise areas offered by this mentor.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ExpertiseAreasJson { get; set; } = "[]";

    /// <summary>Parsed expertise areas. Mutating this list does not auto-persist; call the service update methods.</summary>
    public IReadOnlyList<MentorExpertiseArea> ExpertiseAreas
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ExpertiseAreasJson))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<MentorExpertiseArea>>(ExpertiseAreasJson) ?? [];
            }
            catch (JsonException)
            {
                // Stored JSON is corrupt or empty string; degrade gracefully to an empty list.
                return [];
            }
        }
        set => ExpertiseAreasJson = JsonSerializer.Serialize(value);
    }

    /// <summary>Short introduction bio visible to potential mentees (max 500 chars).</summary>
    public string? IntroductionBio { get; set; }

    /// <summary>Self-declared availability in hours per month.</summary>
    public int AvailabilityHoursPerMonth { get; set; } = 2;

    /// <summary>Whether this mentor appears in the community discovery directory.</summary>
    public bool IsDiscoverable { get; set; } = true;

    /// <summary>Timestamp when the member first opted in.</summary>
    public DateTimeOffset OptedInAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Timestamp of the last status or preference update.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Navigation to the owning community member.</summary>
    public CommunityMember? Member { get; init; }
}
