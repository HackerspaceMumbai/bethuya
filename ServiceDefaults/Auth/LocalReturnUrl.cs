namespace ServiceDefaults.Auth;

/// <summary>
/// Validates post-login <c>returnUrl</c> values so the login flow cannot be used as an open redirect.
/// </summary>
public static class LocalReturnUrl
{
    /// <summary>Destination used whenever a supplied value is missing or not a local path.</summary>
    public const string DefaultPath = "/";

    /// <summary>
    /// Returns <paramref name="returnUrl"/> when it is a local absolute path such as <c>/events?tab=1</c>;
    /// otherwise returns <see cref="DefaultPath"/>.
    /// </summary>
    /// <remarks>
    /// Rejected: absolute URLs (<c>https://evil.example</c>), protocol-relative URLs (<c>//evil.example</c>),
    /// backslash variants (<c>/\evil.example</c>, which browsers normalise to <c>//</c>), and any value
    /// containing control characters (tabs and newlines are stripped by browsers, so <c>/\t/evil</c> would
    /// otherwise become <c>//evil</c>).
    /// </remarks>
    public static string Resolve(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return DefaultPath;
        }

        if (returnUrl[0] != '/' || (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\')))
        {
            return DefaultPath;
        }

        foreach (var character in returnUrl)
        {
            if (character == '\\' || char.IsControl(character))
            {
                return DefaultPath;
            }
        }

        return returnUrl;
    }
}
