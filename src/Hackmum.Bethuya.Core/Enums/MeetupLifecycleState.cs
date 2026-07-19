namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// Operational lifecycle states for a community meetup.
/// </summary>
public enum MeetupLifecycleState
{
    Drafted,
    VenueLocked,
    CfpOpen,
    CfpExtended,
    ReviewAndPlanning,
    AgendaApproved,
    Published,
    ScheduleAltered,
    Delayed,
    Completed,
    Archived
}
