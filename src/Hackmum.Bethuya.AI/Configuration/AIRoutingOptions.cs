namespace Hackmum.Bethuya.AI.Configuration;

/// <summary>
/// Routing configuration — maps data sensitivity levels to provider names.
/// Bound from "AI" configuration section.
/// </summary>
public sealed class AIRoutingOptions
{
    public const string SectionName = "AI";

    /// <summary>Provider for sensitive/PII data (default: FoundryLocal).</summary>
    public string SensitiveProvider { get; set; } = "FoundryLocal";

    /// <summary>Provider for non-sensitive data (default: AzureOpenAI).</summary>
    public string NonSensitiveProvider { get; set; } = "AzureOpenAI";

    /// <summary>Provider for public content (default: OpenAI).</summary>
    public string PublicProvider { get; set; } = "OpenAI";

    /// <summary>Fallback provider if primary is unavailable.</summary>
    public string FallbackProvider { get; set; } = "Ollama";

    /// <summary>Named provider configurations.</summary>
    public Dictionary<string, AIProviderOptions> Providers { get; set; } = [];
}
