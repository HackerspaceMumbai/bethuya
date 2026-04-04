namespace Bethuya.Hybrid.Shared.Offline;

/// <summary>
/// Defines storage operations for offline draft instances identified by a unique identifier.
/// </summary>
/// <typeparam name="T">The type of draft stored by this interface.</typeparam>
public interface IOfflineDraftStore<T>
{
    /// <summary>
    /// Saves the specified draft using the provided identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the draft.</param>
    /// <param name="draft">The draft instance to persist.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SaveAsync(string id, T draft);

    /// <summary>
    /// Loads the draft associated with the provided identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the draft.</param>
    /// <returns>
    /// A task that represents the asynchronous load operation. The task result contains the draft
    /// if one is found; otherwise, <see langword="null" />.
    /// </returns>
    Task<T?> LoadAsync(string id);

    /// <summary>
    /// Determines whether a draft exists for the provided identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the draft.</param>
    /// <returns>
    /// A task that represents the asynchronous existence check. The task result is
    /// <see langword="true" /> if a draft exists; otherwise, <see langword="false" />.
    /// </returns>
    Task<bool> ExistsAsync(string id);
}