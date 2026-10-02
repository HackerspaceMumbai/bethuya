using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Shared.Models.CommandCenter;
using System.Security.Claims;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Resolves the display-only command-center audience from trusted authentication claims.</summary>
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
            var developmentAudience = developmentPersona switch
            {
                "Anish" => new CommandCenterAudience(CommunityRole.CommunityMember, displayName, "Member journey", "Find a welcoming next step and build a consistent participation rhythm."),
                "Priya" => new CommandCenterAudience(CommunityRole.VolunteerLead, displayName, "Volunteer lead", "Close coverage gaps and help reliable volunteers grow into ownership."),
                "Rohan" => new CommandCenterAudience(CommunityRole.EventOrganizer, displayName, "Event organizer", "Keep event readiness, approvals, and execution risks moving."),
                "Maya" => new CommandCenterAudience(CommunityRole.MentorshipLead, displayName, "Mentorship lead", "Turn mentorship demand into thoughtful, human-reviewed pairings."),
                "Farah" => new CommandCenterAudience(CommunityRole.EmergingContributor, displayName, "Community member · Emerging contributor", "Build on your session contribution and discover the next opportunity to lead."),
                "Vikram" => new CommandCenterAudience(CommunityRole.CommunityAdministrator, displayName, "Community administrator", "Balance community health, operations, and cross-workspace decisions."),
                _ => null
            };

            if (developmentAudience is not null)
            {
                return developmentAudience;
            }
        }

        return ResolveFromRoles(principal, displayName);
    }

    private static CommandCenterAudience ResolveFromRoles(ClaimsPrincipal principal, string displayName)
    {
        if (principal.IsInRole(BethuyaRoles.Admin))
        {
            return new(CommunityRole.CommunityAdministrator, displayName, "Community administrator", "Balance community health, operations, and cross-workspace decisions.");
        }

        if (principal.IsInRole(BethuyaRoles.Organizer))
        {
            return new(CommunityRole.EventOrganizer, displayName, "Event organizer", "Keep event readiness, approvals, and execution risks moving.");
        }

        return new(CommunityRole.CommunityMember, displayName, "Community member", "Find a welcoming next step and build a consistent participation rhythm.");
    }
}
