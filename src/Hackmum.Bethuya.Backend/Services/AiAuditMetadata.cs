using System.Security.Cryptography;
using System.Text.Json;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Shared normalization and hashing helpers for AI invocation audit persistence.
/// </summary>
internal static class AiAuditMetadata
{
    public const int MaxPersistedTraceMetadataLength = 200;
    public const int MaxPersistedProviderMetadataLength = 200;
    public const string MissingTraceParentPrefix = "missing-traceparent:";

    public static string ComputeInputHash<TInput>(TInput input)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(input);
        var hash = SHA256.HashData(payload);
        return Convert.ToHexString(hash);
    }

    public static string BuildAuditTraceParent(string? traceParent, string correlationId)
    {
        if (!string.IsNullOrWhiteSpace(traceParent))
        {
            return NormalizeRequiredTraceMetadata(traceParent);
        }

        var availableCorrelationLength = Math.Max(0, MaxPersistedTraceMetadataLength - MissingTraceParentPrefix.Length);
        var boundedCorrelationId = correlationId.Length <= availableCorrelationLength
            ? correlationId
            : correlationId[..availableCorrelationLength];

        return MissingTraceParentPrefix + boundedCorrelationId;
    }

    public static string NormalizeRequiredTraceMetadata(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Length <= MaxPersistedTraceMetadataLength
            ? value
            : value[..MaxPersistedTraceMetadataLength];
    }

    public static string? NormalizeOptionalTraceMetadata(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeRequiredTraceMetadata(value);
    }

    public static string? NormalizeOptionalPersistedProviderMetadata(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= MaxPersistedProviderMetadataLength
            ? value
            : value[..MaxPersistedProviderMetadataLength];
    }

    public static string NormalizeRequiredPersistedProviderMetadata(string? value, string fallback)
    {
        return NormalizeOptionalPersistedProviderMetadata(value)
            ?? NormalizeOptionalPersistedProviderMetadata(fallback)
            ?? throw new InvalidOperationException("Required persisted provider metadata could not be normalized.");
    }
}
