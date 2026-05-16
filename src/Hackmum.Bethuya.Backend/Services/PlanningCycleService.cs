using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hackmum.Bethuya.Backend.Agents;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Planning;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Coordinates planning-cycle lifecycle, planner invocations, approvals, and publishing.
/// </summary>
public sealed class PlanningCycleService(BethuyaDbContext db, IAgentInvoker plannerInvoker)
{
    public async Task<PlanningCycleResponse?> GetActiveCycleAsync(Guid eventId, CancellationToken ct = default)
    {
        var cycle = await db.PlanningCycles
            .Where(c => c.EventId == eventId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (cycle is null)
        {
            return null;
        }

        PlannerDraft? draft = null;
        if (cycle.ActiveDraftId.HasValue)
        {
            draft = await db.PlannerDrafts.FirstOrDefaultAsync(d => d.Id == cycle.ActiveDraftId.Value, ct);
        }

        return ToCycleResponse(cycle, draft);
    }

    public async Task<PlanningCycleResponse> StartCycleAsync(Guid eventId, StartPlanningCycleRequest request, CancellationToken ct = default)
    {
        var evt = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw new KeyNotFoundException("Event not found.");

        var latestCycle = await db.PlanningCycles
            .Where(c => c.EventId == eventId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (latestCycle is not null && latestCycle.Status != PlanningCycleStatus.Published)
        {
            if (request.ForceNewCycle)
            {
                throw new InvalidOperationException("Cannot create a new cycle while an active cycle is still open. Publish the current cycle first.");
            }

            PlannerDraft? currentDraft = null;
            if (latestCycle.ActiveDraftId.HasValue)
            {
                currentDraft = await db.PlannerDrafts.FirstOrDefaultAsync(d => d.Id == latestCycle.ActiveDraftId.Value, ct);
            }

            return ToCycleResponse(latestCycle, currentDraft);
        }

        var cycle = new PlanningCycle
        {
            EventId = evt.Id,
            ConversationId = $"pc_{Guid.CreateVersion7():N}",
            Status = PlanningCycleStatus.Drafting
        };

        db.PlanningCycles.Add(cycle);
        await db.SaveChangesAsync(ct);

        return ToCycleResponse(cycle, draft: null);
    }

    public async Task<PlanningCycleResponse> GenerateDraftAsync(Guid cycleId, GeneratePlannerDraftRequest request, CancellationToken ct = default)
    {
        var cycle = await db.PlanningCycles.FirstOrDefaultAsync(c => c.Id == cycleId, ct)
            ?? throw new KeyNotFoundException("Planning cycle not found.");

        if (cycle.Status == PlanningCycleStatus.Published)
        {
            throw new InvalidOperationException("This cycle is already published and locked. Start a new cycle to make changes.");
        }

        var evt = await db.Events.FirstOrDefaultAsync(e => e.Id == cycle.EventId, ct)
            ?? throw new KeyNotFoundException("Event not found for planning cycle.");

        var existingDraft = await db.PlannerDrafts
            .Where(d => d.PlanningCycleId == cycleId && d.WorkItemId == request.WorkItemId)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (existingDraft is not null)
        {
            return ToCycleResponse(cycle, existingDraft);
        }

        var invocationInput = new PlannerInvocationInput(
            EventId: evt.Id,
            Title: evt.Title,
            Date: evt.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            Timezone: "Asia/Kolkata",
            Location: evt.Location,
            Capacity: evt.Capacity,
            Constraints: request.Constraints,
            PriorEventsContext: request.PriorEventsContext,
            HumanEditedMarkdown: request.HumanEditedMarkdown);

        var inputHash = ComputeInputHash(invocationInput);
        var traceParent = Activity.Current?.Id;
        var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.CreateVersion7().ToString("N");
        var invocation = await plannerInvoker.InvokePlannerAsync(
            invocationInput,
            cycle.ConversationId,
            request.WorkItemId,
            traceParent,
            correlationId,
            ct);

        var draft = new PlannerDraft
        {
            PlanningCycleId = cycle.Id,
            WorkItemId = request.WorkItemId,
            InputHash = inputHash,
            MarkdownAgenda = invocation.MarkdownAgenda,
            AgendaJson = JsonSerializer.Serialize(invocation.AgendaJson),
            ResponseId = invocation.ResponseId,
            AgentName = invocation.AgentName,
            AgentVersionTag = invocation.AgentVersionTag,
            TraceParent = traceParent,
            CorrelationId = correlationId
        };

        var audit = new PlannerInvocationAudit
        {
            PlanningCycleId = cycle.Id,
            EventId = cycle.EventId,
            WorkItemId = request.WorkItemId,
            ConversationId = cycle.ConversationId,
            InputHash = inputHash,
            ResponseId = invocation.ResponseId,
            AgentName = invocation.AgentName,
            AgentVersionTag = invocation.AgentVersionTag,
            MarkdownAgenda = invocation.MarkdownAgenda,
            AgendaJson = JsonSerializer.Serialize(invocation.AgendaJson),
            TraceParent = traceParent,
            CorrelationId = correlationId
        };

        db.PlannerDrafts.Add(draft);
        db.PlannerInvocationAudits.Add(audit);

        cycle.ActiveDraftId = draft.Id;
        cycle.Status = PlanningCycleStatus.Drafting;
        cycle.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToCycleResponse(cycle, draft);
    }

    public async Task<PlanningCycleResponse> ApproveDraftAsync(Guid cycleId, ApprovePlannerDraftRequest request, CancellationToken ct = default)
    {
        var cycle = await db.PlanningCycles.FirstOrDefaultAsync(c => c.Id == cycleId, ct)
            ?? throw new KeyNotFoundException("Planning cycle not found.");

        if (cycle.Status == PlanningCycleStatus.Published)
        {
            throw new InvalidOperationException("This cycle is already published and locked. Start a new cycle to make changes.");
        }

        var draft = await db.PlannerDrafts.FirstOrDefaultAsync(d => d.Id == request.DraftId && d.PlanningCycleId == cycleId, ct)
            ?? throw new KeyNotFoundException("Planner draft not found.");

        var agendaJson = JsonSerializer.Deserialize<PlanningAgendaJson>(draft.AgendaJson)
            ?? throw new InvalidOperationException("Stored agenda_json is invalid and cannot be approved.");

        var reconciled = PlanningAgendaValidator.ReconcileFromMarkdown(agendaJson, request.EditedMarkdown);
        var validationErrors = PlanningAgendaValidator.Validate(reconciled);
        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException($"Edited markdown could not be reconciled to valid agenda_json: {string.Join("; ", validationErrors)}");
        }

        var consistencyErrors = PlanningAgendaValidator.ValidateMarkdownConsistency(reconciled, request.EditedMarkdown);
        if (consistencyErrors.Count > 0)
        {
            throw new InvalidOperationException($"Edited markdown is inconsistent with agenda_json: {string.Join("; ", consistencyErrors)}");
        }

        var previousMarkdown = draft.MarkdownAgenda;
        draft.MarkdownAgenda = request.EditedMarkdown;
        draft.AgendaJson = JsonSerializer.Serialize(reconciled);
        draft.HumanEditedMarkdown = request.EditedMarkdown;
        draft.HumanDiff = BuildLineDiffSummary(previousMarkdown, request.EditedMarkdown);
        draft.IsApproved = true;
        draft.ApprovalDecision = $"Approved by {request.ApprovedBy}";
        draft.UpdatedAt = DateTimeOffset.UtcNow;

        cycle.ActiveDraftId = draft.Id;
        cycle.Status = PlanningCycleStatus.ReadyToPublish;
        cycle.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToCycleResponse(cycle, draft);
    }

    public async Task<PublishedScheduleResponse> PublishAsync(Guid cycleId, PublishPlanningCycleRequest request, CancellationToken ct = default)
    {
        var cycle = await db.PlanningCycles.FirstOrDefaultAsync(c => c.Id == cycleId, ct)
            ?? throw new KeyNotFoundException("Planning cycle not found.");

        if (cycle.Status == PlanningCycleStatus.Published)
        {
            throw new InvalidOperationException("Planning cycle is already published.");
        }

        var draft = await db.PlannerDrafts.FirstOrDefaultAsync(d => d.Id == request.DraftId && d.PlanningCycleId == cycleId, ct)
            ?? throw new KeyNotFoundException("Planner draft not found.");

        if (!draft.IsApproved || cycle.Status != PlanningCycleStatus.ReadyToPublish)
        {
            throw new InvalidOperationException("Only approved drafts in ReadyToPublish cycles can be published.");
        }

        var snapshot = new PublishedScheduleSnapshot
        {
            EventId = cycle.EventId,
            PlanningCycleId = cycle.Id,
            PlannerDraftId = draft.Id,
            MarkdownAgenda = draft.MarkdownAgenda,
            AgendaJson = draft.AgendaJson,
            PublishedBy = request.PublishedBy,
            AgentVersionTag = draft.AgentVersionTag
        };

        db.PublishedScheduleSnapshots.Add(snapshot);

        cycle.Status = PlanningCycleStatus.Published;
        cycle.PublishedAt = DateTimeOffset.UtcNow;
        cycle.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return new PublishedScheduleResponse(
            SnapshotId: snapshot.Id,
            EventId: snapshot.EventId,
            CycleId: snapshot.PlanningCycleId,
            DraftId: snapshot.PlannerDraftId,
            MarkdownAgenda: snapshot.MarkdownAgenda,
            AgendaJson: JsonSerializer.Deserialize<PlanningAgendaJson>(snapshot.AgendaJson)!,
            PublishedAt: snapshot.PublishedAt,
            PublishedBy: snapshot.PublishedBy);
    }

    private static string ComputeInputHash(PlannerInvocationInput input)
    {
        var payload = JsonSerializer.Serialize(input);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private static PlanningCycleResponse ToCycleResponse(PlanningCycle cycle, PlannerDraft? draft) =>
        new(
            CycleId: cycle.Id,
            EventId: cycle.EventId,
            ConversationId: cycle.ConversationId,
            Status: cycle.Status,
            CreatedAt: cycle.CreatedAt,
            UpdatedAt: cycle.UpdatedAt,
            PublishedAt: cycle.PublishedAt,
            ActiveDraft: draft is null
                ? null
                : new PlannerDraftResponse(
                    DraftId: draft.Id,
                    WorkItemId: draft.WorkItemId,
                    MarkdownAgenda: draft.MarkdownAgenda,
                    AgendaJson: JsonSerializer.Deserialize<PlanningAgendaJson>(draft.AgendaJson)!,
                    IsApproved: draft.IsApproved,
                    HumanEditedMarkdown: draft.HumanEditedMarkdown,
                    HumanDiff: draft.HumanDiff,
                    CreatedAt: draft.CreatedAt,
                    AgentVersionTag: draft.AgentVersionTag));

    private static string BuildLineDiffSummary(string beforeMarkdown, string afterMarkdown)
    {
        var beforeLines = beforeMarkdown.Split('\n', StringSplitOptions.TrimEntries);
        var afterLines = afterMarkdown.Split('\n', StringSplitOptions.TrimEntries);

        var removed = beforeLines.Except(afterLines, StringComparer.Ordinal).Count();
        var added = afterLines.Except(beforeLines, StringComparer.Ordinal).Count();
        return $"Markdown changes: +{added} / -{removed} lines.";
    }
}

