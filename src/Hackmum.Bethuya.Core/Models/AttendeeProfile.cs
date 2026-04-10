namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Stores profile information for a registered attendee.
/// Mandatory fields are required before accessing the platform.
/// AIDE (Accessibility, Inclusivity, Diversity, Equity) fields are optional
/// and only collected with explicit consent.
/// </summary>
public sealed class AttendeeProfile
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>The authentication provider subject identifier (JWT 'sub' claim).</summary>
    public required string UserId { get; init; }

    // ── Mandatory profile fields ──────────────────────────────────────────────

    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public string? MobileNumber { get; set; }
    public string? OccupationStatus { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    // ── Completion tracking ───────────────────────────────────────────────────

    public bool IsProfileComplete { get; set; }
    public DateTimeOffset? ProfileCompletedAt { get; set; }
    public bool IsAideProfileComplete { get; set; }
    public DateTimeOffset? AideProfileCompletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // ── AIDE fields (optional, consent-only) ─────────────────────────────────

    public string? GenderIdentity { get; set; }
    public string? SelfDescribeGender { get; set; }
    public string? AgeRange { get; set; }
    public string? Ethnicity { get; set; }
    public string? SelfDescribeEthnicity { get; set; }
    public string? Disability { get; set; }
    public string? DisabilityDetails { get; set; }
    public string? DietaryRequirements { get; set; }
    public string? LgbtqIdentity { get; set; }
    public string? ParentalStatus { get; set; }
    public string? Religion { get; set; }
    public string? Caste { get; set; }
    public string? Neighborhood { get; set; }
    public string? ModeOfTransportation { get; set; }
    public string? SocioeconomicBackground { get; set; }
    public string? Neurodiversity { get; set; }
    public string? CaregivingResponsibilities { get; set; }
    public string? LanguageProficiency { get; set; }
    public string? EducationalBackground { get; set; }
    public string? HowDidYouHear { get; set; }
    public string? AdditionalSupport { get; set; }
}
