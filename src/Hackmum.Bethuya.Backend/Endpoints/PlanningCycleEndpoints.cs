using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using ServiceDefaults.Auth;
using ServiceDefaults.Auth.Observability;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class PlanningCycleEndpoints
{
    private const int MaxWorkItemIdLength = 100;

    public static void MapPlanningCycleEndpoints(this WebApplication app)
    {
        MapPlanningCycleRoutes(app.MapGroup("/api/organizer/planning-cycles")
            .WithTags("PlanningCycles")
            .RequireRateLimiting(RateLimitPolicies.Ai)
            .RequireAuthorization(BethuyaPolicyNames.RequireOrganizer));
        MapPlanningCycleRoutes(app.MapGroup("/api/planning-cycles")
            .WithTags("PlanningCycles")
            .RequireRateLimiting(RateLimitPolicies.Ai)
            .RequireAuthorization(BethuyaPolicyNames.RequireOrganizer));
    }

    private static void MapPlanningCycleRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/events/{eventId:guid}/active", static async (
            Guid eventId,
            PlanningCycleService service,
            CancellationToken ct) =>
        {
            var cycle = await service.GetActiveCycleAsync(eventId, ct);
            return cycle is null ? Results.NotFound() : Results.Ok(cycle);
        });

        group.MapPost("/events/{eventId:guid}/start", async (
            Guid eventId,
            StartPlanningCycleRequest request,
            PlanningCycleService service,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await service.StartCycleAsync(eventId, request, ct));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        group.MapPost("/{cycleId:guid}/draft", async (
            Guid cycleId,
            GeneratePlannerDraftRequest request,
            PlanningCycleService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.WorkItemId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.WorkItemId)] = ["workItemId is required for idempotency."]
                });
            }

            if (request.WorkItemId.Length > MaxWorkItemIdLength)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.WorkItemId)] = [$"workItemId must be {MaxWorkItemIdLength} characters or fewer."]
                });
            }

            try
            {
                return Results.Ok(await service.GenerateDraftAsync(cycleId, request, ct));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        group.MapPost("/{cycleId:guid}/approve", async (
            Guid cycleId,
            ApprovePlannerDraftRequest request,
            IUserContext userContext,
            IAuthorizationAuditor auditor,
            PlanningCycleService service,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.EditedMarkdown))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.EditedMarkdown)] = ["editedMarkdown is required."]
                });
            }

            // M2 (PR3): the approver identity is the validated principal, never request-body input.
            // The body value is overwritten below and is not trusted for audit attribution.
            var approvedBy = ResolveDecider(userContext);
            if (string.IsNullOrWhiteSpace(approvedBy))
            {
                auditor.RecordDecision(
                    AuthorizationDecision.Deny,
                    BethuyaPolicyNames.RequireOrganizer,
                    BethuyaAuditResourceTypes.PlanningCycle,
                    subject: userContext.UserId,
                    resourceId: cycleId.ToString(),
                    route: http.Request.Path.Value,
                    outcomeStatusCode: StatusCodes.Status401Unauthorized);
                return Results.Unauthorized();
            }

            request = request with { ApprovedBy = approvedBy };

            try
            {
                var result = await service.ApproveDraftAsync(cycleId, request, ct);

                // Decider-identity resolved: record the approval against the organizer's non-PII subject
                // hash (NOT approvedBy / Email / Name).
                auditor.RecordDecision(
                    AuthorizationDecision.Allow,
                    BethuyaPolicyNames.RequireOrganizer,
                    BethuyaAuditResourceTypes.PlanningCycle,
                    subject: userContext.UserId,
                    resourceId: cycleId.ToString(),
                    route: http.Request.Path.Value,
                    outcomeStatusCode: StatusCodes.Status200OK);

                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        group.MapPost("/{cycleId:guid}/publish", async (
            Guid cycleId,
            PublishPlanningCycleRequest request,
            IUserContext userContext,
            IAuthorizationAuditor auditor,
            PlanningCycleService service,
            HttpContext http,
            CancellationToken ct) =>
        {
            // M2 (PR3): the publisher identity is the validated principal, never request-body input.
            // The body value is overwritten below and is not trusted for audit attribution.
            var publishedBy = ResolveDecider(userContext);
            if (string.IsNullOrWhiteSpace(publishedBy))
            {
                auditor.RecordDecision(
                    AuthorizationDecision.Deny,
                    BethuyaPolicyNames.RequireOrganizer,
                    BethuyaAuditResourceTypes.PlanningCycle,
                    subject: userContext.UserId,
                    resourceId: cycleId.ToString(),
                    route: http.Request.Path.Value,
                    outcomeStatusCode: StatusCodes.Status401Unauthorized);
                return Results.Unauthorized();
            }

            request = request with { PublishedBy = publishedBy };

            try
            {
                var result = await service.PublishAsync(cycleId, request, ct);

                // Decider-identity resolved: record the publish against the organizer's non-PII subject
                // hash (NOT publishedBy / Email / Name).
                auditor.RecordDecision(
                    AuthorizationDecision.Allow,
                    BethuyaPolicyNames.RequireOrganizer,
                    BethuyaAuditResourceTypes.PlanningCycle,
                    subject: userContext.UserId,
                    resourceId: cycleId.ToString(),
                    route: http.Request.Path.Value,
                    outcomeStatusCode: StatusCodes.Status200OK);

                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });
    }

    /// <summary>Resolves the decider identity from the validated principal, or <c>null</c> when anonymous.</summary>
    private static string? ResolveDecider(IUserContext userContext) =>
        userContext.Email ?? userContext.Name ?? userContext.UserId;
}

