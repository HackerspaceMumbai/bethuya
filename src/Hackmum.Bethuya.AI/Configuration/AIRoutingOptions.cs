namespace Hackmum.Bethuya.AI.Configuration;

/// <summary>
/// Routing configuration - maps data sensitivity levels to provider names.
/// Bound from "AI" configuration section.
/// </summary>
public sealed class AIRoutingOptions
{
    public const string SectionName = "AI";

    /// <summary>Provider for sensitive/PII data (default: FoundryLocal - on-device only).</summary>
    public string SensitiveProvider { get; set; } = "FoundryLocal";

    /// <summary>Provider for non-sensitive data (default: Foundry - primary, falls back to Ollama then OpenAI).</summary>
    public string NonSensitiveProvider { get; set; } = "Foundry";

    /// <summary>Provider for public content (default: Foundry - primary, falls back to Ollama then OpenAI).</summary>
    public string PublicProvider { get; set; } = "Foundry";

    /// <summary>Fallback provider if primary is unavailable (Ollama → OpenAI chain).</summary>
    public string FallbackProvider { get; set; } = "Ollama";

    /// <summary>Named provider configurations.</summary>
    public Dictionary<string, AIProviderOptions> Providers { get; set; } = [];
}
