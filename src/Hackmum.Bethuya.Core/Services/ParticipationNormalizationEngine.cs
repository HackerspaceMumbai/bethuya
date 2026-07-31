using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Validates and normalizes canonical participation records before persistence.
/// </summary>
public static class ParticipationNormalizationEngine
{
    public static NormalizedParticipationEntry Normalize(NormalizedParticipationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.OccurredAt == default)
        {
            throw new ArgumentException("Occurrence timestamp is required.", nameof(entry));
        }

        return entry with
        {
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
