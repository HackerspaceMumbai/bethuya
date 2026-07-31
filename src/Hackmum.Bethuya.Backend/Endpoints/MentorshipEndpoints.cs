using System.Collections.Generic;
using System.Security.Claims;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Backend.Endpoints;

/// <summary>
/// Mentorship programme endpoint mappings.
/// Opt-in and discovery require <see cref="BethuyaPolicyNames.RequireAttendee"/>;
/// recommendation drafts require <see cref="BethuyaPolicyNames.RequireOrganizer"/>.
/// </summary>
public static class MentorshipEndpoints
{
    /// <summary>Maps mentor opt-in, status update, profile read, discovery, and recommendation endpoints.</summary>
    public static void MapMentorshipEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/community/mentorship")
            .WithTags("Mentorship")
            .RequireAuthorization(BethuyaPolicyNames.RequireAttendee);

        // ── Opt-in (any authenticated member) ───────────────────────────────

        group.MapPost("/opt-in", async (
            MentorOptInRequest request,
            ClaimsPrincipal user,
            [FromServices] MentorshipService service,
            CancellationToken ct) =>
        {
            if (request.ExpertiseAreas is null || request.ExpertiseAreas.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["expertiseAreas"] = ["At least one expertise area is required to opt in."]
                });
            }

            var subject = GetSubject(user);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            var profile = await service.OptInAsync(subject, request, ct);
            return Results.Ok(profile);
        });

        group.MapPatch("/status", async (
            MentorStatusUpdateRequest request,
            ClaimsPrincipal user,
            [FromServices] MentorshipService service,
            CancellationToken ct) =>
        {
            if (!Enum.IsDefined(request.Status))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Status must be a valid MentorshipStatus value."]
                });
            }

            var subject = GetSubject(user);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                var updated = await service.UpdateStatusAsync(subject, request, ct);
                return Results.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapGet("/my-profile", async (
            ClaimsPrincipal user,
            [FromServices] MentorshipService service,
            CancellationToken ct) =>
        {
            var subject = GetSubject(user);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            var profile = await service.GetMyProfileAsync(subject, ct);
            return profile is null ? Results.NotFound("You have not opted in as a mentor.") : Results.Ok(profile);
        });

        // ── Discovery (any authenticated member) ─────────────────────────────

        group.MapGet("/discover", async (
            [FromQuery] string? expertiseAreas,
            int? limit,
            [FromServices] MentorshipService service,
            CancellationToken ct) =>
        {
            if (limit is <= 0 or > 100)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["limit"] = ["Limit must be between 1 and 100."]
                });
            }

            var filterAreas = ParseExpertiseAreas(expertiseAreas);
            var results = await service.DiscoverMentorsAsync(filterAreas, limit ?? 20, ct);
            return Results.Ok(results);
        });

        // ── Recommendation draft (organizer only) ─────────────────────────────

        group.MapPost("/recommendations/mentor-pairing", async (
            DraftMentorPairingSuggestionRequest request,
            ClaimsPrincipal user,
            [FromServices] MentorshipService service,
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
            var draft = await service.DraftMentorPairingSuggestionAsync(request, requestedBy, ct);
            return Results.Ok(draft);
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

        var displayName = user.FindFirst("name")?.Value ?? user.Identity?.Name;
        var email = user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value;

        return new CommunitySubjectContext(userId, displayName, email);
    }

    private static List<MentorExpertiseArea>? ParseExpertiseAreas(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var parsed = new List<MentorExpertiseArea>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<MentorExpertiseArea>(part, ignoreCase: true, out var area))
            {
                parsed.Add(area);
            }
        }

        return parsed.Count > 0 ? parsed : null;
    }
}
