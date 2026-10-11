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
    Mentored,
    Spoke,
    Organized,
    ProjectContributed,
    ContentCreated,
    Maintained,
    Moderated,
    LedProgram,
    /// <summary>Verified project contribution from graph ingestion.</summary>
    ContributedProject,
    /// <summary>Verified chapter membership.</summary>
    JoinedChapter,
    /// <summary>Verified use of a technology during participation.</summary>
    UsedTechnology
}
