using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>Extension methods for configuring Bethuya authorization policies.</summary>
public static class BethuyaAuthorizationExtensions
{
    /// <summary>
    /// Adds Bethuya authorization policies based on platform roles.
    /// Call from both Web and Backend projects to ensure consistent policy enforcement.
    /// </summary>
    /// <remarks>
    /// Policy and role names mirror <c>Bethuya.Hybrid.Shared.Auth.BethuyaRoles</c>
    /// and <c>BethuyaAuthorizationPolicies</c>. Keep in sync.
    /// </remarks>
    public static TBuilder AddBethuyaAuthorization<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("RequireAdmin", policy =>
                policy.RequireRole("Admin"))
            .AddPolicy("RequireOrganizer", policy =>
                policy.RequireRole("Admin", "Organizer"))
            .AddPolicy("RequireCurator", policy =>
                policy.RequireRole("Admin", "Curator"))
            .AddPolicy("RequireAttendee", policy =>
                policy.RequireRole("Admin", "Organizer", "Curator", "Attendee"));

        return builder;
    }
}
