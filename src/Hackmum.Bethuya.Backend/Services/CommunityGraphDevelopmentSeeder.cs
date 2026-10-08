using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>Explicit development-only fixtures for graph visual and interaction testing.</summary>
public sealed class CommunityGraphDevelopmentSeeder(BethuyaDbContext db, IHostEnvironment environment)
{
    /// <summary>Seeds repeatable fictional participation. Never available in production.</summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!environment.IsDevelopment()) throw new InvalidOperationException("Graph fixtures are Development-only.");
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var priya = await MemberAsync("priya", "Priya Menon", "Staff Platform Engineer", ct);
            var david = await MemberAsync("david", "David L.", "Active mentee · Volunteer", ct);
            var sarah = await MemberAsync("sarah", "Sarah J.", "Go Developer", ct);
            var maya = await MemberAsync("maya", "Maya F.", "Community contributor", ct);
            await db.SaveChangesAsync(ct);
            var now = DateTimeOffset.UtcNow;
            List<ParticipationLedgerEntry> records = [];
            Add(priya, "membership", ParticipationActivityKind.JoinedCommunity, "Community", "hackerspace-mumbai", "Hackerspace Mumbai", "Confirmed Hackerspace Mumbai community membership");
            Add(priya, "chapter", ParticipationActivityKind.JoinedChapter, "Chapter", "bengaluru", "Bengaluru Chapter", "Verified Bengaluru Chapter membership");
            Add(priya, "cloud-talk", ParticipationActivityKind.Spoke, "Event", "cloud-native", "Cloud Native Day", "Delivered a platform engineering workshop at Cloud Native Day");
            Add(priya, "design-talk", ParticipationActivityKind.Spoke, "Event", "design-open", "Design & Architecture in the Open", "Delivered a session on open architecture practices");
            Add(priya, "review-142", ParticipationActivityKind.ContributedProject, "Project", "portal", "Community Portal", "Reviewed PR #142 in Community Portal");
            Add(priya, "review-156", ParticipationActivityKind.ContributedProject, "Project", "portal", "Community Portal", "Reviewed PR #156 in Community Portal");
            Add(priya, "mentor-david", ParticipationActivityKind.Mentored, "Member", david.Id.Value.ToString(), david.DisplayName, "Confirmed mentorship with David L. through the community mentoring programme");
            Add(priya, "mentor-sarah", ParticipationActivityKind.Mentored, "Member", sarah.Id.Value.ToString(), sarah.DisplayName, "Confirmed mentorship with Sarah J. through the community mentoring programme");
            Add(priya, "volunteer", ParticipationActivityKind.Volunteered, "Event", "cloud-native", "Cloud Native Day", "Verified volunteer shift supporting Cloud Native Day");
            Add(david, "cloud-attend", ParticipationActivityKind.Attended, "Event", "cloud-native", "Cloud Native Day", "Checked in at Cloud Native Day");
            Add(sarah, "cloud-attend", ParticipationActivityKind.Attended, "Event", "cloud-native", "Cloud Native Day", "Checked in at Cloud Native Day");
            Add(maya, "docs", ParticipationActivityKind.ContributedProject, "Project", "docs", "Docs Initiative", "Published the contributor onboarding guide");
            Add(maya, "portal", ParticipationActivityKind.ContributedProject, "Project", "portal", "Community Portal", "Contributed an accessibility fix to Community Portal");
            Add(david, "chapter", ParticipationActivityKind.JoinedChapter, "Chapter", "bengaluru", "Bengaluru Chapter", "Verified Bengaluru Chapter membership");
            Add(priya, "technology", ParticipationActivityKind.UsedTechnology, "Technology", "kubernetes", "Kubernetes", "Used Kubernetes in the Cloud Native Day workshop");
            var keys = records.Select(r => r.ProvenanceKey).ToArray();
            var existing = await db.ParticipationLedgerEntries.Where(e => keys.Contains(e.ProvenanceKey))
                .Select(e => e.ProvenanceKey).ToListAsync(ct);
            db.ParticipationLedgerEntries.AddRange(records.Where(r => !existing.Contains(r.ProvenanceKey, StringComparer.Ordinal)));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            void Add(CommunityMember member, string key, ParticipationActivityKind activity, string kind, string target, string label, string evidence)
                => records.Add(new()
                {
                    CommunityMemberId = member.Id, Connector = ParticipationConnectorKind.Forms,
                    ExternalMemberKey = member.UserId, Activity = activity, IsVerified = true,
                    TargetKind = kind, TargetKey = target, TargetLabel = label,
                    Evidence = evidence, ProvenanceKey = $"graph-demo-v1:{member.UserId}:{key}",
                    OccurredAt = now.AddDays(-10 - records.Count), SourceCorrelationId = "fictional-development-fixture"
                });
        });
    }

    private async Task<CommunityMember> MemberAsync(string key, string name, string occupation, CancellationToken ct)
    {
        var subject = $"graph-demo-v1:{key}";
        var member = await db.CommunityMembers.SingleOrDefaultAsync(m => m.UserId == subject, ct);
        if (member is not null) return member;
        member = new() { UserId = subject, DisplayName = name, Email = $"{key}@graph-fixture.invalid", OccupationStatus = occupation };
        db.CommunityMembers.Add(member);
        return member;
    }
}
