using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Shared.Models.CommandCenter;
using System.Security.Claims;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Resolves the display-only homepage audience from trusted authentication claims.</summary>
public interface ICommandCenterAudienceResolver
{
    /// <summary>Resolves the homepage audience without changing the principal's authorization roles.</summary>
    CommandCenterAudience Resolve(ClaimsPrincipal principal);
}

/// <summary>Maps authenticated identities to deterministic homepage audiences.</summary>
public sealed class ClaimsCommandCenterAudienceResolver : ICommandCenterAudienceResolver
{
    /// <inheritdoc />
    public CommandCenterAudience Resolve(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var displayName = principal.FindFirst("name")?.Value
            ?? principal.Identity?.Name
            ?? "Community member";
        const string developmentPersonaClaimType = "bethuya:development-persona";
        const string developmentPersonaClaimIssuer = "Bethuya.Development";
        var developmentPersona = principal.Claims.FirstOrDefault(claim =>
            claim.Type == developmentPersonaClaimType &&
            claim.Issuer == developmentPersonaClaimIssuer)?.Value;

        if (developmentPersona is not null)
        {
            var developmentRole = developmentPersona switch
            {
                "Anish" => CommunityRole.EventParticipant,
                "Priya" => CommunityRole.VolunteerLead,
                "Rohan" => CommunityRole.EventOrganizer,
                "Maya" => CommunityRole.MentorshipLead,
                "Farah" => CommunityRole.EmergingContributor,
                "Vikram" => CommunityRole.CommunityAdministrator,
                _ => (CommunityRole?)null
            };

            if (developmentRole is { } persona)
            {
                return Create(persona, displayName);
            }
        }

        return ResolveFromRoles(principal, displayName);
    }

    /// <summary>
    /// Operations roles share one command center; participation roles share one journey homepage.
    /// The surface is the architectural distinction, not the individual role.
    /// </summary>
    public static CommandCenterSurface GetSurface(CommunityRole role) => role switch
    {
        CommunityRole.CommunityAdministrator or
        CommunityRole.EventOrganizer or
        CommunityRole.VolunteerLead or
        CommunityRole.MentorshipLead => CommandCenterSurface.Operations,
        _ => CommandCenterSurface.Participation
    };

    private static CommandCenterAudience ResolveFromRoles(ClaimsPrincipal principal, string displayName)
    {
        if (principal.IsInRole(BethuyaRoles.Admin))
        {
            return Create(CommunityRole.CommunityAdministrator, displayName);
        }

        if (principal.IsInRole(BethuyaRoles.Organizer))
        {
            return Create(CommunityRole.EventOrganizer, displayName);
        }

        return Create(CommunityRole.CommunityMember, displayName);
    }

    private static CommandCenterAudience Create(CommunityRole role, string displayName) => role switch
    {
        CommunityRole.CommunityAdministrator => new(role, GetSurface(role), displayName, "Community administrator", "Balance community health, retention, leadership pipeline, and cross-workspace decisions."),
        CommunityRole.EventOrganizer => new(role, GetSurface(role), displayName, "Event organizer", "Keep capacity, waitlists, volunteer coverage, and speaker readiness moving."),
        CommunityRole.VolunteerLead => new(role, GetSurface(role), displayName, "Volunteer lead", "Close coverage gaps, protect volunteers from burnout, and recognise reliability."),
        CommunityRole.MentorshipLead => new(role, GetSurface(role), displayName, "Mentorship lead", "Balance mentor supply, pairing latency, and mentee progress."),
        CommunityRole.EmergingContributor => new(role, GetSurface(role), displayName, "Emerging contributor", "Build on your contributions and discover the next opportunity to lead."),
        CommunityRole.EventParticipant => new(role, GetSurface(role), displayName, "Event participant", "Get ready for your event and find your first step into the community."),
        _ => new(CommunityRole.CommunityMember, GetSurface(CommunityRole.CommunityMember), displayName, "Community member", "Follow your journey and find the next way to participate more deeply.")
    };
}
