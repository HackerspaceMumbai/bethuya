namespace Hackmum.Bethuya.AI.Routing;

/// <summary>
/// Classifies AI request sensitivity for privacy-aware routing.
/// Sensitive → local-only (Foundry Local); NonSensitive → cloud OK; Public → any provider.
/// </summary>
public enum DataSensitivity
{
    /// <summary>Contains PII or attendee data — route to on-device provider only (Foundry Local).</summary>
    Sensitive,

    /// <summary>No PII but internal content — route to enterprise boundary (Azure OpenAI).</summary>
    NonSensitive,

    /// <summary>Public content — any provider acceptable.</summary>
    Public
}
