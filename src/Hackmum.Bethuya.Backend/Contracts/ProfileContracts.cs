using System.ComponentModel.DataAnnotations;

namespace Hackmum.Bethuya.Backend.Contracts;

/// <summary>Profile completion status for the current user.</summary>
public sealed record ProfileCompletionStatusResponse(
    bool IsProfileComplete,
    bool IsSocialConnectionsComplete,
    bool IsAideProfileComplete,
    DateTimeOffset? ProfileCompletedAt,
    DateTimeOffset? AideProfileCompletedAt);

/// <summary>Request payload for saving mandatory profile fields.</summary>
public sealed record SaveMandatoryProfileRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress, MaxLength(200)] string Email,
    [MaxLength(30)] string? MobileNumber,
    [Required, MaxLength(100)] string GovernmentPhotoIdType,
    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "Government ID last four digits must be exactly 4 digits.")] string GovernmentIdLastFour,
    [Required, MaxLength(200)] string? OccupationStatus,
    [MaxLength(200)] string? CompanyName,
    [MaxLength(200)] string? EducationInstitute,
    [MaxLength(100)] string? City,
    [MaxLength(100)] string? State,
    [MaxLength(20)] string? PostalCode,
    [MaxLength(100)] string? Country) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(OccupationStatus, "Employee", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(CompanyName))
        {
            yield return new ValidationResult(
                "Company name is required when employment status is Employee.",
                [nameof(CompanyName)]);
        }

        if (string.Equals(OccupationStatus, "Student", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(EducationInstitute))
        {
            yield return new ValidationResult(
                "Education institute is required when employment status is Student.",
                [nameof(EducationInstitute)]);
        }
    }
}

/// <summary>Current verified social profile details for the signed-in user.</summary>
public sealed record SocialProfileResponse(
    string? OccupationStatus,
    bool IsLinkedInRequired,
    bool IsGitHubRequired,
    string? LinkedInMemberId,
    string? LinkedInProfileUrl,
    string? GitHubLogin,
    string? GitHubProfileUrl);

/// <summary>Request payload for saving verified social profile fields.</summary>
public sealed record SaveSocialProfileRequest(
    [MaxLength(100)] string? LinkedInMemberId,
    [Url, MaxLength(500)] string? LinkedInProfileUrl,
    [MaxLength(100)] string? GitHubLogin,
    [Url, MaxLength(500)] string? GitHubProfileUrl);

/// <summary>Request payload for saving optional AIDE profile fields.</summary>
public sealed record SaveAideProfileRequest(
    [MaxLength(100)] string? GenderIdentity,
    [MaxLength(200)] string? SelfDescribeGender,
    [MaxLength(50)] string? AgeRange,
    [MaxLength(100)] string? Ethnicity,
    [MaxLength(200)] string? SelfDescribeEthnicity,
    [MaxLength(100)] string? Disability,
    [MaxLength(1000)] string? DisabilityDetails,
    [MaxLength(500)] string? DietaryRequirements,
    [MaxLength(100)] string? LgbtqIdentity,
    [MaxLength(100)] string? ParentalStatus,
    [MaxLength(100)] string? Religion,
    [MaxLength(100)] string? Caste,
    [MaxLength(200)] string? Neighborhood,
    [MaxLength(100)] string? ModeOfTransportation,
    [MaxLength(200)] string? SocioeconomicBackground,
    [MaxLength(200)] string? Neurodiversity,
    [MaxLength(200)] string? CaregivingResponsibilities,
    [MaxLength(500)] string? LanguageProficiency,
    [MaxLength(200)] string? EducationalBackground,
    [MaxLength(200)] string? HowDidYouHear,
    [MaxLength(1000)] string? AdditionalSupport);
