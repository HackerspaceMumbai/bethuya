using Refit;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>
/// Refit client for planning cycle orchestration APIs.
/// </summary>
public interface IPlanningCycleApi
{
    [Get("/api/planning-cycles/events/{eventId}/active")]
    Task<PlanningCycleDto> GetActiveCycleAsync(Guid eventId, CancellationToken ct = default);

    [Post("/api/planning-cycles/events/{eventId}/start")]
    Task<PlanningCycleDto> StartCycleAsync(Guid eventId, [Body] StartPlanningCycleDto request, CancellationToken ct = default);

    [Post("/api/planning-cycles/{cycleId}/draft")]
    Task<PlanningCycleDto> GenerateDraftAsync(Guid cycleId, [Body] GeneratePlannerDraftDto request, CancellationToken ct = default);

    [Post("/api/planning-cycles/{cycleId}/approve")]
    Task<PlanningCycleDto> ApproveDraftAsync(Guid cycleId, [Body] ApprovePlannerDraftDto request, CancellationToken ct = default);

    [Post("/api/planning-cycles/{cycleId}/publish")]
    Task<PublishedScheduleDto> PublishAsync(Guid cycleId, [Body] PublishPlanningCycleDto request, CancellationToken ct = default);
}

public sealed record StartPlanningCycleDto(
    bool ForceNewCycle = false,
    string? RequestedBy = null);

public sealed record GeneratePlannerDraftDto(
    string WorkItemId,
    string? Constraints = null,
    string? PriorEventsContext = null,
    string? HumanEditedMarkdown = null,
    string? RequestedBy = null);

public sealed record ApprovePlannerDraftDto(
    Guid DraftId,
    string EditedMarkdown,
    string ApprovedBy);

public sealed record PublishPlanningCycleDto(
    Guid DraftId,
    string PublishedBy);

public sealed record PlanningCycleDto(
    Guid CycleId,
    Guid EventId,
    string ConversationId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    PlannerDraftDto? ActiveDraft);

public sealed record PlannerDraftDto(
    Guid DraftId,
    string WorkItemId,
    string MarkdownAgenda,
    PlanningAgendaJsonDto AgendaJson,
    bool IsApproved,
    string? HumanEditedMarkdown,
    string? HumanDiff,
    DateTimeOffset CreatedAt,
    string? AgentVersionTag);

public sealed record PublishedScheduleDto(
    Guid SnapshotId,
    Guid EventId,
    Guid CycleId,
    Guid DraftId,
    string MarkdownAgenda,
    PlanningAgendaJsonDto AgendaJson,
    DateTimeOffset PublishedAt,
    string PublishedBy);

public sealed class PlanningAgendaJsonDto
{
    public required string AgendaVersion { get; set; }
    public required PlanningAgendaEventDto Event { get; set; }
    public List<string> Objectives { get; set; } = [];
    public List<string> Constraints { get; set; } = [];
    public required PlanningAgendaBodyDto Agenda { get; set; }
    public required PlanningAgendaRationaleDto Rationale { get; set; }
    public required PlanningAgendaRisksDto Risks { get; set; }
    public required PlanningAgendaNextActionsDto NextActions { get; set; }
}

public sealed class PlanningAgendaEventDto
{
    public required string EventId { get; set; }
    public required string Title { get; set; }
    public required string Date { get; set; }
    public required string Timezone { get; set; }
    public string? Location { get; set; }
}

public sealed class PlanningAgendaBodyDto
{
    public double TotalDurationMinutes { get; set; }
    public List<PlanningAgendaBlockDto> Blocks { get; set; } = [];
}

public sealed class PlanningAgendaBlockDto
{
    public required string BlockId { get; set; }
    public required string Start { get; set; }
    public required string End { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Format { get; set; }
    public List<PlanningAgendaSpeakerDto> Speakers { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}

public sealed class PlanningAgendaSpeakerDto
{
    public required string Name { get; set; }
    public string? Role { get; set; }
}

public sealed class PlanningAgendaRationaleDto
{
    public List<string> KeyTradeoffs { get; set; } = [];
    public List<string> InclusionNotes { get; set; } = [];
}

public sealed class PlanningAgendaRisksDto
{
    public List<PlanningAgendaRiskDto> Items { get; set; } = [];
}

public sealed class PlanningAgendaRiskDto
{
    public required string Risk { get; set; }
    public required string Mitigation { get; set; }
}

public sealed class PlanningAgendaNextActionsDto
{
    public List<PlanningAgendaActionDto> Items { get; set; } = [];
}

public sealed class PlanningAgendaActionDto
{
    public required string Owner { get; set; }
    public required string Action { get; set; }
}

