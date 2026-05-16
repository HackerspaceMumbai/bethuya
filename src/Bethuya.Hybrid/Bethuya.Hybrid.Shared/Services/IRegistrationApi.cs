using Refit;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Refit-generated typed HTTP client for the Bethuya Registrations API.</summary>
public interface IRegistrationApi
{
    [Get("/api/registrations/event/{eventId}")]
    Task<List<RegistrationDto>> GetByEventIdAsync(Guid eventId, CancellationToken ct = default);

    [Post("/api/registrations")]
    Task<RegistrationDto> CreateAsync([Body] CreateRegistrationDto request, CancellationToken ct = default);
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
    List<string> Interests);