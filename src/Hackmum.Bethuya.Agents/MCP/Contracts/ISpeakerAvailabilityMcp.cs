using Refit;

namespace Hackmum.Bethuya.Agents.MCP.Contracts;

/// <summary>
/// MCP tool for retrieving speaker availability information.
/// Used by Scout agent to inform Planner about speaker availability.
/// </summary>
public interface ISpeakerAvailabilityMcp
{
    /// <summary>
    /// Gets availability info for a specific speaker during a date range.
    /// </summary>
    [Get("/api/speakers/{speakerId}/availability")]
    Task<SpeakerAvailability> GetAvailabilityAsync(
        string speakerId,
        [Query] DateOnly startDate,
        [Query] DateOnly endDate,
        CancellationToken ct = default);

    /// <summary>
    /// Gets availability for multiple speakers (convenience method for fan-out queries).
    /// </summary>
    [Get("/api/speakers/availability")]
    Task<List<SpeakerAvailability>> GetMultipleAvailabilityAsync(
        [Query] List<string> speakerIds,
        [Query] DateOnly startDate,
        [Query] DateOnly endDate,
        CancellationToken ct = default);
}

/// <summary>
/// Speaker availability and profile information for planning.
/// </summary>
public sealed record SpeakerAvailability(
    string SpeakerId,
    string Name,
    string Bio,
    List<string> Topics,
    List<DateTimeOffset> AvailableSlots,
    int MaxSessionsPerYear);
