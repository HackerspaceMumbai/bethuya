using System.Globalization;

using Bethuya.Hybrid.Shared.Services;

namespace Bethuya.Hybrid.Shared.Components.Passport;

internal static class PassportPresentation
{
    internal static IReadOnlyList<CommunitySignalDto> Roles(CommunityPassportExperienceDto model)
        => model.Signals
            .Where(signal => !signal.IsOrganizerAwarded && signal.Kind != "Champion")
            .Take(4)
            .ToArray();

    internal static IReadOnlyList<CommunitySignalDto> Recognition(CommunityPassportExperienceDto model)
        => model.Signals
            .Where(signal => signal.IsOrganizerAwarded || signal.Kind == "Champion")
            .ToArray();

    internal static IReadOnlyList<string> VerifiedCommunities(CommunityPassportExperienceDto model)
        => model.Contributions
            .Select(item => item.Community)
            .Append(model.Identity.CommunityName)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

    internal static string JourneyTitle(string name)
        => name.EndsWith("Track", StringComparison.OrdinalIgnoreCase) ? name : $"{name} Track";

    internal static string JourneyIcon(string name)
        => name.Contains("Mentor", StringComparison.OrdinalIgnoreCase) ? "users-round"
            : name.Contains("Volunteer", StringComparison.OrdinalIgnoreCase) ? "hand-heart"
            : name.Contains("Speaker", StringComparison.OrdinalIgnoreCase) ? "mic-2"
            : name.Contains("Builder", StringComparison.OrdinalIgnoreCase) ? "blocks"
            : "route";

    internal static IEnumerable<string> EligibilityReasons(PassportOpportunityDto opportunity)
    {
        var reasons = opportunity.Lifecycle
            .Select(item => item.Explanation)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(3)
            .ToArray();
        return reasons.Length > 0
            ? reasons
            : ["Verified contribution history", "Consent-enabled recommendations"];
    }

    internal static string ContributionProvenance(PassportContributionDto contribution)
        => $"{contribution.Attestation} · {contribution.Community}";

    internal static string HumanizeProvenance(string evidence)
        => $"Verified participation record · {evidence}";

    internal static string Initials(string name)
        => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => char.ToUpperInvariant(part[0])));

    internal static string PluralizeConnection(string kind, int count)
        => count == 1 ? kind : kind.EndsWith('s') ? kind : $"{kind}s";

    internal static int ActiveWeeks(IReadOnlyList<ActivityDayDto> activity)
        => activity
            .Where(day => day.Count > 0 && day.Date >= DateOnly.FromDateTime(DateTime.Today.AddDays(-181)))
            .Select(day =>
            {
                var date = day.Date.ToDateTime(TimeOnly.MinValue);
                return (ISOWeek.GetYear(date), ISOWeek.GetWeekOfYear(date));
            })
            .Distinct()
            .Count();
}
