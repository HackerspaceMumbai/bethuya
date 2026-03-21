namespace Bethuya.Hybrid.Shared.Auth;

/// <summary>
/// Lightweight, serializable representation of the authenticated user.
/// Persisted from server to WASM client via <see cref="Microsoft.AspNetCore.Components.PersistentComponentState"/>.
/// </summary>
public sealed record UserInfo(string UserId, string Email, string Name, string[] Roles);
