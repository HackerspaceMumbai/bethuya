using System.Security.Claims;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/profile").WithTags("Profile");

        group.MapGet("/completion-status", async (
            ClaimsPrincipal user,
            IAttendeeProfileRepository repo,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            var profile = await repo.GetByUserIdAsync(userId, ct);

            if (profile is null)
            {
                return Results.Ok(new ProfileCompletionStatusResponse(
                    IsProfileComplete: false,
                    IsAideProfileComplete: false,
                    ProfileCompletedAt: null,
                    AideProfileCompletedAt: null));
            }

            return Results.Ok(new ProfileCompletionStatusResponse(
                profile.IsProfileComplete,
                profile.IsAideProfileComplete,
                profile.ProfileCompletedAt,
                profile.AideProfileCompletedAt));
        });

        group.MapPost("/", async (
            SaveMandatoryProfileRequest request,
            ClaimsPrincipal user,
            IAttendeeProfileRepository repo,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            var profile = await repo.GetByUserIdAsync(userId, ct);

            if (profile is null)
            {
                profile = new AttendeeProfile
                {
                    UserId = userId,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    MobileNumber = request.MobileNumber,
                    OccupationStatus = request.OccupationStatus,
                    City = request.City,
                    State = request.State,
                    PostalCode = request.PostalCode,
                    Country = request.Country,
                    IsProfileComplete = true,
                    ProfileCompletedAt = DateTimeOffset.UtcNow
                };

                try
                {
                    await repo.CreateAsync(profile, ct);
                }
                catch (DbUpdateException)
                {
                    // Concurrent first-time request already created the profile; fetch and update instead.
                    profile = await repo.GetByUserIdAsync(userId, ct)
                        ?? throw new InvalidOperationException("Profile missing after concurrent insert.");
                    ApplyMandatoryFields(profile, request);
                    await repo.UpdateAsync(profile, ct);
                }
            }
            else
            {
                ApplyMandatoryFields(profile, request);
                await repo.UpdateAsync(profile, ct);
            }

            return Results.Ok(new ProfileCompletionStatusResponse(
                profile.IsProfileComplete,
                profile.IsAideProfileComplete,
                profile.ProfileCompletedAt,
                profile.AideProfileCompletedAt));
        });

        group.MapPost("/aide", async (
            SaveAideProfileRequest request,
            ClaimsPrincipal user,
            IAttendeeProfileRepository repo,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            var profile = await repo.GetByUserIdAsync(userId, ct);

            if (profile is null || !profile.IsProfileComplete)
                return Results.BadRequest("Mandatory profile must be completed first.");

            profile.GenderIdentity = request.GenderIdentity;
            profile.SelfDescribeGender = request.SelfDescribeGender;
            profile.AgeRange = request.AgeRange;
            profile.Ethnicity = request.Ethnicity;
            profile.SelfDescribeEthnicity = request.SelfDescribeEthnicity;
            profile.Disability = request.Disability;
            profile.DisabilityDetails = request.DisabilityDetails;
            profile.DietaryRequirements = request.DietaryRequirements;
            profile.LgbtqIdentity = request.LgbtqIdentity;
            profile.ParentalStatus = request.ParentalStatus;
            profile.Religion = request.Religion;
            profile.Caste = request.Caste;
            profile.Neighborhood = request.Neighborhood;
            profile.ModeOfTransportation = request.ModeOfTransportation;
            profile.SocioeconomicBackground = request.SocioeconomicBackground;
            profile.Neurodiversity = request.Neurodiversity;
            profile.CaregivingResponsibilities = request.CaregivingResponsibilities;
            profile.LanguageProficiency = request.LanguageProficiency;
            profile.EducationalBackground = request.EducationalBackground;
            profile.HowDidYouHear = request.HowDidYouHear;
            profile.AdditionalSupport = request.AdditionalSupport;

            if (!profile.IsAideProfileComplete)
            {
                profile.IsAideProfileComplete = true;
                profile.AideProfileCompletedAt = DateTimeOffset.UtcNow;
            }

            await repo.UpdateAsync(profile, ct);

            return Results.Ok(new ProfileCompletionStatusResponse(
                profile.IsProfileComplete,
                profile.IsAideProfileComplete,
                profile.ProfileCompletedAt,
                profile.AideProfileCompletedAt));
        });
    }

    /// <summary>Extracts the user identifier from the <c>sub</c> JWT claim, or falls back to <c>nameidentifier</c>.</summary>
    private static string? GetUserId(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated is not true) return null;
        return user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private static void ApplyMandatoryFields(AttendeeProfile profile, SaveMandatoryProfileRequest request)
    {
        profile.FirstName = request.FirstName;
        profile.LastName = request.LastName;
        profile.Email = request.Email;
        profile.MobileNumber = request.MobileNumber;
        profile.OccupationStatus = request.OccupationStatus;
        profile.City = request.City;
        profile.State = request.State;
        profile.PostalCode = request.PostalCode;
        profile.Country = request.Country;

        if (!profile.IsProfileComplete)
        {
            profile.IsProfileComplete = true;
            profile.ProfileCompletedAt = DateTimeOffset.UtcNow;
        }
    }
}

