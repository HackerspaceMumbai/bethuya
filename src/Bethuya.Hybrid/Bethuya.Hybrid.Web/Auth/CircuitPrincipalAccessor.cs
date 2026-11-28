using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Provides async-flow access to the current Blazor <c>InteractiveServer</c> circuit's
/// <see cref="ClaimsPrincipal"/> so <c>IHttpClientFactory</c> message handlers (which run in a
/// separate DI scope from the circuit) and synchronous user services can resolve the signed-in
/// identity even when <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor.HttpContext"/>
/// is unavailable.
/// </summary>
public interface ICircuitPrincipalAccessor
{
    /// <summary>Gets or sets the active circuit's <see cref="ClaimsPrincipal"/> for the current async execution flow.</summary>
    ClaimsPrincipal? CurrentPrincipal { get; set; }
}

internal sealed class CircuitPrincipalAccessor : ICircuitPrincipalAccessor
{
    private static readonly AsyncLocal<PrincipalHolder?> Holder = new();

    public ClaimsPrincipal? CurrentPrincipal
    {
        get => Holder.Value?.Principal;
        set
        {
            var current = Holder.Value;
            if (current is not null)
            {
                current.Principal = null;
            }

            if (value is not null)
            {
                Holder.Value = new PrincipalHolder { Principal = value };
            }
        }
    }

    private sealed class PrincipalHolder
    {
        public ClaimsPrincipal? Principal { get; set; }
    }
}

/// <summary>
/// Wraps every inbound Blazor Server circuit dispatch so <see cref="ICircuitPrincipalAccessor.CurrentPrincipal"/>
/// holds the circuit's authenticated <see cref="ClaimsPrincipal"/> across awaits into Refit HTTP handlers.
/// </summary>
internal sealed class BethuyaCircuitHandler(
    AuthenticationStateProvider authenticationStateProvider,
    ICircuitPrincipalAccessor principalAccessor) : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
    {
        return async context =>
        {
            var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
            var previousPrincipal = principalAccessor.CurrentPrincipal;
            principalAccessor.CurrentPrincipal = authenticationState.User;
            try
            {
                await next(context).ConfigureAwait(false);
            }
            finally
            {
                principalAccessor.CurrentPrincipal = previousPrincipal;
            }
        };
    }
}
