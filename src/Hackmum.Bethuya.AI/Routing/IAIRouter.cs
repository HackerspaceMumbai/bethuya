using Microsoft.Extensions.AI;

namespace Hackmum.Bethuya.AI.Routing;

/// <summary>
/// Routes AI requests to the appropriate provider based on data sensitivity.
/// </summary>
public interface IAIRouter
{
    /// <summary>
    /// Gets an IChatClient for the given data sensitivity level.
    /// Automatically selects the correct provider based on routing config.
    /// </summary>
    IChatClient GetChatClient(DataSensitivity sensitivity);

    /// <summary>
    /// Gets the provider name that would be selected for the given sensitivity.
    /// Useful for logging and audit trails.
    /// </summary>
    string GetProviderName(DataSensitivity sensitivity);
}
