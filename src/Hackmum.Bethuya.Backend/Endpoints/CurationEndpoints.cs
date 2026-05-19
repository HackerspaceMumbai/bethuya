using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class CurationEndpoints
{
    public static void MapCurationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/curation").WithTags("Curation");

        group.MapGet("/{eventId:guid}", async (
            Guid eventId,
            IEventRepository eventRepo,
            IRegistrationRepository registrationRepo,
            CurationFairnessService fairnessService,
            CancellationToken ct) =>
        {
            var evt = await eventRepo.GetByIdAsync(eventId, ct);
            if (evt is null)
            {
                return Results.NotFound("Event not found");
            }

            var registrations = await registrationRepo.GetByEventIdAsync(eventId, ct);
            var dashboard = fairnessService.BuildDashboard(evt, registrations);
            return Results.Ok(dashboard);
        });

        group.MapPost("/{eventId:guid}/proposal", async (
            Guid eventId,
            GenerateCurationProposalRequest request,
            IAgent<CuratorRequest, CuratorResponse> curator,
            IEventRepository eventRepo,
            IRegistrationRepository registrationRepo,
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

            var dashboard = fairnessService.BuildDashboard(evt, registrations, insights);
            return Results.Ok(dashboard);
        });
    }
}
