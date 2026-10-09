namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// Evidence-backed ways a member contributes to a community.
/// </summary>
public enum CommunitySignalKind
{
    Builder,
    Connector,
    Mentor,
    Volunteer,
    Speaker,
    Organizer,
    Champion,
    Maintainer,
    KnowledgeSharer,
    CommunitySteward
}

/// <summary>
/// Supported member-curated portfolio link types.
/// </summary>
public enum PortfolioLinkKind
{
    Link,
    Repository,
    BlogPost,
    Presentation,
    Video,
    ProjectShowcase
}

/// <summary>
/// Member opportunity categories surfaced in the passport.
/// </summary>
public enum MemberOpportunityKind
{
    Volunteer,
    Mentorship,
    Speaker,
    Organizer,
    Leadership,
    ProjectCollaboration,
    Learning,
    Other
}

/// <summary>
/// Lifecycle states for an opportunity offered to a member.
/// </summary>
public enum MemberOpportunityStatus
{
    Offered,
    Accepted,
    Active,
    Participated,
    Completed,
    Declined,
    Expired
}

/// <summary>
/// Contextual relationship types used by the Phase 1 passport.
/// </summary>
public enum CommunityRelationshipKind
{
    Mentor,
    Mentee,
    Collaborator,
    CoAttendee,
    Volunteer,
    Project,
    Speaker
}
