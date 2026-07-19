using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Validates and normalizes canonical session records before persistence.
/// </summary>
public static class SessionNormalizationEngine
{
    public static NormalizedSession Normalize(NormalizedSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var title = NormalizeRequired(session.Title, 200, "Session title");
        var description = NormalizeOptional(session.Description, 2000);
        var speakers = session.Speakers
            .Select(NormalizeSpeaker)
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .ToArray();

        if (session.Duration is not null && session.Duration <= TimeSpan.Zero)
        {
            throw new ArgumentException("Session duration must be greater than zero.", nameof(session));
        }

        return session with
        {
            Title = title,
            Description = description,
            Speakers = speakers,
            SourceSessionId = NormalizeOptional(session.SourceSessionId, 200)
        };
    }

    private static NormalizedSpeaker NormalizeSpeaker(NormalizedSpeaker speaker)
        => speaker with
        {
            Name = NormalizeRequired(speaker.Name, 200, "Speaker name"),
            GitHubHandle = NormalizeHandle(speaker.GitHubHandle),
            TwitterHandle = NormalizeHandle(speaker.TwitterHandle),
            AvatarUrl = NormalizeHttpsUrl(speaker.AvatarUrl, "Speaker avatar URL")
        };

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

    private static string? NormalizeHandle(string? value)
    {
        var normalized = NormalizeOptional(value?.TrimStart('@'), 100);
        return normalized is null ? null : normalized.TrimStart('@');
    }

    private static string? NormalizeHttpsUrl(string? value, string fieldName)
    {
        var normalized = NormalizeOptional(value, 2048);
        if (normalized is null)
        {
            return null;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException($"{fieldName} must be an absolute HTTPS URL.", fieldName);
        }

        return normalized;
    }
}
