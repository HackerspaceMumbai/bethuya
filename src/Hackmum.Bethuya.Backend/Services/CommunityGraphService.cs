using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>Read-only graph projection. Relationships can only originate in verified ledger records.</summary>
public sealed class CommunityGraphService(BethuyaDbContext db)
{
    /// <summary>Reads a bounded graph within the caller's community and member privacy permissions.</summary>
    public async Task<CommunityGraphSnapshot> ReadAsync(string userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var viewer = await db.CommunityMembers.AsNoTracking().SingleOrDefaultAsync(m => m.UserId == userId, ct);
        if (viewer is null)
            return new([], [], [], [], now, false);

        // Organizer-only profiles stay out of this member-facing exploration surface, even for organizers.
        var members = await db.CommunityMembers.AsNoTracking()
            .Where(m => m.CommunitySlug == viewer.CommunitySlug
                && (m.Id == viewer.Id || (m.IsDiscoverableToCommunity
                    && m.ShareParticipationWithOrganizers && m.Visibility != ProfileVisibilityScope.OrganizerOnly)))
            .OrderByDescending(m => m.Id == viewer.Id).ThenBy(m => m.DisplayName).ThenBy(m => m.UserId)
            .Take(81).ToListAsync(ct);
        var truncated = members.Count > 80;
        members = members.Take(80).ToList();
        var memberIds = members.Select(m => m.Id).ToArray();
        var entries = await db.ParticipationLedgerEntries.AsNoTracking()
            .Where(e => memberIds.Contains(e.CommunityMemberId) && e.IsVerified && e.OccurredAt <= now)
            .OrderByDescending(e => e.OccurredAt).ThenBy(e => e.ProvenanceKey)
            .Take(401).ToListAsync(ct);
        truncated |= entries.Count > 400;
        entries = entries.Take(400).ToList();
        var eventIds = entries.Where(e => e.EventId.HasValue).Select(e => e.EventId!.Value).Distinct().ToArray();
        var events = await db.Events.AsNoTracking().Where(e => eventIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Title, ct);
        var membersById = members.ToDictionary(m => m.Id);
        Dictionary<GraphNodeId, CommunityGraphNode> nodes = [];
        Dictionary<(GraphNodeId Source, GraphNodeId Target, string Kind), List<CommunityGraphEvidence>> ties = [];

        foreach (var entry in entries)
        {
            var member = membersById[entry.CommunityMemberId];
            var relation = Relation(entry.Activity);
            if (relation is null || string.IsNullOrWhiteSpace(entry.Evidence)) continue;
            var target = ResolveTarget(entry, member, membersById, events);
            if (target is null) continue;
            var source = MemberNode(member);
            if (source.Id == target.Id) continue;
            nodes[source.Id] = source;
            nodes[target.Id] = target;
            var key = (source.Id, target.Id, relation);
            if (!ties.TryGetValue(key, out var evidence)) ties[key] = evidence = [];
            evidence.Add(Proof(entry));
        }

        // Shared attendance explains proximity; it never implies mentorship or collaboration.
        var shared = ties.Where(t => t.Key.Kind == "Attended").GroupBy(t => t.Key.Target).ToList();
        var peerTies = 0;
        foreach (var group in shared)
        {
            var attendees = group.OrderBy(t => t.Key.Source.Value, StringComparer.Ordinal).ToArray();
            for (var first = 0; first < attendees.Length; first++)
            for (var second = first + 1; second < attendees.Length; second++)
            {
                var key = (attendees[first].Key.Source, attendees[second].Key.Source, "Shared attendance");
                if (!ties.TryGetValue(key, out var proof))
                {
                    if (peerTies >= 200) { truncated = true; continue; }
                    ties[key] = proof = [];
                    peerTies++;
                }
                proof.AddRange(attendees[first].Value.Concat(attendees[second].Value));
            }
        }

        List<CommunityGraphOpportunity> opportunities = [];
        foreach (var member in members)
        {
            var memberId = MemberNode(member).Id;
            var speaking = ties.Where(t => t.Key.Source == memberId && t.Key.Kind == "Spoke at").ToArray();
            if (speaking.Length >= 2)
                AddOpportunity("speaker", "Workshop Speaker", $"Delivered sessions at {speaking.Length} distinct events.", speaking.SelectMany(t => t.Value).ToList());
            var contributions = ties.Where(t => t.Key.Source == memberId && t.Key.Kind == "Contributes to")
                .SelectMany(t => t.Value).DistinctBy(p => p.EntryId).ToList();
            if (contributions.Count >= 2)
                AddOpportunity("reviewer", "Project Reviewer", $"{contributions.Count} verified project contributions.", contributions);
            var mentoring = ties.Where(t => t.Key.Source == memberId && t.Key.Kind == "Mentors").ToArray();
            if (mentoring.Length >= 2)
                AddOpportunity("mentor", "Mentorship Circle Lead", $"Mentored {mentoring.Length} community members.", mentoring.SelectMany(t => t.Value).ToList());

            void AddOpportunity(string key, string title, string reason, List<CommunityGraphEvidence> proof)
            {
                var id = GraphNodeId.From($"opportunity:{member.Id.Value}:{key}");
                opportunities.Add(new(id, memberId, title, reason, proof));
                nodes[id] = new(id, "Opportunity", title, "Suggested pathway · Human review required");
                ties[(memberId, id, "Recommended for")] = proof;
            }
        }

        var joins = entries.Where(e => e.Activity == ParticipationActivityKind.JoinedCommunity).ToList();
        var currentJoins = joins.Where(e => e.OccurredAt >= now.AddDays(-30)).Select(e => e.CommunityMemberId).Distinct().Count();
        var previousJoins = joins.Where(e => e.OccurredAt >= now.AddDays(-60) && e.OccurredAt < now.AddDays(-30))
            .Select(e => e.CommunityMemberId).Distinct().Count();
        List<CommunityGraphHealth> health =
        [
            new("Participation Trends", $"{entries.Count} verified records in this snapshot · {nodes.Values.Count(n => n.Kind == "Member")} participating members"),
            new(currentJoins > previousJoins ? "Growing Community" : "Community Participation",
                $"{currentJoins} members joined in the last 30 days · {previousJoins} in the preceding 30 days")
        ];
        return new(nodes.Values.ToList(), ties.Select(t => new CommunityGraphRelationship(t.Key.Source,
            t.Key.Target, t.Key.Kind, t.Value.DistinctBy(p => p.EntryId).OrderByDescending(p => p.OccurredAt).ToList())).ToList(),
            opportunities, health, now, truncated);
    }

