using System.Security.Cryptography;
using System.Text;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Services;

/// <summary>Computes a stable identity for the column mappings used by an import Dry Run.</summary>
public static class ImportMappingFingerprint
{
    /// <summary>
    /// Computes a case-insensitive, order-independent SHA-256 fingerprint of the template mappings.
    /// </summary>
    public static string Compute(ImportTemplate template)
    {
        var canonical = string.Join(
            "\n",
            template.ColumnMappings
                .OrderBy(mapping => mapping.SourceColumnName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(mapping => mapping.TargetField)
                .Select(mapping => $"{mapping.SourceColumnName.Trim().ToUpperInvariant()}={mapping.TargetField}"));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
