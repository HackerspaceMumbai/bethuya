using Hackmum.Bethuya.Agents.Workflows;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Repositories;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class ApprovalEndpoints
{
    public static void MapApprovalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/approvals").WithTags("Approvals");

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
        });

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
        });
    }
}
