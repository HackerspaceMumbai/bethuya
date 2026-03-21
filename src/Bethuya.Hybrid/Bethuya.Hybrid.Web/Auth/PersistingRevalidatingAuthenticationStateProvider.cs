using System.Diagnostics;
using System.Security.Claims;
using Bethuya.Hybrid.Shared.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Server-side <see cref="AuthenticationStateProvider"/> that revalidates the authentication state
/// at a configurable interval and persists user info to <see cref="PersistentComponentState"/>
/// so the WASM client can read it.
/// </summary>
internal sealed class PersistingRevalidatingAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PersistentComponentState _state;
    private readonly PersistingComponentStateSubscription _subscription;
    private Task<AuthenticationState>? _authenticationStateTask;

    public PersistingRevalidatingAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        PersistentComponentState state)
        : base(loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        AuthenticationStateChanged += OnAuthenticationStateChanged;
        _subscription = _state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        // Get fresh user from a new scope to avoid DbContext concurrency issues
        await using var scope = _scopeFactory.CreateAsyncScope();
        return ValidatePrincipal(authenticationState.User);
    }

    private static bool ValidatePrincipal(ClaimsPrincipal principal)
    {
        // Validate the security stamp or token expiry as needed.
        // For OIDC, we trust the cookie; the middleware handles token refresh.
        return principal.Identity?.IsAuthenticated ?? false;
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        _authenticationStateTask = task;
    }

    private async Task OnPersistingAsync()
    {
        if (_authenticationStateTask is null)
        {
            throw new UnreachableException($"{nameof(_authenticationStateTask)} was not set in {nameof(OnAuthenticationStateChanged)}.");
        }

        var authenticationState = await _authenticationStateTask;
        var principal = authenticationState.User;

        if (principal.Identity?.IsAuthenticated == true)
        {
            var userInfo = new UserInfo(
                UserId: principal.FindFirst("sub")?.Value ?? principal.FindFirst("oid")?.Value ?? "",
                Email: principal.FindFirst("email")?.Value ?? principal.FindFirst("preferred_username")?.Value ?? "",
                Name: principal.FindFirst("name")?.Value ?? "",
                Roles: principal.FindAll(principal.Identities.First().RoleClaimType).Select(c => c.Value).ToArray());

            _state.PersistAsJson(nameof(UserInfo), userInfo);
        }
    }

    void IDisposable.Dispose()
    {
        _subscription.Dispose();
        AuthenticationStateChanged -= OnAuthenticationStateChanged;
        base.Dispose(true);
    }
}
