using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Backend.Services;

public sealed class InclusionSignalsNormalizer
{
    private static readonly Regex LanguageSplitRegex = new(@"[,;/\|]+", RegexOptions.Compiled);

    public InclusionSignals FromSource(AttendeeInclusionSource? source)
    {
        if (source is null)
        {
            return new InclusionSignals();
        }

        var languages = NormalizeLanguages(source.LanguageProficiency);
        var speaksMarathi = languages.Contains("marathi", StringComparer.Ordinal);
        var speaksKonkani = languages.Contains("konkani", StringComparer.Ordinal);

        return new InclusionSignals
        {
            GeoBucket = NormalizeGeoBucket(source.Neighborhood),
            LanguagesNormalized = languages,
            SpeaksMarathi = speaksMarathi,
            SpeaksKonkani = speaksKonkani,
            HasLocalLanguage = speaksMarathi || speaksKonkani,
            EducationBucket = NormalizeEducationBucket(source.EducationalBackground),
            SocioeconomicBucket = NormalizeSocioeconomicBucket(source.SocioeconomicBackground)
        };
    }

    public List<string> NormalizeLanguages(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var normalized = LanguageSplitRegex
            .Split(raw)
            .Select(NormalizeLanguageToken)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

        return normalized;
    }

    private static string NormalizeLanguageToken(string token)
    {
        var trimmed = token.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var lower = trimmed.ToLowerInvariant();
        var folded = RemoveDiacritics(lower);

        if (folded is "marathi" or "marati" || trimmed is "मराठी")
        {
            return "marathi";
        }

        if (folded is "konkani" or "konkni" || trimmed is "कोंकणी")
        {
            return "konkani";
        }

        return folded;
    }

    private static GeoBucket NormalizeGeoBucket(string? rawNeighborhood)
    {
        if (string.IsNullOrWhiteSpace(rawNeighborhood))
        {
            return GeoBucket.Unknown;
        }

        var value = rawNeighborhood.ToLowerInvariant();

        if (ContainsAny(value, "colaba", "fort", "churchgate", "marine", "cuffe", "byculla", "mazgaon"))
        {
            return GeoBucket.MumbaiIslandCity;
        }

        if (ContainsAny(value, "andheri", "bandra", "borivali", "malad", "powai", "kurla", "goregaon", "jogeshwari", "santacruz", "chembur", "mulund", "ghatkopar", "vile parle"))
        {
            return GeoBucket.MumbaiSuburban;
        }

        if (ContainsAny(value, "thane", "navi mumbai", "vashi", "panvel", "kalyan", "dombivli", "vasai", "virar", "mira", "bhayander", "ulhasnagar"))
        {
            return GeoBucket.MumbaiMetropolitanRegion;
        }

        return GeoBucket.OutsideMmr;
    }

    private static EducationBucket NormalizeEducationBucket(string? rawEducation)
    {
        if (string.IsNullOrWhiteSpace(rawEducation))
        {
            return EducationBucket.Unknown;
        }

        var value = rawEducation.ToLowerInvariant();

        if (ContainsAny(value, "doctorate", "phd", "dphil"))
        {
            return EducationBucket.Doctorate;
        }

        if (ContainsAny(value, "master", "postgraduate", "post-graduate", "m.tech", "mtech", "mba", "msc", "m.sc", "ma ", "m.a"))
        {
            return EducationBucket.Postgraduate;
        }

        if (ContainsAny(value, "bachelor", "undergraduate", "under-grad", "b.tech", "btech", "be ", "b.e", "bsc", "b.sc", "ba ", "b.a", "bcom"))
        {
            return EducationBucket.Undergraduate;
        }

        if (ContainsAny(value, "diploma", "certificate", "iti"))
        {
            return EducationBucket.DiplomaOrCertificate;
        }

        if (ContainsAny(value, "no formal", "secondary", "higher secondary", "10+2", "school"))
        {
            return EducationBucket.SchoolOrLower;
        }

        if (ContainsAny(value, "self-taught", "self taught", "bootcamp", "alternative"))
        {
            return EducationBucket.AlternativePath;
        }

        return EducationBucket.Unknown;
    }

    private static SocioeconomicBucket? NormalizeSocioeconomicBucket(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.ToLowerInvariant();

        if (ContainsAny(value, "working class"))
        {
            return SocioeconomicBucket.WorkingClass;
        }

        if (ContainsAny(value, "lower middle"))
        {
            return SocioeconomicBucket.LowerMiddleClass;
        }

        if (ContainsAny(value, "middle class"))
        {
            return SocioeconomicBucket.MiddleClass;
        }

        if (ContainsAny(value, "upper middle"))
        {
            return SocioeconomicBucket.UpperMiddleClass;
        }

        if (ContainsAny(value, "upper class"))
        {
            return SocioeconomicBucket.UpperClass;
        }

        return SocioeconomicBucket.Unknown;
    }

    private static bool ContainsAny(string value, params string[] tokens)
        => tokens.Any(value.Contains);

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        Span<char> buffer = stackalloc char[normalized.Length];
        var length = 0;
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                buffer[length++] = c;
            }
        }

        return new string(buffer[..length]).Normalize(NormalizationForm.FormC);
    }
}
