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
            ExternalMemberKey = NormalizeRequired(entry.ExternalMemberKey, 200, "External member key"),
            Evidence = NormalizeRequired(entry.Evidence, 600, "Evidence"),
            ProvenanceKey = NormalizeRequired(entry.ProvenanceKey, 300, "Provenance key"),
            ExternalEventId = NormalizeOptional(entry.ExternalEventId, 200),
            ExternalRecordId = NormalizeOptional(entry.ExternalRecordId, 200),
            SourceCorrelationId = NormalizeOptional(entry.SourceCorrelationId, 200)
        };
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} must be {maxLength} characters or fewer.", fieldName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value must be {maxLength} characters or fewer.", nameof(value));
        }

        return normalized;
    }
}
