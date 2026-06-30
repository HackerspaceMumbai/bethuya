using Hackmum.Bethuya.Agents.Workflows;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Repositories;
using ServiceDefaults.Auth;
using ServiceDefaults.Auth.Observability;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class ApprovalEndpoints
{
    public static void MapApprovalEndpoints(this WebApplication app)
    {
        MapApprovalRoutes(app.MapGroup("/api/admin/approvals")
            .WithTags("Approvals")
            .RequireAuthorization(BethuyaPolicyNames.RequireAdmin));
        MapApprovalRoutes(app.MapGroup("/api/approvals")
            .WithTags("Approvals")
            .RequireAuthorization(BethuyaPolicyNames.RequireAdmin));
    }

    private static void MapApprovalRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/pending", static async (IDecisionRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetPendingAsync(ct)));

        group.MapGet("/{entityType}/{entityId:guid}", static async (
            string entityType, Guid entityId, IDecisionRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetByEntityAsync(entityType, entityId, ct)));

        group.MapPost("/{id:guid}/approve", static async (
            Guid id,
            ApproveRequest request,
            IDecisionRepository repo,
            ApprovalWorkflow workflow,
            IUserContext userContext,
            IAuthorizationAuditor auditor,
            HttpContext http,
            CancellationToken ct) =>
        {
            var decision = await repo.GetByIdAsync(id, ct);
            if (decision is null) return Results.NotFound();

            workflow.Approve(decision, request.Reason);
            await repo.UpdateAsync(decision, ct);

            // Admin acted on a pending decision. Recorded with the admin's non-PII subject hash only —
            // the route already enforces RequireAdmin, so this is observability, not a new gate.
            auditor.RecordDecision(
                AuthorizationDecision.Allow,
                BethuyaPolicyNames.RequireAdmin,
                BethuyaAuditResourceTypes.Approval,
                subject: userContext.UserId,
                resourceId: id.ToString(),
                route: http.Request.Path.Value,
                outcomeStatusCode: StatusCodes.Status200OK);

            return Results.Ok(decision);
        });

        group.MapPost("/{id:guid}/reject", static async (
            Guid id,
            RejectRequest request,
            IDecisionRepository repo,
            ApprovalWorkflow workflow,
            IUserContext userContext,
            IAuthorizationAuditor auditor,
            HttpContext http,
            CancellationToken ct) =>
        {
            var decision = await repo.GetByIdAsync(id, ct);
            if (decision is null) return Results.NotFound();

            workflow.Reject(decision, request.Reason);
            await repo.UpdateAsync(decision, ct);

            // Admin acted on a pending decision. Recorded with the admin's non-PII subject hash only —
            // the route already enforces RequireAdmin, so this is observability, not a new gate.
            auditor.RecordDecision(
                AuthorizationDecision.Allow,
                BethuyaPolicyNames.RequireAdmin,
                BethuyaAuditResourceTypes.Approval,
                subject: userContext.UserId,
                resourceId: id.ToString(),
                route: http.Request.Path.Value,
                outcomeStatusCode: StatusCodes.Status200OK);

            return Results.Ok(decision);
        });
    }
}
