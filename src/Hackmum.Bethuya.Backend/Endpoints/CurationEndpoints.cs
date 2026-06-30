using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Repositories;
using ServiceDefaults.Auth;
using System.Security.Claims;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class CurationEndpoints
{
    public static void MapCurationEndpoints(this WebApplication app)
    {
        MapCurationRoutes(app.MapGroup("/api/curator/curation")
            .WithTags("Curation")
            .RequireAuthorization(BethuyaPolicyNames.RequireCurator));
        MapCurationRoutes(app.MapGroup("/api/curation")
            .WithTags("Curation")
            .RequireAuthorization(BethuyaPolicyNames.RequireCurator));
    }

    private static void MapCurationRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/{eventId:guid}", static async (
            Guid eventId,
            IEventRepository eventRepo,
            IRegistrationRepository registrationRepo,
            IAttendeeProfileRepository attendeeProfileRepo,
            CurationFairnessService fairnessService,
            CancellationToken ct) =>
        {
            var evt = await eventRepo.GetByIdAsync(eventId, ct);
            if (evt is null)
            {
                return Results.NotFound("Event not found");
            }

            var registrations = await registrationRepo.GetByEventIdAsync(eventId, ct);
            var dashboard = await fairnessService.BuildDashboardAsync(evt, registrations, attendeeProfileRepo, registrationRepo, ct: ct);
            return Results.Ok(dashboard);
        });

        group.MapPost("/{eventId:guid}/proposal", async (
            Guid eventId,
            GenerateCurationProposalRequest request,
            IAgent<CuratorRequest, CuratorResponse> curator,
            IEventRepository eventRepo,
            IRegistrationRepository registrationRepo,
            IAttendeeProfileRepository attendeeProfileRepo,
            CurationFairnessService fairnessService,
            CancellationToken ct) =>
        {
            var evt = await eventRepo.GetByIdAsync(eventId, ct);
            if (evt is null)
            {
                return Results.NotFound("Event not found");
            }

            var registrations = await registrationRepo.GetByEventIdAsync(eventId, ct);
            var targets = evt.FairnessTargets ?? new EventFairnessTargets();

            var budget = new FairnessBudget
            {
                EventId = eventId,
                DiversityTargets = new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["geo_outside_dominant"] = targets.GeoOutsideDominantMinPercent,
                    ["local_language_marathi_konkani"] = targets.LocalLanguageMinPercent,
                    ["education_underrepresented"] = targets.UnderrepresentedEducationMinPercent
                },
                EquityPrompts =
                [
                    "Curator must never auto-accept or auto-reject attendees.",
                    "Use only consented derived inclusion signals and privacy-safe aggregates.",
                    "Do not use disability, neurodiversity, or additional support fields for ranking."
                ]
            };

            if (targets.EnableSocioeconomicDimension && targets.UnderrepresentedSocioeconomicMinPercent is not null)
            {
                budget.DiversityTargets["socioeconomic_underrepresented"] = targets.UnderrepresentedSocioeconomicMinPercent.Value;
            }

            var response = await curator.DraftAsync(
                new CuratorRequest(evt, registrations, budget, request.RequestedBy),
                ct);

            var insights = response.Insights.DEINudges
                .Concat(response.Insights.OverRepresentationAlerts)
                .Concat(response.Insights.CommunitySignals)
                .Concat(response.Insights.FirstComeSignals)
                .ToList();

            var dashboard = await fairnessService.BuildDashboardAsync(
                evt,
                registrations,
                attendeeProfileRepo,
                registrationRepo,
                insights,
                ct);
            return Results.Ok(dashboard);
        });

        group.MapPost("/{eventId:guid}/registrants/{registrationId:guid}/decision", async (
            Guid eventId,
            Guid registrationId,
            ApplyCurationDecisionRequest request,
            ClaimsPrincipal user,
            IEventRepository eventRepo,
            IRegistrationRepository registrationRepo,
            IAttendeeProfileRepository attendeeProfileRepo,
            IDecisionRepository decisionRepo,
            CurationFairnessService fairnessService,
            CancellationToken ct) =>
        {
            var evt = await eventRepo.GetByIdAsync(eventId, ct);
            if (evt is null)
            {
                return Results.NotFound("Event not found");
            }

            var registration = await registrationRepo.GetByIdAsync(registrationId, ct);
            if (registration is null || registration.EventId != eventId)
            {
                return Results.NotFound("Registrant not found");
            }

            var (status, type) = request.Action.Trim().ToLowerInvariant() switch
            {
                "approve" => (RegistrationStatus.Accepted, DecisionType.Approve),
                "waitlist" => (RegistrationStatus.Waitlisted, DecisionType.Defer),
                "reject" => (RegistrationStatus.Rejected, DecisionType.Reject),
                _ => throw new InvalidOperationException("Unsupported curation decision action.")
            };

            registration.Status = status;
            await registrationRepo.UpdateAsync(registration, ct);

            var decidedBy = user.FindFirst("email")?.Value
                            ?? user.Identity?.Name
                            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? "curation-ui";

            var decision = new Decision
            {
                EntityType = "registration",
                EntityId = registration.Id,
                Type = type,
                Status = DecisionStatus.Applied,
                DecidedBy = decidedBy,
                Reason = request.Reason
            };

            await decisionRepo.CreateAsync(decision, ct);

            var registrations = await registrationRepo.GetByEventIdAsync(eventId, ct);
            var dashboard = await fairnessService.BuildDashboardAsync(
                evt,
                registrations,
                attendeeProfileRepo,
                registrationRepo,
                ct: ct);

            return Results.Ok(dashboard);
        });
    }
}
