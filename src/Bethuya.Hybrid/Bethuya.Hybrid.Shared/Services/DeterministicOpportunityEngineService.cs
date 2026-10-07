using Bethuya.Hybrid.Shared.Models.OpportunityEngine;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>
/// Deterministic Opportunity Engine workspace used until live Participation Ledger /
/// Community Passport / Community Graph providers replace this module.
/// </summary>
public sealed class DeterministicOpportunityEngineService : IOpportunityEngineService
{
    /// <inheritdoc />
    public Task<OpportunityEngineWorkspace> GetWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BuildWorkspace());
    }

    private static OpportunityEngineWorkspace BuildWorkspace()
    {
        var maya = BuildMayaOpportunity();
        var priyaLead = BuildPriyaMentorshipOpportunity();
        var anish = BuildAnishOpportunity();
        var david = BuildDavidOpportunity();
        var connect = BuildConnectOpportunity();
        var priyaKeynote = BuildPriyaSummitKeynoteOpportunity();
        var rohanVolunteerLead = BuildRohanSummitVolunteerLeadOpportunity();
        var mayaMentorshipHost = BuildMayaSummitMentorshipHostOpportunity();

        return new OpportunityEngineWorkspace(
            Kpis:
            [
                new("Open Opportunities", "24", "Active pool"),
                new("Awaiting Review", "7", "Priority", IsPriority: true),
                new("Average Time To Fill", "3.2 Days", "Operational velocity"),
                new("Completed Opportunities", "41", "Outcomes logged"),
                new("Champion Candidates", "13", "In pipeline")
            ],
            Events:
            [
                new FeaturedEventNeeds(
                    EventId: "evt-cnd-2026",
                    Title: "Cloud Native Day 2026",
                    DateLabel: "Oct 24, 2026",
                    LocationLabel: "Bengaluru Main Track",
                    NeedsSummary: ["1 Workshop Speaker", "2 Volunteers", "1 Registration Lead", "1 Panel Moderator"],
                    OpenRoleCount: 4,
                    MatchCount: 11,
                    Needs:
                    [
                        new StaffingNeed(
                            NeedId: "need-workshop",
                            Role: "Workshop Speaker",
                            BestMatchMemberId: "mem-maya",
                            BestMatchName: "Maya Fernandes",
                            BestMatchDetail: "Staff Platform Eng • 4.9 Session Rating",
                            BestMatchInitials: "MF",
                            MatchCount: 4,
                            LinkedOpportunityId: maya.OpportunityId),
                        new StaffingNeed(
                            NeedId: "need-reg",
                            Role: "Registration Lead",
                            BestMatchMemberId: "mem-anish",
                            BestMatchName: "Anish Kulkarni",
                            BestMatchDetail: "Operations Volunteer • 8 meetups",
                            BestMatchInitials: "AK",
                            MatchCount: 2,
                            LinkedOpportunityId: anish.OpportunityId),
                        new StaffingNeed(
                            NeedId: "need-moderator",
                            Role: "Panel Moderator",
                            BestMatchMemberId: "mem-priya",
                            BestMatchName: "Priya Menon",
                            BestMatchDetail: "Staff Eng • 18m Tenure",
                            BestMatchInitials: "PM",
                            MatchCount: 2,
                            LinkedOpportunityId: priyaLead.OpportunityId),
                        new StaffingNeed(
                            NeedId: "need-logistics",
                            Role: "Logistics Lead",
                            BestMatchMemberId: "mem-david",
                            BestMatchName: "David Chen",
                            BestMatchDetail: "DevOps Practitioner • Docker Day '25",
                            BestMatchInitials: "DC",
                            MatchCount: 3,
                            LinkedOpportunityId: david.OpportunityId)
                    ]),
                new FeaturedEventNeeds(
                    EventId: "evt-aioss-2026",
                    Title: "AI Open Source Summit",
                    DateLabel: "Nov 12, 2026",
                    LocationLabel: "Hybrid Track",
                    NeedsSummary: ["1 Keynote Facilitator", "1 Volunteer Lead", "1 Mentorship Host"],
                    OpenRoleCount: 3,
                    MatchCount: 6,
                    Needs:
                    [
                        new StaffingNeed(
                            NeedId: "need-ai-keynote",
                            Role: "Keynote Facilitator",
                            BestMatchMemberId: "mem-priya",
                            BestMatchName: "Priya Menon",
                            BestMatchDetail: "Staff Eng • Strong facilitation evidence",
                            BestMatchInitials: "PM",
                            MatchCount: 2,
                            LinkedOpportunityId: priyaKeynote.OpportunityId),
                        new StaffingNeed(
                            NeedId: "need-ai-volunteer-lead",
                            Role: "Volunteer Lead",
                            BestMatchMemberId: "mem-rohan",
                            BestMatchName: "Rohan Mehta",
                            BestMatchDetail: "Frequent Event Volunteer • Shift coverage",
                            BestMatchInitials: "RM",
                            MatchCount: 2,
                            LinkedOpportunityId: rohanVolunteerLead.OpportunityId),
                        new StaffingNeed(
                            NeedId: "need-ai-mentorship-host",
                            Role: "Mentorship Host",
                            BestMatchMemberId: "mem-maya",
                            BestMatchName: "Maya Fernandes",
                            BestMatchDetail: "Session lead • Mentorship interest",
                            BestMatchInitials: "MF",
                            MatchCount: 2,
                            LinkedOpportunityId: mayaMentorshipHost.OpportunityId)
                    ])
            ],
            Opportunities: [maya, priyaLead, anish, david, connect, priyaKeynote, rohanVolunteerLead, mayaMentorshipHost],
            Risks:
            [
                new RiskIntervention(
                    RiskId: "risk-alex-burnout",
                    Title: "Organizer Burnout Risk",
                    MemberName: "Alex Kim",
                    MemberInitials: "AK",
                    MemberRole: "Active Chapter Organizer",
                    Signals:
                    [
                        "Volunteer load increasing (+60%)",
                        "Attendance decreasing",
                        "Managing multiple chapters"
                    ],
                    CareOwner: "Unassigned"),
                new RiskIntervention(
                    RiskId: "risk-volunteer-fatigue",
                    Title: "Volunteer Fatigue",
                    MemberName: "Rohan Mehta",
                    MemberInitials: "RM",
                    MemberRole: "Frequent Event Volunteer",
                    Signals:
                    [
                        "Four consecutive weekend shifts",
                        "Skipped last two social meetups"
                    ],
                    CareOwner: "Unassigned"),
                new RiskIntervention(
                    RiskId: "risk-retention",
                    Title: "Member Retention Risk",
                    MemberName: "Farah Siddiqui",
                    MemberInitials: "FS",
                    MemberRole: "Emerging Contributor",
                    Signals:
                    [
                        "No attendance in 45 days",
                        "Previously high engagement"
                    ],
                    CareOwner: "Unassigned")
            ],
            Champions: new ChampionPipeline(
                CandidateCount: 13,
                UnderReviewCount: 4,
                ReadyForNominationCount: 3,
                RecognitionPendingCount: 1,
                Candidates:
                [
                    new ChampionCandidate(
                        CandidateId: "champ-priya",
                        MemberName: "Priya Menon",
                        MemberInitials: "PM",
                        StatusLabel: "Ready For Nomination",
                        Evidence:
                        [
                            "9 Events Attended",
                            "5 Volunteer Contributions",
                            "3 Members Mentored",
                            "18 Month Community Tenure"
                        ])
                ]),
            Pathways:
            [
                new ProgressionPathway("Volunteer", "Organizer", Identified: 24, Approved: 10, Accepted: 6, Active: 3),
                new ProgressionPathway("Speaker", "Mentor", Identified: 14, Approved: 9, Accepted: 9, Active: 4),
                new ProgressionPathway("Mentor", "Champion", Identified: 13, Approved: 4, Accepted: 2, Active: 2)
            ]);
    }

    private static MemberOpportunity BuildMayaOpportunity() =>
        new(
            OpportunityId: "opp-maya-workshop",
            MemberId: "mem-maya",
            MemberName: "Maya Fernandes",
            MemberInitials: "MF",
            OpportunityTitle: "Workshop Speaker",
            Category: OpportunityCategory.Share,
            EvidenceStrength: EvidenceStrength.Strong,
            Receipts: ["12 Events Attended", "Led 2 Sessions", "Strong Community Network"],
            CurrentPathway: "Member → Volunteer → Speaker",
            PathwayJourney:
            [
                "Joined Community",
                "Attended Events",
                "Volunteered",
                "Led Session",
                "Mentored Members",
                "Recommended Speaker"
            ],
            Status: OpportunityWorkflowStatus.UnderReview,
            NeedContext: new CommunityNeedContext(
                EventTitle: "Cloud Native Day 2026",
                Role: "Workshop Speaker",
                RequiredBy: "Oct 24, 2026"),
            WhyExists:
            [
                "Attended 12 community events",
                "Led 2 previous sessions",
                "Positive attendee feedback",
                "Strong Community Graph relationships"
            ],
            EvidenceSources: ["Participation Ledger", "Community Passport", "Community Graph"],
            Graph: new CommunityGraphSnapshot(28, 12, 8, "Cloud Native"),
            StatusDetail: "Needs Review");

    private static MemberOpportunity BuildPriyaMentorshipOpportunity() =>
        new(
            OpportunityId: "opp-priya-lead",
            MemberId: "mem-priya",
            MemberName: "Priya Menon",
            MemberInitials: "PM",
            OpportunityTitle: "Mentorship Circle Lead",
            Category: OpportunityCategory.Lead,
            EvidenceStrength: EvidenceStrength.Strong,
            Receipts: ["Mentored 3 members", "Consistent attendance", "Active community relationships"],
            CurrentPathway: "Volunteer → Mentor → Lead",
            PathwayJourney:
            [
                "Joined Community",
                "Attended Events",
                "Volunteered",
                "Mentored Members",
                "Recommended Lead"
            ],
            Status: OpportunityWorkflowStatus.UnderReview,
            NeedContext: new CommunityNeedContext(
                EventTitle: "Cloud Native Day 2026",
                Role: "Panel Moderator",
                RequiredBy: "Oct 24, 2026"),
            WhyExists:
            [
                "Mentored 3 members",
                "18 month community tenure",
                "Strong facilitation evidence"
            ],
            EvidenceSources: ["Participation Ledger", "Community Passport", "Community Graph"],
            Graph: new CommunityGraphSnapshot(34, 15, 11, "Mentorship"),
            StatusDetail: "Under Review • Mentorship track");

    private static MemberOpportunity BuildAnishOpportunity() =>
        new(
            OpportunityId: "opp-anish-reg",
            MemberId: "mem-anish",
            MemberName: "Anish Kulkarni",
            MemberInitials: "AK",
            OpportunityTitle: "Registration Lead",
            Category: OpportunityCategory.Contribute,
            EvidenceStrength: EvidenceStrength.Strong,
            Receipts: ["8 Meetups staffed", "Reliable check-in coverage", "Operations passport verified"],
            CurrentPathway: "Member → Volunteer → Operations Lead",
            PathwayJourney:
            [
                "Joined Community",
                "Attended Events",
                "Volunteered",
                "Recommended Operations Lead"
            ],
            Status: OpportunityWorkflowStatus.Suggested,
            NeedContext: new CommunityNeedContext(
                EventTitle: "Cloud Native Day 2026",
                Role: "Registration Lead",
                RequiredBy: "Oct 24, 2026"),
            WhyExists:
            [
                "Eight meetup staffing shifts",
                "Positive organizer feedback",
                "Availability aligned to event day"
            ],
            EvidenceSources: ["Participation Ledger", "Community Passport", "Community Graph"],
            Graph: new CommunityGraphSnapshot(19, 6, 14, "Event Operations"),
            StatusDetail: "Suggested • Operations track");

    private static MemberOpportunity BuildDavidOpportunity() =>
        new(
            OpportunityId: "opp-david-crew",
            MemberId: "mem-david",
            MemberName: "David Chen",
            MemberInitials: "DC",
            OpportunityTitle: "Event Crew Lead",
            Category: OpportunityCategory.Contribute,
            EvidenceStrength: EvidenceStrength.Strong,
            Receipts: ["6 Local meetups attended", "Expressed logistics interest", "Verified .NET/K8s passport"],
            CurrentPathway: "Member → Event Volunteer",
            PathwayJourney:
            [
                "Joined Community",
                "Attended Events",
                "Volunteered",
                "Recommended Logistics Lead"
            ],
            Status: OpportunityWorkflowStatus.Suggested,
            NeedContext: new CommunityNeedContext(
                EventTitle: "Cloud Native Day 2026",
                Role: "Logistics Lead",
                RequiredBy: "Oct 24, 2026"),
            WhyExists:
            [
                "Six meetup attendance receipts",
                "Self-reported logistics interest",
                "Passport skills match event track"
            ],
            EvidenceSources: ["Participation Ledger", "Community Passport", "Community Graph"],
            Graph: new CommunityGraphSnapshot(22, 5, 9, "Cloud Native"),
            StatusDetail: "Suggested • Event Lead track");

    private static MemberOpportunity BuildConnectOpportunity() =>
        new(
            OpportunityId: "opp-connect-sarah-david",
            MemberId: "mem-sarah-david",
            MemberName: "Sarah Jenkins & David L.",
            MemberInitials: "SD",
            OpportunityTitle: "Mentor Match Introduction",
            Category: OpportunityCategory.Connect,
            EvidenceStrength: EvidenceStrength.Strong,
            Receipts: ["Shared Go/Backend interest", "Complementary experience levels"],
            CurrentPathway: "1:1 Intro Match",
            PathwayJourney:
            [
                "Joined Community",
                "Attended Events",
                "Expressed Mentorship Interest",
                "Recommended Connection"
            ],
            Status: OpportunityWorkflowStatus.Suggested,
            NeedContext: new CommunityNeedContext(
                EventTitle: "Community Mentorship Program",
                Role: "Mentor Match",
                RequiredBy: "Nov 1, 2026"),
            WhyExists:
            [
                "Shared technical interests",
                "Complementary experience levels",
                "Both opted into mentorship discovery"
            ],
            EvidenceSources: ["Participation Ledger", "Community Passport", "Community Graph"],
            Graph: new CommunityGraphSnapshot(16, 4, 3, "Backend"),
            StatusDetail: "Ready • 1:1 Intro Match");

    private static MemberOpportunity BuildPriyaSummitKeynoteOpportunity() =>
        new(
            OpportunityId: "opp-priya-summit-keynote",
            MemberId: "mem-priya",
            MemberName: "Priya Menon",
            MemberInitials: "PM",
            OpportunityTitle: "Keynote Facilitator",
            Category: OpportunityCategory.Lead,
            EvidenceStrength: EvidenceStrength.Strong,
            Receipts: ["Facilitated 3 community panels", "18 month tenure", "Positive attendee feedback"],
            CurrentPathway: "Volunteer → Mentor → Facilitator",
            PathwayJourney:
            [
                "Joined Community",
                "Attended Events",
                "Volunteered",
                "Mentored Members",
                "Recommended Facilitator"
            ],
            Status: OpportunityWorkflowStatus.Suggested,
            NeedContext: new CommunityNeedContext(
                EventTitle: "AI Open Source Summit",
                Role: "Keynote Facilitator",
                RequiredBy: "Nov 12, 2026"),
            WhyExists:
            [
                "Prior panel facilitation receipts",
                "Strong mentorship track record",
                "Availability aligned to summit date"
            ],
            EvidenceSources: ["Participation Ledger", "Community Passport", "Community Graph"],
            Graph: new CommunityGraphSnapshot(34, 15, 11, "AI / Open Source"),
            StatusDetail: "Suggested • Summit facilitation track");

    private static MemberOpportunity BuildRohanSummitVolunteerLeadOpportunity() =>
        new(
            OpportunityId: "opp-rohan-summit-volunteer",
            MemberId: "mem-rohan",
            MemberName: "Rohan Mehta",
            MemberInitials: "RM",
            OpportunityTitle: "Volunteer Lead",
            Category: OpportunityCategory.Contribute,
            EvidenceStrength: EvidenceStrength.Moderate,
            Receipts: ["Four recent volunteer shifts", "Reliable day-of coverage", "Operations interest"],
            CurrentPathway: "Member → Volunteer → Volunteer Lead",
            PathwayJourney:
            [
                "Joined Community",
                "Attended Events",
                "Volunteered",
                "Recommended Volunteer Lead"
            ],
            Status: OpportunityWorkflowStatus.Suggested,
            NeedContext: new CommunityNeedContext(
                EventTitle: "AI Open Source Summit",
                Role: "Volunteer Lead",
                RequiredBy: "Nov 12, 2026"),
            WhyExists:
            [
                "Repeated volunteer shift receipts",
                "Organizer feedback on reliability",
                "Self-reported interest in leading crew"
            ],
            EvidenceSources: ["Participation Ledger", "Community Passport", "Community Graph"],
            Graph: new CommunityGraphSnapshot(21, 7, 12, "Event Operations"),
            StatusDetail: "Suggested • Summit volunteer track");

    private static MemberOpportunity BuildMayaSummitMentorshipHostOpportunity() =>
        new(
            OpportunityId: "opp-maya-summit-mentorship",
            MemberId: "mem-maya",
            MemberName: "Maya Fernandes",
            MemberInitials: "MF",
            OpportunityTitle: "Mentorship Host",
            Category: OpportunityCategory.Share,
            EvidenceStrength: EvidenceStrength.Strong,
            Receipts: ["Led 2 sessions", "Mentored first-time attendees", "Strong session ratings"],
            CurrentPathway: "Volunteer → Speaker → Mentorship Host",
            PathwayJourney:
            [
                "Joined Community",
                "Attended Events",
                "Volunteered",
                "Led Session",
                "Recommended Mentorship Host"
            ],
            Status: OpportunityWorkflowStatus.Suggested,
            NeedContext: new CommunityNeedContext(
                EventTitle: "AI Open Source Summit",
                Role: "Mentorship Host",
                RequiredBy: "Nov 12, 2026"),
            WhyExists:
            [
                "Session leadership receipts",
                "Mentorship interest in passport",
                "Community Graph support for newcomers"
            ],
            EvidenceSources: ["Participation Ledger", "Community Passport", "Community Graph"],
            Graph: new CommunityGraphSnapshot(28, 12, 8, "AI / Open Source"),
            StatusDetail: "Suggested • Summit mentorship track");
}
