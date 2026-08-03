namespace ServiceDefaults.Auth;

/// <summary>
/// Fixed, deterministic allowlist of development personas. The opaque persona <em>key</em>
/// is the only value that crosses the wire (cookie on Web requests, header on Backend-direct
/// requests). The Backend constructs all claims from this catalog — callers never supply
/// roles or profile data.
/// </summary>
/// <remarks>
/// <para>
/// Cookie name: <see cref="PersonaCookieName"/> (<c>bethuya-dev-persona</c>)<br/>
/// Header name: <see cref="PersonaHeaderName"/> (<c>X-Bethuya-Dev-Persona</c>)
/// </para>
/// <para>
/// Both surfaces (Web SSR cookie and Backend-direct header) use the same allowlist and
/// the same fail-closed validation logic. The header surface is populated exclusively by
/// <c>DevPersonaPropagationHandler</c> on outbound Refit calls, but since the Backend
/// cannot cryptographically distinguish an internally-generated header from an externally
/// supplied one, both are validated identically — no special-casing based on trust intent.
/// </para>
/// </remarks>
public static class DevelopmentPersonaCatalog
{
    /// <summary>
    /// Cookie name used by the Web SSR host to store the selected persona key.
    /// The <c>DevelopmentAuthenticationHandler</c> reads this on Web requests.
    /// </summary>
    public const string PersonaCookieName = "bethuya-dev-persona";

    /// <summary>
    /// Header name used by <c>DevPersonaPropagationHandler</c> to forward the selected
    /// persona key to the Backend API. The <c>DevelopmentAuthenticationHandler</c> reads
    /// this header before checking the cookie (priority order: header > cookie > legacy default).
    /// </summary>
    public const string PersonaHeaderName = "X-Bethuya-Dev-Persona";

    private static readonly Dictionary<string, DevelopmentPersona> Personas;

    static DevelopmentPersonaCatalog()
    {
        // NOTE: email domain "bethuya.dev" is intentionally different from "bethuya.local"
        // (the legacy fixed-admin default). This keeps the two identity spaces visually
        // distinguishable in logs and assertions without any schema change.
        DevelopmentPersona[] entries =
        [
            new("Anish",  "dev-persona-anish",  "Anish",  "anish@bethuya.dev",  [BethuyaRoleNames.Attendee]),
            new("Priya",  "dev-persona-priya",  "Priya",  "priya@bethuya.dev",  [BethuyaRoleNames.Curator, BethuyaRoleNames.Attendee]),
            new("Rohan",  "dev-persona-rohan",  "Rohan",  "rohan@bethuya.dev",  [BethuyaRoleNames.Attendee]),
            new("Maya",   "dev-persona-maya",   "Maya",   "maya@bethuya.dev",   [BethuyaRoleNames.Attendee]),
            new("Farah",  "dev-persona-farah",  "Farah",  "farah@bethuya.dev",  [BethuyaRoleNames.Attendee]),
            new("Vikram", "dev-persona-vikram", "Vikram", "vikram@bethuya.dev", [BethuyaRoleNames.Admin, BethuyaRoleNames.Organizer, BethuyaRoleNames.Curator, BethuyaRoleNames.Attendee]),
        ];

        Personas = new Dictionary<string, DevelopmentPersona>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            Personas[entry.Key] = entry;
        }
    }

    /// <summary>All personas in the catalog.</summary>
    public static IReadOnlyCollection<DevelopmentPersona> All => [.. Personas.Values];

    /// <summary>Valid persona keys (case-insensitive).</summary>
    public static IReadOnlyCollection<string> Keys => [.. Personas.Keys];

    /// <summary>
    /// Looks up a persona by key (case-insensitive).
    /// Returns <see langword="false"/> for any key not in the allowlist,
    /// including empty, null, or tampered values.
    /// </summary>
    public static bool TryGet(string? key, out DevelopmentPersona? persona)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            persona = null;
            return false;
        }

        return Personas.TryGetValue(key, out persona);
    }
}
