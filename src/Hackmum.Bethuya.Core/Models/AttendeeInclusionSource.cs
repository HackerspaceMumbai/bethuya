namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Minimal, privacy-safe subset used to derive inclusion signals.
/// </summary>
public sealed record AttendeeInclusionSource(
    string? Neighborhood,
    string? LanguageProficiency,
    string? EducationalBackground,
    string? SocioeconomicBackground);
