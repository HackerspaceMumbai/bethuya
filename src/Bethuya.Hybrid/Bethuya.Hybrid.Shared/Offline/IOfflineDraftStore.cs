using System.Threading;

namespace Bethuya.Hybrid.Shared.Offline;

public interface IOfflineDraftStore<T>
{
    /// <summary>
    /// Saves the specified draft using the provided identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the draft to save.</param>
    /// <param name="draft">The draft instance to persist.</param>
    /// <param name="ct">A cancellation token for the operation.</param>
    Task SaveAsync(string id, T draft, CancellationToken ct = default);

    /// <summary>
    /// Loads the draft associated with the provided identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the draft to load.</param>
    /// <param name="ct">A cancellation token for the operation.</param>
    /// <returns>
    /// The draft when found; otherwise, <see langword="null" />.
    /// </returns>
    Task<T?> LoadAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Determines whether a draft exists for the provided identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the draft to check.</param>
    /// <param name="ct">A cancellation token for the operation.</param>
    Task<bool> ExistsAsync(string id, CancellationToken ct = default);
}