    private static CommunityGraphNode? ResolveTarget(ParticipationLedgerEntry entry, CommunityMember member,
        Dictionary<CommunityMemberId, CommunityMember> members, Dictionary<Guid, string> events)
    {
        if (entry.Activity == ParticipationActivityKind.Mentored)
        {
            if (entry.TargetKind != "Member" || !Guid.TryParse(entry.TargetKey, out var id) || id == Guid.Empty) return null;
            return members.TryGetValue(CommunityMemberId.From(id), out var targetMember) ? MemberNode(targetMember) : null;
        }
        if (entry.Activity is ParticipationActivityKind.Attended or ParticipationActivityKind.Spoke or ParticipationActivityKind.Volunteered)
        {
            if (entry.EventId is Guid eventId && events.TryGetValue(eventId, out var title))
                return new(GraphNodeId.From($"event:{eventId}"), "Event", title, "Verified event participation");
            if (entry.TargetKind != "Event") return null;
        }
        if (entry.Activity == ParticipationActivityKind.JoinedCommunity && entry.TargetKind is null)
            return new(GraphNodeId.From($"community:{member.CommunitySlug}"), "Community", member.CommunitySlug, "Community membership");
        var expectedKind = entry.Activity switch
        {
            ParticipationActivityKind.JoinedCommunity => "Community",
            ParticipationActivityKind.JoinedChapter => "Chapter",
            ParticipationActivityKind.ContributedProject => "Project",
            ParticipationActivityKind.UsedTechnology => "Technology",
            _ => "Event"
        };
        if (entry.TargetKind != expectedKind || string.IsNullOrWhiteSpace(entry.TargetKey) || string.IsNullOrWhiteSpace(entry.TargetLabel)) return null;
        // External IDs are connector-scoped to avoid merging unrelated projects/events with the same local key.
        return new(GraphNodeId.From($"{expectedKind}:{entry.Connector}:{entry.TargetKey}"), expectedKind,
            entry.TargetLabel, $"{expectedKind} · {entry.Connector} ledger evidence");
    }

    private static CommunityGraphNode MemberNode(CommunityMember member) =>
        new(GraphNodeId.From($"member:{member.Id.Value}"), "Member", member.DisplayName,
            string.Join(" · ", new[] { member.OccupationStatus, member.CompanyName }.Where(s => !string.IsNullOrWhiteSpace(s))));

    private static CommunityGraphEvidence Proof(ParticipationLedgerEntry entry) =>
        new(entry.Id, entry.Evidence, entry.Activity.ToString(), entry.Connector.ToString(), entry.OccurredAt,
            GraphNodeId.From($"member:{entry.CommunityMemberId.Value}"));

    private static string? Relation(ParticipationActivityKind activity) => activity switch
    {
        ParticipationActivityKind.Attended => "Attended",
        ParticipationActivityKind.Volunteered => "Volunteered",
        ParticipationActivityKind.Spoke => "Spoke at",
        ParticipationActivityKind.ContributedProject => "Contributes to",
        ParticipationActivityKind.JoinedCommunity => "Community member",
        ParticipationActivityKind.JoinedChapter => "Chapter member",
        ParticipationActivityKind.Mentored => "Mentors",
        ParticipationActivityKind.UsedTechnology => "Uses technology",
        _ => null
    };
}
