using System.Text.Json;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Builds deterministic recommendation input snapshots and hashes covering both request parameters and read-model data.
/// </summary>
internal static class RecommendationInputSnapshot
{
    public static (string InputHash, JsonElement InputSnapshot) Create<TRequest>(
        TRequest request,
        CommunityHealthDashboardReadModelResponse dashboard,
        IReadOnlyList<MentorProfile>? discoverableMentors = null)
    {
        var dashboardSnapshot = new DashboardMetricsSnapshot(
            LookbackDays: dashboard.LookbackDays,
            Retention: dashboard.Retention,
            Attendance: dashboard.Attendance,
            VolunteerGrowth: dashboard.VolunteerGrowth,
            LeadershipFunnel: dashboard.LeadershipFunnel);

        var mentorSnapshots = discoverableMentors is null
            ? null
            : discoverableMentors
                .OrderBy(profile => profile.MemberId.Value)
                .ThenBy(profile => profile.Id.Value)
                .Select(profile => new MentorSnapshot(
                    MentorProfileId: profile.Id.Value,
                    MemberId: profile.MemberId.Value,
                    AvailabilityHoursPerMonth: profile.AvailabilityHoursPerMonth,
                    ExpertiseAreas: [.. profile.ExpertiseAreas
                        .Select(area => area.ToString())
                        .OrderBy(area => area, StringComparer.Ordinal)]))
                .ToArray();

        var hashedInput = new HashedRecommendationInput<TRequest>(
            Request: request,
            Dashboard: dashboardSnapshot,
            DiscoverableMentors: mentorSnapshots);

        var persistedSnapshot = new PersistedRecommendationInputSnapshot<TRequest>(
            Request: request,
            Dashboard: dashboardSnapshot,
            DiscoverableMentors: mentorSnapshots,
            SnapshotCapturedAtUtc: dashboard.AsOfUtc);

        var inputHash = AiAuditMetadata.ComputeInputHash(hashedInput);
        var inputSnapshot = JsonSerializer.SerializeToElement(persistedSnapshot);
        return (inputHash, inputSnapshot);
    }

    private sealed record HashedRecommendationInput<TRequest>(
        TRequest Request,
        DashboardMetricsSnapshot Dashboard,
        IReadOnlyList<MentorSnapshot>? DiscoverableMentors);

    private sealed record PersistedRecommendationInputSnapshot<TRequest>(
        TRequest Request,
        DashboardMetricsSnapshot Dashboard,
        IReadOnlyList<MentorSnapshot>? DiscoverableMentors,
        DateTimeOffset SnapshotCapturedAtUtc);

    private sealed record DashboardMetricsSnapshot(
        int LookbackDays,
        RetentionReadModelResponse Retention,
        AttendanceReadModelResponse Attendance,
        VolunteerGrowthReadModelResponse VolunteerGrowth,
        LeadershipFunnelReadModelResponse LeadershipFunnel);

    private sealed record MentorSnapshot(
        Guid MentorProfileId,
        Guid MemberId,
        int AvailabilityHoursPerMonth,
        string[] ExpertiseAreas);
}
