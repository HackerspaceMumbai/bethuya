using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using ServiceDefaults.Auth;
using System.Security.Claims;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class PlanningCycleEndpoints
{
    private const int MaxWorkItemIdLength = 100;

    public static void MapPlanningCycleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/planning-cycles")
            .WithTags("PlanningCycles")
            .RequireRateLimiting(RateLimitPolicies.Ai);

        group.MapGet("/events/{eventId:guid}/active", async (
            Guid eventId,
            PlanningCycleService service,
            CancellationToken ct) =>
        {
            var cycle = await service.GetActiveCycleAsync(eventId, ct);
            return cycle is null ? Results.NotFound() : Results.Ok(cycle);
        }).RequireAuthorization(BethuyaPolicyNames.RequireAttendee);

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
        }).RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);

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
        }).RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);

        group.MapPost("/{cycleId:guid}/approve", async (
            Guid cycleId,
            ApprovePlannerDraftRequest request,
            ClaimsPrincipal user,
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

            var actor = user.GetSubject()?.UserId;
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.ApproveDraftAsync(cycleId, request with { ApprovedBy = actor }, ct));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);

        group.MapPost("/{cycleId:guid}/publish", async (
            Guid cycleId,
            PublishPlanningCycleRequest request,
            ClaimsPrincipal user,
            PlanningCycleService service,
            CancellationToken ct) =>
        {
            var actor = user.GetSubject()?.UserId;
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.PublishAsync(cycleId, request with { PublishedBy = actor }, ct));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);
    }
}

