namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// Canonical participation signal types persisted to the unified member ledger.
/// </summary>
public enum ParticipationActivityKind
{
    Registered,
    Waitlisted,
    Attended,
    Volunteered,
    SubmittedSession,
    JoinedCommunity,
    MessageEngaged,
    Other,
    /// <summary>Verified delivery of a session.</summary>
    Spoke,
    /// <summary>Verified project contribution.</summary>
    ContributedProject,
    /// <summary>Verified chapter membership.</summary>
    JoinedChapter,
    /// <summary>Verified mentorship of the target member.</summary>
    Mentored,
    /// <summary>Verified use of a technology during participation.</summary>
    UsedTechnology
}
