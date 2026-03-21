namespace Hackmum.Bethuya.AI.Configuration;

/// <summary>
/// Configuration for a single AI provider endpoint.
/// </summary>
public sealed class AIProviderOptions
{
    public required string Name { get; set; }
    public required string Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? ModelId { get; set; }
    public bool IsLocal { get; set; }
}
