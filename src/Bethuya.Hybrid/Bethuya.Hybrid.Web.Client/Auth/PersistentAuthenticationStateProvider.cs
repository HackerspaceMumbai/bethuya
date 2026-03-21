using System.Security.Claims;
using Bethuya.Hybrid.Shared.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bethuya.Hybrid.Web.Client.Auth;

/// <summary>
/// WASM client-side <see cref="AuthenticationStateProvider"/> that reads the user info
/// persisted by the server via <see cref="PersistentComponentState"/>.
/// </summary>
internal sealed class PersistentAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly Task<AuthenticationState> DefaultUnauthenticatedTask =
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    private readonly Task<AuthenticationState> _authenticationStateTask = DefaultUnauthenticatedTask;

    public PersistentAuthenticationStateProvider(PersistentComponentState state)
    {
        if (!state.TryTakeFromJson<UserInfo>(nameof(UserInfo), out var userInfo) || userInfo is null)
        {
            return;
        }

        var claims = new List<Claim>
        {
            new("sub", userInfo.UserId),
            new("name", userInfo.Name),
            new("email", userInfo.Email),
        };

        foreach (var role in userInfo.Roles)
        {
            claims.Add(new("role", role));
        }

        _authenticationStateTask = Task.FromResult(
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bethuya", nameType: "name", roleType: "role"))));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _authenticationStateTask;
}
