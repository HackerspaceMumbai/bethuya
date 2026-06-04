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
            var profileInclusionSource = await ResolveProfileInclusionSourceAsync(user, request.Email, profileRepo, ct);
            var inclusionSignals = profileInclusionSource is not null
                ? inclusionSignalsNormalizer.FromSource(profileInclusionSource)
                : new InclusionSignals();

            var reg = new Registration
            {
                EventId = request.EventId,
                FullName = request.FullName,
                Email = request.Email,
                Bio = request.Bio,
                Interests = request.Interests,
                Intent = request.Intent,
                Goals = request.Goals,
                ContributionPreferences = request.ContributionPreferences,
                ExperienceLevel = request.ExperienceLevel,
                DietaryRequirements = request.DietaryRequirements,
                AccessibilityNeeds = request.AccessibilityNeeds,
                InclusionSignals = inclusionSignals
            };

            var created = await repo.CreateAsync(reg, ct);
            return Results.Created($"/api/registrations/{created.Id}", created);
        });

        group.MapPost("/{id:guid}/government-id", async (
            Guid id,
            IFormFile file,
            IRegistrationRepository repo,
            CancellationToken ct) =>
        {
            if (file.Length > 10 * 1024 * 1024)
                return Results.Problem("File exceeds the 10 MB limit.", statusCode: 413);

            var reg = await repo.GetByIdAsync(id, ct);
            if (reg is null)
                return Results.NotFound();

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            // Payload stored as base64; production implementation should encrypt before persisting.
            reg.GovernmentIdFileName = file.FileName;
            reg.GovernmentIdContentType = file.ContentType;
            reg.GovernmentIdProtectedPayload = Convert.ToBase64String(ms.ToArray());
            reg.GovernmentIdUploadedAt = DateTimeOffset.UtcNow;

            await repo.UpdateAsync(reg, ct);
            return Results.NoContent();
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

}
