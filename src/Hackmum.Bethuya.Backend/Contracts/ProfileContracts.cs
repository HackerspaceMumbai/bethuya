using System.ComponentModel.DataAnnotations;

namespace Hackmum.Bethuya.Backend.Contracts;

/// <summary>Profile completion status for the current user.</summary>
public sealed record ProfileCompletionStatusResponse(
    bool IsProfileComplete,
    bool IsAideProfileComplete,
    DateTimeOffset? ProfileCompletedAt,
    DateTimeOffset? AideProfileCompletedAt);

/// <summary>Request payload for saving mandatory profile fields.</summary>
public sealed record SaveMandatoryProfileRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress, MaxLength(200)] string Email,
    [MaxLength(30)] string? MobileNumber,
    [MaxLength(200)] string? OccupationStatus,
    [MaxLength(100)] string? City,
    [MaxLength(100)] string? State,
    [MaxLength(20)] string? PostalCode,
    [MaxLength(100)] string? Country);

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
