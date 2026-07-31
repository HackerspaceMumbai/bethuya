using Refit;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>
/// Refit-generated typed client for the Community Passport API.
/// </summary>
public interface ICommunityPassportApi
{
    [Get("/api/community/passport")]
    Task<CommunityPassportDto> GetPassportAsync(CancellationToken ct = default);

    [Post("/api/community/passport/privacy")]
    Task<PassportPrivacyDto> SavePrivacyAsync([Body] UpdateCommunityPassportPrivacyDto request, CancellationToken ct = default);
}

public sealed record CommunityPassportDto(
    string DisplayName,
    string Email,
    string? OccupationStatus,
    string? CompanyName,
    string? EducationInstitute,
    string CurrentTier,
    PassportMetricsDto Metrics,
    PassportPrivacyDto Privacy,
    PassportResidencyDto Residency,
    IReadOnlyList<PassportIdentityDto> LinkedIdentities,
    IReadOnlyList<PassportTimelineEntryDto> Timeline);

public sealed record PassportMetricsDto(
    int EventsRegistered,
    int EventsAttended,
    int EventsWaitlisted,
    int VolunteerSignals,
    int MilestonesEarned);

public sealed record PassportPrivacyDto(
    string Visibility,
    bool ShareParticipationWithOrganizers,
    bool IsDiscoverableToCommunity);

public sealed record PassportResidencyDto(
    string Region,
    string Mode,
    string ComplianceProfile);

public sealed record PassportIdentityDto(
    string Provider,
    string Subject,
    string? Username,
    string? ProfileUrl,
    bool IsVerified,
    DateTimeOffset LinkedAt);

public sealed record PassportTimelineEntryDto(
    Guid EventId,
    string EventTitle,
    string Status,
    DateTimeOffset OccurredAt,
    string Evidence);

public sealed record UpdateCommunityPassportPrivacyDto(
    string Visibility,
    bool ShareParticipationWithOrganizers,
    bool IsDiscoverableToCommunity);
