namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Non-sensitive attendee summary fields safe for organizer-facing curation views.
/// </summary>
public sealed record AttendeePublicSummary(
    string? OccupationStatus,
    string? CompanyName,
    string? EducationInstitute);
