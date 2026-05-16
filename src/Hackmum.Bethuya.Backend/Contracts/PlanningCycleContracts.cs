using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Backend.Contracts;

public sealed record StartPlanningCycleRequest(
    bool ForceNewCycle = false,
    string? RequestedBy = null);

public sealed record GeneratePlannerDraftRequest(
    string WorkItemId,
    string? Constraints = null,
    string? PriorEventsContext = null,
    string? HumanEditedMarkdown = null,
    string? RequestedBy = null);

public sealed record ApprovePlannerDraftRequest(
    Guid DraftId,
    string EditedMarkdown,
    string ApprovedBy);

public sealed record PublishPlanningCycleRequest(
    Guid DraftId,
    string PublishedBy);

public sealed record PlanningCycleResponse(
    Guid CycleId,
    Guid EventId,
    string ConversationId,
    PlanningCycleStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    PlannerDraftResponse? ActiveDraft);

public sealed record PlannerDraftResponse(
    Guid DraftId,
    string WorkItemId,
    string MarkdownAgenda,
    PlanningAgendaJson AgendaJson,
    bool IsApproved,
    string? HumanEditedMarkdown,
    string? HumanDiff,
    DateTimeOffset CreatedAt,
    string? AgentVersionTag);

public sealed record PublishedScheduleResponse(
    Guid SnapshotId,
    Guid EventId,
    Guid CycleId,
    Guid DraftId,
    string MarkdownAgenda,
    PlanningAgendaJson AgendaJson,
    DateTimeOffset PublishedAt,
    string PublishedBy);

