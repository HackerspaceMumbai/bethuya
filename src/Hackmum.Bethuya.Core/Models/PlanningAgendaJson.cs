namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Structured sidecar agenda payload produced by Planner.
/// </summary>
public sealed class PlanningAgendaJson
{
    public required string AgendaVersion { get; set; }
    public required PlanningAgendaEvent Event { get; set; }
    public List<string> Objectives { get; set; } = [];
    public List<string> Constraints { get; set; } = [];
    public required PlanningAgendaBody Agenda { get; set; }
    public required PlanningAgendaRationale Rationale { get; set; }
    public required PlanningAgendaRisks Risks { get; set; }
    public required PlanningAgendaNextActions NextActions { get; set; }
}

/// <summary>
/// Event metadata for the structured agenda.
/// </summary>
public sealed class PlanningAgendaEvent
{
    public required string EventId { get; set; }
    public required string Title { get; set; }
    public required string Date { get; set; }
    public required string Timezone { get; set; }
    public string? Location { get; set; }
}

/// <summary>
/// Timeline body for the structured agenda.
/// </summary>
public sealed class PlanningAgendaBody
{
    public double TotalDurationMinutes { get; set; }
    public List<PlanningAgendaBlock> Blocks { get; set; } = [];
}

/// <summary>
/// Single timeline block in the structured agenda.
/// </summary>
public sealed class PlanningAgendaBlock
{
    public required string BlockId { get; set; }
    public required string Start { get; set; }
    public required string End { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Format { get; set; }
    public List<PlanningAgendaSpeaker> Speakers { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// Speaker descriptor for an agenda block.
/// </summary>
public sealed class PlanningAgendaSpeaker
{
    public required string Name { get; set; }
    public string? Role { get; set; }
}

/// <summary>
/// Non-sensitive rationale and tradeoff notes from planner.
/// </summary>
public sealed class PlanningAgendaRationale
{
    public List<string> KeyTradeoffs { get; set; } = [];
    public List<string> InclusionNotes { get; set; } = [];
}

/// <summary>
/// Risk list for an agenda.
/// </summary>
public sealed class PlanningAgendaRisks
{
    public List<PlanningAgendaRiskItem> Items { get; set; } = [];
}

/// <summary>
/// Single risk entry.
/// </summary>
public sealed class PlanningAgendaRiskItem
{
    public required string Risk { get; set; }
    public required string Mitigation { get; set; }
}

/// <summary>
/// Suggested next actions tied to the agenda.
/// </summary>
public sealed class PlanningAgendaNextActions
{
    public List<PlanningAgendaActionItem> Items { get; set; } = [];
}

/// <summary>
/// Single actionable next step.
/// </summary>
public sealed class PlanningAgendaActionItem
{
    public required string Owner { get; set; }
    public required string Action { get; set; }
}

