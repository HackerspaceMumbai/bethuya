using System.Collections.Generic;
using System.Security.Claims;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Backend.Endpoints;

/// <summary>
/// Community Passport API endpoint mappings.
/// </summary>
public static class CommunityPassportEndpoints
{
    /// <summary>
    /// Maps authenticated Community Passport read and privacy-update endpoints.
    /// </summary>
    /// <param name="app">Application instance to map endpoints on.</param>
    public static void MapCommunityPassportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/community/passport")
            .WithTags("Community")
            .RequireAuthorization();

        group.MapGet("/", async (
            ClaimsPrincipal user,
            CommunityPassportService service,
            CancellationToken ct) =>
        {
            var subject = GetSubject(user);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            var passport = await service.GetPassportAsync(subject, ct);
            return Results.Ok(passport);
        });

        group.MapPost("/privacy", async (
            UpdateCommunityPassportPrivacyRequest request,
            ClaimsPrincipal user,
            CommunityPassportService service,
            CancellationToken ct) =>
        {
            if (!Enum.IsDefined(request.Visibility))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["visibility"] = ["Visibility must be a valid ProfileVisibilityScope value."]
                });
            }

            var subject = GetSubject(user);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            var updatedPrivacy = await service.UpdatePrivacyAsync(subject, request, ct);
            return Results.Ok(updatedPrivacy);
        });

        group.MapPost("/participation", async (
            UpsertParticipationEntriesRequest request,
            ClaimsPrincipal user,
            ParticipationLedgerService service,
            CancellationToken ct) =>
        {
            if (request.Entries is null || request.Entries.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["entries"] = ["At least one participation entry is required."]
                });
            }

            var subject = GetSubject(user);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                var result = await service.WriteAsync(subject, request, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["entries"] = [ex.Message]
                });
            }
        })
        .RequireAuthorization(BethuyaPolicyNames.RequireConnectorIngestion);

        group.MapGet("/participation/timeline", async (
            int? limit,
            ClaimsPrincipal user,
            ParticipationLedgerService service,
            CancellationToken ct) =>
        {
            if (limit is <= 0 or > 200)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["limit"] = ["Limit must be between 1 and 200."]
                });
            }

            var subject = GetSubject(user);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            var timeline = await service.ReadTimelineAsync(subject, limit ?? 25, ct);
            return Results.Ok(timeline);
        });

        group.MapGet("/journey", async (
            int? timelineLimit,
            ClaimsPrincipal user,
            [FromServices] CommunityJourneyReadModelService service,
            CancellationToken ct) =>
        {
            if (timelineLimit is <= 0 or > 100)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["timelineLimit"] = ["Timeline limit must be between 1 and 100."]
                });
            }

            var subject = GetSubject(user);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            var journey = await service.GetJourneyProjectionAsync(subject, timelineLimit ?? 20, ct);
            return Results.Ok(journey);
        });

        group.MapGet("/dashboard/read-model", async (
            int? lookbackDays,
            [FromServices] CommunityJourneyReadModelService service,
            CancellationToken ct) =>
        {
            if (lookbackDays is < 30 or > 365)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["lookbackDays"] = ["Lookback days must be between 30 and 365."]
                });
            }

            var dashboard = await service.GetDashboardReadModelAsync(lookbackDays ?? 90, ct);
            return Results.Ok(dashboard);
        })
        .RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);

        group.MapPost("/recommendations/member-growth", async (
            DraftMemberGrowthRecommendationRequest request,
            ClaimsPrincipal user,
            [FromServices] CommunityRecommendationService service,
            CancellationToken ct) =>
        {
            if (request.LookbackDays is < 30 or > 365)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["lookbackDays"] = ["Lookback days must be between 30 and 365."]
                });
            }

            var subject = GetSubject(user);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            var requestedBy = subject.Email ?? subject.DisplayName ?? subject.UserId;
            var draft = await service.DraftMemberGrowthOpportunityAsync(request, requestedBy, ct);
            return Results.Ok(draft);
        })
        .RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);

        group.MapPost("/recommendations/weekly-briefing", async (
            DraftWeeklyCommunityBriefingRequest request,
            ClaimsPrincipal user,
            [FromServices] CommunityRecommendationService service,
            CancellationToken ct) =>
        {
            if (request.LookbackDays is < 30 or > 365)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["lookbackDays"] = ["Lookback days must be between 30 and 365."]
                });
            }

            var subject = GetSubject(user);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            var requestedBy = subject.Email ?? subject.DisplayName ?? subject.UserId;
            var draft = await service.DraftWeeklyBriefingAsync(request, requestedBy, ct);
            return Results.Ok(draft);
        })
        .RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);

        group.MapPost("/recommendations/{draftId:guid}/approve", async (
            Guid draftId,
            ApproveRecommendationDraftRequest request,
            ClaimsPrincipal user,
            [FromServices] CommunityRecommendationService service,
            CancellationToken ct) =>
        {
            var subject = GetSubject(user);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            var approver = subject.Email ?? subject.DisplayName ?? subject.UserId;

            try
            {
                var approved = await service.ApproveDraftAsync(
                    draftId,
                    approver,
                    request,
                    ct);
                return Results.Ok(approved);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound("Recommendation draft not found.");
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        })
        .RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);
    }

    private static CommunitySubjectContext? GetSubject(ClaimsPrincipal user)
    {
        var userId = user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var displayName = user.FindFirst("name")?.Value
            ?? user.Identity?.Name;
        var email = user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value;

        return new CommunitySubjectContext(userId, displayName, email);
    }
}
