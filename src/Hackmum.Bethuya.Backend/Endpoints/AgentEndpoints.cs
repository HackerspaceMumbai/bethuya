using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.AI.CopilotSdk;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Microsoft.Extensions.Hosting;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/agents")
            .WithTags("Agents")
            .RequireRateLimiting(RateLimitPolicies.Ai);

        group.MapPost("/planner/{eventId:guid}", async (
            Guid eventId,
            InvokePlannerRequest request,
            IAgent<PlannerRequest, PlannerResponse> planner,
            IEventRepository eventRepo,
            CancellationToken ct) =>
        {
            var evt = await eventRepo.GetByIdAsync(eventId, ct);
            if (evt is null) return Results.NotFound("Event not found");

            var agentRequest = new PlannerRequest(
                Event: evt,
                Constraints: request.Constraints,
                PriorEventsContext: request.PriorEventsContext,
                RequestedBy: request.RequestedBy);

            var response = await planner.DraftAsync(agentRequest, ct);
            return Results.Ok(response);
        });

        group.MapPost("/curator/{eventId:guid}", async (
            Guid eventId,
            InvokeCuratorRequest request,
            IAgent<CuratorRequest, CuratorResponse> curator,
            IEventRepository eventRepo,
            IRegistrationRepository regRepo,
            CancellationToken ct) =>
        {
            var evt = await eventRepo.GetByIdAsync(eventId, ct);
            if (evt is null) return Results.NotFound("Event not found");

            var registrations = await regRepo.GetByEventIdAsync(eventId, ct);

            var budget = new FairnessBudget
            {
                EventId = eventId,
                DiversityTargets = request.DiversityTargets,
                EquityPrompts = request.EquityPrompts
            };

            var agentRequest = new CuratorRequest(
                Event: evt,
                Registrations: registrations,
                Budget: budget,
                RequestedBy: request.RequestedBy);

            var response = await curator.DraftAsync(agentRequest, ct);
            return Results.Ok(response);
        });

        group.MapPost("/facilitator/{eventId:guid}", async (
            Guid eventId,
            InvokeFacilitatorRequest request,
            IAgent<FacilitatorRequest, FacilitatorResponse> facilitator,
            IEventRepository eventRepo,
            CancellationToken ct) =>
        {
            var evt = await eventRepo.GetByIdAsync(eventId, ct);
            if (evt is null) return Results.NotFound("Event not found");
            if (evt.Agenda is null) return Results.BadRequest("Event has no agenda");

            var agentRequest = new FacilitatorRequest(
                Event: evt,
                Agenda: evt.Agenda,
                CurrentSessionTitle: request.CurrentSessionTitle,
                RequestedBy: request.RequestedBy);

            var response = await facilitator.DraftAsync(agentRequest, ct);
            return Results.Ok(response);
        });

        group.MapPost("/reporter/{eventId:guid}", async (
            Guid eventId,
            InvokeReporterRequest request,
            IAgent<ReporterRequest, ReporterResponse> reporter,
            IEventRepository eventRepo,
            CancellationToken ct) =>
        {
            var evt = await eventRepo.GetByIdAsync(eventId, ct);
            if (evt is null) return Results.NotFound("Event not found");

            var agentRequest = new ReporterRequest(
                Event: evt,
                SessionNotes: request.SessionNotes,
                RequestedBy: request.RequestedBy);

            var response = await reporter.DraftAsync(agentRequest, ct);
            return Results.Ok(response);
        });

        group.MapPost("/recommend-dates", async (
            RecommendDatesApiRequest request,
            IDateRecommendationService dateService,
            CancellationToken ct) =>
        {
            var context = new DateRecommendationContext(
                Title: request.Title,
                Type: request.Type,
                Description: request.Description,
                Location: request.Location,
                Capacity: request.Capacity);

            var recommendation = await dateService.RecommendAsync(context, ct);

            return Results.Ok(new RecommendDatesApiResponse(
                StartDate: recommendation.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                StartTime: recommendation.StartTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                EndDate: recommendation.EndDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                EndTime: recommendation.EndTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                Reasoning: recommendation.Reasoning));
        });
    }
}
