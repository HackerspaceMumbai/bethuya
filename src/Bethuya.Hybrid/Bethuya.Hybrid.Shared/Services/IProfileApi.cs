using System.ComponentModel.DataAnnotations;
using Refit;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Refit-generated typed HTTP client for the Bethuya Profile API.</summary>
public interface IProfileApi
{
    [Get("/api/profile/completion-status")]
    Task<ProfileCompletionStatusDto> GetCompletionStatusAsync(CancellationToken ct = default);

    [Post("/api/profile")]
    Task<ProfileCompletionStatusDto> SaveMandatoryProfileAsync([Body] SaveMandatoryProfileDto request, CancellationToken ct = default);

    [Post("/api/profile/aide")]
    Task<ProfileCompletionStatusDto> SaveAideProfileAsync([Body] SaveAideProfileDto request, CancellationToken ct = default);
}

/// <summary>Profile completion status returned by the API.</summary>
public sealed record ProfileCompletionStatusDto(
    bool IsProfileComplete,
    bool IsAideProfileComplete,
    DateTimeOffset? ProfileCompletedAt,
    DateTimeOffset? AideProfileCompletedAt);

/// <summary>Payload for saving mandatory profile fields.</summary>
public sealed record SaveMandatoryProfileDto(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress, MaxLength(200)] string Email,
    [MaxLength(30)] string? MobileNumber,
    [MaxLength(200)] string? OccupationStatus,
    [MaxLength(100)] string? City,
    [MaxLength(100)] string? State,
    [MaxLength(20)] string? PostalCode,
    [MaxLength(100)] string? Country);

/// <summary>Payload for saving optional AIDE profile fields.</summary>
public sealed record SaveAideProfileDto(
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
