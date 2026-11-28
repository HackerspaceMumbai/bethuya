using Hackmum.Bethuya.Agents.Workflows;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Repositories;
using ServiceDefaults.Auth;
using System.Security.Claims;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class ApprovalEndpoints
{
    public static void MapApprovalEndpoints(this WebApplication app)
    {
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
            ClaimsPrincipal user,
            IDecisionRepository repo,
            ApprovalWorkflow workflow,
            CancellationToken ct) =>
        {
            var actor = GetActor(user);
            if (actor is null) return Results.Unauthorized();

            var decision = await repo.GetByIdAsync(id, ct);
            if (decision is null) return Results.NotFound();

            workflow.Approve(decision, request.Reason, actor);
            await repo.UpdateAsync(decision, ct);
            return Results.Ok(decision);
        });

        group.MapPost("/{id:guid}/reject", async (
            Guid id,
            RejectRequest request,
            ClaimsPrincipal user,
            IDecisionRepository repo,
            ApprovalWorkflow workflow,
            CancellationToken ct) =>
        {
            var actor = GetActor(user);
            if (actor is null) return Results.Unauthorized();

            var decision = await repo.GetByIdAsync(id, ct);
            if (decision is null) return Results.NotFound();

            workflow.Reject(decision, request.Reason, actor);
            await repo.UpdateAsync(decision, ct);
            return Results.Ok(decision);
        });
    }

    private static string? GetActor(ClaimsPrincipal user)
    {
        var subject = user.GetSubject();
        return subject is null ? null : subject.Email ?? subject.DisplayName ?? subject.UserId;
    }
}
