using Refit;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>
/// Refit-generated typed client for the Community Passport API.
/// </summary>
public interface ICommunityPassportApi
{
    /// <summary>
    /// Gets the current user's Community Passport projection.
    /// </summary>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>Current Community Passport DTO.</returns>
    [Get("/api/community/passport")]
    Task<CommunityPassportDto> GetPassportAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves updated privacy controls for the current user's Community Passport.
    /// </summary>
    /// <param name="request">Updated privacy payload.</param>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>Persisted privacy view.</returns>
    [Post("/api/community/passport/privacy")]
    Task<PassportPrivacyDto> SavePrivacyAsync([Body] UpdateCommunityPassportPrivacyDto request, CancellationToken ct = default);
}

/// <summary>
/// Member-facing Community Passport projection.
/// </summary>
/// <param name="DisplayName">Display name shown in the passport.</param>
/// <param name="Email">Primary member email.</param>
/// <param name="OccupationStatus">Optional occupation status.</param>
/// <param name="CompanyName">Optional company affiliation.</param>
/// <param name="EducationInstitute">Optional education affiliation.</param>
/// <param name="CurrentTier">Computed community tier label.</param>
/// <param name="Metrics">Aggregated participation metrics.</param>
/// <param name="Privacy">Current privacy controls.</param>
/// <param name="Residency">Applied residency policy.</param>
/// <param name="LinkedIdentities">Linked external identities.</param>
/// <param name="Timeline">Recent activity timeline.</param>
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

/// <summary>
/// Summary participation metrics.
/// </summary>
/// <param name="EventsRegistered">Total registrations.</param>
/// <param name="EventsAttended">Total attended events.</param>
/// <param name="EventsWaitlisted">Total waitlisted events.</param>
/// <param name="VolunteerSignals">Total volunteer intent signals.</param>
/// <param name="MilestonesEarned">Total milestones earned.</param>
public sealed record PassportMetricsDto(
    int EventsRegistered,
    int EventsAttended,
    int EventsWaitlisted,
    int VolunteerSignals,
    int MilestonesEarned);

/// <summary>
/// Privacy controls for the current member.
/// </summary>
/// <param name="Visibility">Visibility scope value.</param>
/// <param name="ShareParticipationWithOrganizers">Whether organizers can use participation signals.</param>
/// <param name="IsDiscoverableToCommunity">Whether discoverable in community opportunity flows.</param>
public sealed record PassportPrivacyDto(
    string Visibility,
    bool ShareParticipationWithOrganizers,
    bool IsDiscoverableToCommunity);

/// <summary>
/// Residency and compliance policy view.
/// </summary>
/// <param name="Region">Residency region label.</param>
/// <param name="Mode">Residency mode value.</param>
/// <param name="ComplianceProfile">Compliance profile label.</param>
public sealed record PassportResidencyDto(
    string Region,
    string Mode,
    string ComplianceProfile);

/// <summary>
/// Linked identity descriptor.
/// </summary>
/// <param name="Provider">Identity provider name.</param>
/// <param name="Subject">Provider subject value.</param>
/// <param name="Username">Optional provider username.</param>
/// <param name="ProfileUrl">Optional provider profile URL.</param>
/// <param name="IsVerified">Whether this identity is verified.</param>
/// <param name="LinkedAt">Timestamp when identity was linked.</param>
public sealed record PassportIdentityDto(
    string Provider,
    string Subject,
    string? Username,
    string? ProfileUrl,
    bool IsVerified,
    DateTimeOffset LinkedAt);

/// <summary>
/// Activity timeline entry for the passport.
/// </summary>
/// <param name="EventId">Event identifier.</param>
/// <param name="EventTitle">Event title.</param>
/// <param name="Status">Mapped participation status.</param>
/// <param name="OccurredAt">When this timeline event occurred.</param>
/// <param name="Evidence">Human-readable supporting evidence.</param>
public sealed record PassportTimelineEntryDto(
    Guid EventId,
    string EventTitle,
    string Status,
    DateTimeOffset OccurredAt,
    string Evidence);

/// <summary>
/// Privacy update payload for the Community Passport.
/// </summary>
/// <param name="Visibility">Requested visibility scope value.</param>
/// <param name="ShareParticipationWithOrganizers">Whether organizers may use participation signals.</param>
/// <param name="IsDiscoverableToCommunity">Whether discoverable in community opportunity flows.</param>
public sealed record UpdateCommunityPassportPrivacyDto(
    string Visibility,
    bool ShareParticipationWithOrganizers,
    bool IsDiscoverableToCommunity);
