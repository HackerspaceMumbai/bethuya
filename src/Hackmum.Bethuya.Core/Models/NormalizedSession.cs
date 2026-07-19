using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Canonical speaker representation produced by session ingestion adapters.
/// </summary>
public sealed record NormalizedSpeaker(
    string Name,
    string? GitHubHandle = null,
    string? TwitterHandle = null,
    string? AvatarUrl = null);

/// <summary>
/// Canonical session representation consumed by Bethuya scheduling workflows.
/// </summary>
public sealed record NormalizedSession(
    string Title,
    string? Description,
    IReadOnlyCollection<NormalizedSpeaker> Speakers,
    SessionSource Source,
    string? SourceSessionId,
    DateTimeOffset? PreferredStartTime,
    TimeSpan? Duration);
