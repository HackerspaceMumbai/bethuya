namespace Bethuya.Hybrid.Shared.Models.OpportunityEngine;

/// <summary>Workspace tab identifiers for the Opportunity Engine operational canvas.</summary>
public enum OpportunityWorkspaceTab
{
    CommunityNeeds,
    MemberOpportunities,
    ChampionPipeline,
    RisksInterventions
}

/// <summary>Opportunity category filters for the member opportunity queue.</summary>
public enum OpportunityCategory
{
    All,
    Contribute,
    Lead,
    Share,
    Connect,
    Recognize
}

/// <summary>Evidence strength derived from verified participation receipts.</summary>
public enum EvidenceStrength
{
    Emerging,
    Moderate,
    Strong
}

/// <summary>Human-reviewed opportunity workflow states.</summary>
public enum OpportunityWorkflowStatus
{
    Suggested,
    UnderReview,
    Approved,
    Offered,
    Accepted,
    Completed,
    Dismissed,
    Declined,
    Expired
}

/// <summary>Display-ready Opportunity Engine workspace projection.</summary>
public sealed record OpportunityEngineWorkspace(
    IReadOnlyList<OpportunityKpi> Kpis,
    IReadOnlyList<FeaturedEventNeeds> Events,
    IReadOnlyList<MemberOpportunity> Opportunities,
    IReadOnlyList<RiskIntervention> Risks,
    ChampionPipeline Champions,
    IReadOnlyList<ProgressionPathway> Pathways);

/// <summary>Operational KPI card.</summary>
public sealed record OpportunityKpi(
    string Label,
    string Value,
    string Detail,
    bool IsPriority = false);

/// <summary>Event staffing needs for a featured community event.</summary>
public sealed record FeaturedEventNeeds(
    string EventId,
    string Title,
    string DateLabel,
    string LocationLabel,
    IReadOnlyList<string> NeedsSummary,
    int OpenRoleCount,
    int MatchCount,
    IReadOnlyList<StaffingNeed> Needs);

/// <summary>Lightweight staffing card for a community need.</summary>
public sealed record StaffingNeed(
    string NeedId,
    string Role,
    string BestMatchMemberId,
    string BestMatchName,
    string BestMatchDetail,
    string BestMatchInitials,
    int MatchCount,
    string LinkedOpportunityId);

/// <summary>Member opportunity triage card (no approval actions).</summary>
public sealed record MemberOpportunity(
    string OpportunityId,
    string MemberId,
    string MemberName,
    string MemberInitials,
    string OpportunityTitle,
    OpportunityCategory Category,
    EvidenceStrength EvidenceStrength,
    IReadOnlyList<string> Receipts,
    string CurrentPathway,
    IReadOnlyList<string> PathwayJourney,
    OpportunityWorkflowStatus Status,
    CommunityNeedContext NeedContext,
    IReadOnlyList<string> WhyExists,
    IReadOnlyList<string> EvidenceSources,
    CommunityGraphSnapshot Graph,
    string StatusDetail);

/// <summary>Need context shown in the selected opportunity panel.</summary>
public sealed record CommunityNeedContext(
    string EventTitle,
    string Role,
    string RequiredBy);

/// <summary>Compact Community Graph snapshot for an opportunity.</summary>
public sealed record CommunityGraphSnapshot(
    int ConnectedMembers,
    int SupportsNewMembers,
    int CollaboratesWithVolunteers,
    string PrimaryCluster);

/// <summary>Isolated care-queue intervention (not an opportunity workflow).</summary>
public sealed record RiskIntervention(
    string RiskId,
    string Title,
    string MemberName,
    string MemberInitials,
    string MemberRole,
    IReadOnlyList<string> Signals,
    string CareOwner,
    bool IsDeferred = false);

/// <summary>Champion pipeline summary and candidates.</summary>
public sealed record ChampionPipeline(
    int CandidateCount,
    int UnderReviewCount,
    int ReadyForNominationCount,
    int RecognitionPendingCount,
    IReadOnlyList<ChampionCandidate> Candidates);

/// <summary>Champion recognition candidate.</summary>
public sealed record ChampionCandidate(
    string CandidateId,
    string MemberName,
    string MemberInitials,
    string StatusLabel,
    IReadOnlyList<string> Evidence,
    bool NominationApproved = false);

/// <summary>Lightweight community progression pathway outcome.</summary>
public sealed record ProgressionPathway(
    string FromRole,
    string ToRole,
    int Identified,
    int Approved,
    int Accepted,
    int Active);
