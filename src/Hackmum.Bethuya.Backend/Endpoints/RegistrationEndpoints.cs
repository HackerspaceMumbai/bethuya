using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using System.Security.Claims;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class RegistrationEndpoints
{
    public static void MapRegistrationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/registrations").WithTags("Registrations");

        group.MapGet("/event/{eventId:guid}", async (Guid eventId, IRegistrationRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetByEventIdAsync(eventId, ct)));

        group.MapGet("/{id:guid}", async (Guid id, IRegistrationRepository repo, CancellationToken ct) =>
            await repo.GetByIdAsync(id, ct) is { } reg
                ? Results.Ok(reg)
                : Results.NotFound());

        group.MapPost("/", async (
            CreateRegistrationRequest request,
            IRegistrationRepository repo,
            IAttendeeProfileRepository profileRepo,
            InclusionSignalsNormalizer inclusionSignalsNormalizer,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var requestInclusionSource = new AttendeeInclusionSource(
                request.Neighborhood,
                request.LanguageProficiency,
                request.EducationalBackground,
                request.SocioeconomicBackground);

            var profileInclusionSource = await ResolveProfileInclusionSourceAsync(user, request.Email, profileRepo, ct);
            var effectiveInclusionSource = MergeInclusionSource(requestInclusionSource, profileInclusionSource);

            if (!IsCompleteInclusionSource(effectiveInclusionSource))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(CreateRegistrationRequest.Neighborhood)] =
                    [
                        "Neighborhood is required for curation fairness calculations."
                    ],
                    [nameof(CreateRegistrationRequest.LanguageProficiency)] =
                    [
                        "Languages spoken are required for curation fairness calculations."
                    ],
                    [nameof(CreateRegistrationRequest.EducationalBackground)] =
                    [
                        "Educational background is required for curation fairness calculations."
                    ],
                    [nameof(CreateRegistrationRequest.SocioeconomicBackground)] =
                    [
                        "Socioeconomic background is required for curation fairness calculations."
                    ]
                });
            }

            var reg = new Registration
            {
                EventId = request.EventId,
                FullName = request.FullName,
                Email = request.Email,
                Bio = request.Bio,
                Interests = request.Interests,
                InclusionSignals = inclusionSignalsNormalizer.FromSource(effectiveInclusionSource)
            };

            var created = await repo.CreateAsync(reg, ct);
            return Results.Created($"/api/registrations/{created.Id}", created);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IRegistrationRepository repo, CancellationToken ct) =>
        {
            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static async Task<AttendeeInclusionSource?> ResolveProfileInclusionSourceAsync(
        ClaimsPrincipal user,
        string email,
        IAttendeeProfileRepository profileRepo,
        CancellationToken ct)
    {
        var userId = user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var profile = await profileRepo.GetByUserIdAsync(userId, ct);
        if (profile is not null)
        {
            return new AttendeeInclusionSource(
                profile.Neighborhood,
                profile.LanguageProficiency,
                profile.EducationalBackground,
                profile.SocioeconomicBackground);
        }

        var claimedEmail = user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value;
        if (string.IsNullOrWhiteSpace(claimedEmail)
            || !string.Equals(claimedEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await profileRepo.GetInclusionSourceByEmailAsync(email, ct);
    }

    private static AttendeeInclusionSource? MergeInclusionSource(
        AttendeeInclusionSource? requestSource,
        AttendeeInclusionSource? profileSource)
    {
        if (requestSource is null && profileSource is null)
        {
            return null;
        }

        static string? Pick(string? primary, string? fallback)
            => !string.IsNullOrWhiteSpace(primary) ? primary : fallback;

        return new AttendeeInclusionSource(
            Pick(requestSource?.Neighborhood, profileSource?.Neighborhood),
            Pick(requestSource?.LanguageProficiency, profileSource?.LanguageProficiency),
            Pick(requestSource?.EducationalBackground, profileSource?.EducationalBackground),
            Pick(requestSource?.SocioeconomicBackground, profileSource?.SocioeconomicBackground));
    }

    private static bool IsCompleteInclusionSource(AttendeeInclusionSource? source)
        => source is not null
            && !string.IsNullOrWhiteSpace(source.Neighborhood)
            && !string.IsNullOrWhiteSpace(source.LanguageProficiency)
            && !string.IsNullOrWhiteSpace(source.EducationalBackground)
            && !string.IsNullOrWhiteSpace(source.SocioeconomicBackground);
}
