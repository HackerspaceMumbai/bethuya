namespace Bethuya.Hybrid.Web.Auth;

/// <summary>
/// Marker service that governs whether <c>DevPersonaToolbar</c> is available.
/// </summary>
/// <remarks>
/// This is registered in DI (see <c>Program.cs</c>) using the exact same condition used to
/// register the Layer 2 persona selection endpoints (<c>Authentication:Provider=None</c> AND
/// <c>Environment=Development</c>) — the single source of truth for "dev-only" gating.
/// The toolbar resolves this marker via <see cref="IServiceProvider"/> at initialization and
/// renders nothing when it is absent. This makes the toolbar inert by composition (DI presence)
/// rather than relying solely on a cosmetic Razor <c>@if</c> that re-implements the environment
/// and provider check inline, which could drift out of sync with the endpoint gating over time.
/// </remarks>
public interface IDevPersonaToolbarFeature
{
}

/// <summary>Default (and only) implementation — an empty marker with no behavior.</summary>
internal sealed class DevPersonaToolbarFeature : IDevPersonaToolbarFeature
{
}
