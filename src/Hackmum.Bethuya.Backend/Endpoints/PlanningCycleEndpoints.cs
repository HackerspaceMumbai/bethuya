using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using ServiceDefaults.Auth;

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
            PlanningCycleService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.EditedMarkdown))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.EditedMarkdown)] = ["editedMarkdown is required."]
                });
            }

            // M2 (PR3): authoritative approver identity comes from the validated principal; the
            // request-body value is tolerated only as a fallback for callers not yet updated.
            var approvedBy = ResolveDecider(userContext) ?? request.ApprovedBy;
            if (string.IsNullOrWhiteSpace(approvedBy))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.ApprovedBy)] = ["approvedBy is required."]
                });
            }

            request = request with { ApprovedBy = approvedBy };

            try
            {
                return Results.Ok(await service.ApproveDraftAsync(cycleId, request, ct));
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
            PlanningCycleService service,
            CancellationToken ct) =>
        {
            // M2 (PR3): authoritative publisher identity comes from the validated principal; the
            // request-body value is tolerated only as a fallback for callers not yet updated.
            var publishedBy = ResolveDecider(userContext) ?? request.PublishedBy;
            if (string.IsNullOrWhiteSpace(publishedBy))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.PublishedBy)] = ["publishedBy is required."]
                });
            }

            request = request with { PublishedBy = publishedBy };

            try
            {
                return Results.Ok(await service.PublishAsync(cycleId, request, ct));
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

