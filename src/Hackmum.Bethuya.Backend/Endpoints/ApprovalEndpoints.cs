using Hackmum.Bethuya.Agents.Workflows;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Repositories;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class ApprovalEndpoints
{
    public static void MapApprovalEndpoints(this WebApplication app)
    {
        // Every route declares its own policy: the global authenticated fallback is opt-in and must not be
        // the only guard. Reads mirror curation decision visibility (organizers and curators); decision
        // changes are restricted to organizers.
        var group = app.MapGroup("/api/approvals")
            .WithTags("Approvals")
            .RequireAuthorization(BethuyaPolicyNames.RequireOrganizerOrCurator);

        group.MapGet("/pending", async (IDecisionRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetPendingAsync(ct)));

        group.MapGet("/{entityType}/{entityId:guid}", async (
            string entityType, Guid entityId, IDecisionRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetByEntityAsync(entityType, entityId, ct)));

        group.MapPost("/{id:guid}/approve", async (
            Guid id,
            ApproveRequest request,
            IDecisionRepository repo,
            ApprovalWorkflow workflow,
            CancellationToken ct) =>
        {
            var decision = await repo.GetByIdAsync(id, ct);
            if (decision is null) return Results.NotFound();

            workflow.Approve(decision, request.Reason);
            await repo.UpdateAsync(decision, ct);
            return Results.Ok(decision);
        }).RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);

        group.MapPost("/{id:guid}/reject", async (
            Guid id,
            RejectRequest request,
            IDecisionRepository repo,
            ApprovalWorkflow workflow,
            CancellationToken ct) =>
        {
            var decision = await repo.GetByIdAsync(id, ct);
            if (decision is null) return Results.NotFound();

            workflow.Reject(decision, request.Reason);
            await repo.UpdateAsync(decision, ct);
            return Results.Ok(decision);
        }).RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);
    }
}
