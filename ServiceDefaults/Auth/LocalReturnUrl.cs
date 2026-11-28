using System.Diagnostics.CodeAnalysis;

namespace ServiceDefaults.Auth;

/// <summary>Validates post-sign-in return URLs so redirects stay on this site.</summary>
public static class LocalReturnUrl
{
    /// <summary>
    /// Returns <see langword="true"/> only for app-relative paths such as <c>/events</c>. Rejects absolute URLs,
    /// <c>//host</c>, <c>/\host</c> (browsers treat the backslash as a slash) and control characters
    /// (browsers strip tabs and newlines, so <c>/\t/host</c> also becomes <c>//host</c>).
    /// </summary>
    public static bool IsLocal([NotNullWhen(true)] string? url) =>
        !string.IsNullOrEmpty(url)
        && url[0] == '/'
        && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'))
        && !url.Any(char.IsControl);

    /// <summary>Returns <paramref name="url"/> when it is local, otherwise <paramref name="fallback"/>.</summary>
    public static string GetOrDefault(string? url, string fallback = "/") => IsLocal(url) ? url : fallback;
}
