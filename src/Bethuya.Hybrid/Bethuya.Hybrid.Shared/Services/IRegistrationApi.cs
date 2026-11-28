using Refit;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Refit-generated typed HTTP client for the Bethuya Registrations API.</summary>
public interface IRegistrationApi
{
    [Get("/api/registrations/event/{eventId}")]
    Task<List<RegistrationDto>> GetByEventIdAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Creates a registration. For a signed-in caller the response carries
    /// <see cref="GovernmentIdUploadTokenHeader"/>, which <see cref="UploadGovernmentIdAsync"/> sends back.
    /// </summary>
    [Post("/api/registrations")]
    Task<ApiResponse<RegistrationDto>> CreateAsync([Body] CreateRegistrationDto request, CancellationToken ct = default);

    [Multipart]
    [Post("/api/registrations/{id}/government-id")]
    Task UploadGovernmentIdAsync(
        Guid id,
        StreamPart file,
        [Header(GovernmentIdUploadTokenHeader)] string? uploadToken,
        CancellationToken ct = default);

    /// <summary>Header that binds a government ID upload to the user who created the registration.</summary>
    const string GovernmentIdUploadTokenHeader = "X-Bethuya-Government-Id-Upload-Token";
}

/// <summary>Registration data returned from the API.</summary>
public sealed record RegistrationDto(
    Guid Id,
    Guid EventId,
    string FullName,
    string Email,
    string? Bio,
    List<string> Interests,
    string Status,
    DateTimeOffset RegisteredAt,
    DateTimeOffset UpdatedAt);

/// <summary>Payload sent to create a new registration.</summary>
public sealed record CreateRegistrationDto(
    Guid EventId,
    string FullName,
    string Email,
    string? Bio,
    List<string> Interests,
    string? Intent,
    string? Goals,
    List<string> ContributionPreferences,
    string? ExperienceLevel,
    string? DietaryRequirements,
    string? AccessibilityNeeds);