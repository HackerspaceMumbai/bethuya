using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Validates and normalizes canonical participation records before persistence.
/// </summary>
public static class ParticipationNormalizationEngine
{
    /// <summary>
    /// Validates and normalises a canonical participation record before persistence.
    /// </summary>
    /// <param name="entry">The raw entry emitted by a connector adapter.</param>
    /// <returns>
    /// A new <see cref="NormalizedParticipationEntry"/> with all string fields trimmed,
    /// whitespace-only values replaced with <see langword="null"/>, and lengths validated
    /// against column limits.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="NormalizedParticipationEntry.OccurredAt"/> is the default value,
    /// a required field is blank, or a field exceeds its maximum length.
    /// </exception>
    public static NormalizedParticipationEntry Normalize(NormalizedParticipationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.OccurredAt == default)
        {
            throw new ArgumentException("Occurrence timestamp is required.", nameof(entry));
        }

        if (!Enum.IsDefined(entry.Activity) || !Enum.IsDefined(entry.Connector))
            throw new ArgumentException("A valid activity and connector are required.", nameof(entry));

        var targetKind = NormalizeOptional(entry.TargetKind, 30, nameof(entry.TargetKind), "Target kind");
        var targetKey = NormalizeOptional(entry.TargetKey, 200, nameof(entry.TargetKey), "Target key");
        var targetLabel = NormalizeOptional(entry.TargetLabel, 200, nameof(entry.TargetLabel), "Target label");
        if ((targetKind is not null || targetKey is not null || targetLabel is not null)
            && (targetKind is null || targetKey is null || targetLabel is null))
            throw new ArgumentException("Target kind, key and label must be provided together.", nameof(entry));
        if (targetKind is not null && targetKind is not ("Member" or "Event" or "Project" or "Community" or "Chapter" or "Technology"))
            throw new ArgumentException("Unsupported participation target kind.", nameof(entry));
        var expectedKind = entry.Activity switch
        {
            Enums.ParticipationActivityKind.ContributedProject => "Project",
            Enums.ParticipationActivityKind.JoinedChapter => "Chapter",
            Enums.ParticipationActivityKind.Mentored => "Member",
            Enums.ParticipationActivityKind.UsedTechnology => "Technology",
            _ => null
        };
        if (expectedKind is not null && targetKind != expectedKind)
            throw new ArgumentException($"{entry.Activity} requires a {expectedKind} target.", nameof(entry));
        if (entry.Activity is Enums.ParticipationActivityKind.Attended or Enums.ParticipationActivityKind.Spoke or Enums.ParticipationActivityKind.Volunteered
            && targetKind is not null && targetKind != "Event")
            throw new ArgumentException("Event participation requires an Event target.", nameof(entry));
        if (entry.Activity == Enums.ParticipationActivityKind.JoinedCommunity && targetKind is not null && targetKind != "Community")
            throw new ArgumentException("Community membership requires a Community target.", nameof(entry));
        if (entry.Activity == Enums.ParticipationActivityKind.Spoke && entry.EventId is null && targetKind != "Event")
            throw new ArgumentException("Speaking participation requires an event identifier or Event target.", nameof(entry));
        if (targetKind == "Member" && (!Guid.TryParse(targetKey, out var memberKey) || memberKey == Guid.Empty))
            throw new ArgumentException("Member targets must use a canonical community member identifier.", nameof(entry));

        return entry with
        {
            TargetKind = targetKind,
            TargetKey = targetKey,
            TargetLabel = targetLabel,
            ExternalMemberKey = NormalizeRequired(
                entry.ExternalMemberKey,
                200,
                nameof(entry.ExternalMemberKey),
                "External member key"),
            Evidence = NormalizeRequired(
                entry.Evidence,
                600,
                nameof(entry.Evidence),
                "Evidence"),
            ProvenanceKey = NormalizeRequired(
                entry.ProvenanceKey,
                300,
                nameof(entry.ProvenanceKey),
                "Provenance key"),
            ExternalEventId = NormalizeOptional(entry.ExternalEventId, 200, nameof(entry.ExternalEventId), "External event id"),
            ExternalRecordId = NormalizeOptional(entry.ExternalRecordId, 200, nameof(entry.ExternalRecordId), "External record id"),
            SourceCorrelationId = NormalizeOptional(entry.SourceCorrelationId, 200, nameof(entry.SourceCorrelationId), "Source correlation id")
        };
    }

    private static string NormalizeRequired(string value, int maxLength, string paramName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} is required.", paramName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{displayName} must be {maxLength} characters or fewer.", paramName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string paramName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{displayName} must be {maxLength} characters or fewer.", paramName);
        }

        return normalized;
    }
}
