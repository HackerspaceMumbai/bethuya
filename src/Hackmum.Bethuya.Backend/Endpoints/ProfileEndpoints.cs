using System.ComponentModel.DataAnnotations;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app)
    {
        MapProfileRoutes(app.MapGroup("/api/attendee/profile")
            .WithTags("Profile")
            .RequireAuthorization(BethuyaPolicyNames.RequireAttendee));
        MapProfileRoutes(app.MapGroup("/api/profile")
            .WithTags("Profile")
            .RequireAuthorization(BethuyaPolicyNames.RequireAttendee));
    }

    private static void MapProfileRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            IUserContext userContext,
            IAttendeeProfileRepository repo,
            CancellationToken ct) =>
        {
            var userId = userContext.UserId;
            if (userId is null) return Results.Unauthorized();

            var profile = await repo.GetByUserIdAsync(userId, ct);
            return Results.Ok(MapMandatoryProfile(profile));
        });

        group.MapGet("/completion-status", async (
            IUserContext userContext,
            IAttendeeProfileRepository repo,
            CancellationToken ct) =>
        {
            var userId = userContext.UserId;
            if (userId is null) return Results.Unauthorized();

            var profile = await repo.GetByUserIdAsync(userId, ct);

            if (profile is null)
            {
                return Results.Ok(new ProfileCompletionStatusResponse(
                    IsProfileComplete: false,
                    IsSocialConnectionsComplete: false,
                    IsAideProfileComplete: false,
                    ProfileCompletedAt: null,
                    AideProfileCompletedAt: null));
            }

            return Results.Ok(new ProfileCompletionStatusResponse(
                profile.IsProfileComplete,
                IsSocialProfileComplete(profile),
                profile.IsAideProfileComplete,
                profile.ProfileCompletedAt,
                profile.AideProfileCompletedAt));
        });

        group.MapGet("/social", async (
            IUserContext userContext,
            IAttendeeProfileRepository repo,
            CancellationToken ct) =>
        {
            var userId = userContext.UserId;
            if (userId is null) return Results.Unauthorized();

            var profile = await repo.GetByUserIdAsync(userId, ct);
            if (profile is null)
            {
                return Results.Ok(new SocialProfileResponse(null, false, false, null, null, null, null));
            }

            return Results.Ok(new SocialProfileResponse(
                profile.OccupationStatus,
                IsLinkedInRequired(profile.OccupationStatus),
                IsGitHubRequired(profile.OccupationStatus),
                profile.LinkedInMemberId,
                profile.LinkedInProfileUrl,
                profile.GitHubLogin,
                profile.GitHubProfileUrl));
        });

        group.MapGet("/aide", async (
            IUserContext userContext,
            IAttendeeProfileRepository repo,
            CancellationToken ct) =>
        {
            var userId = userContext.UserId;
            if (userId is null) return Results.Unauthorized();

            var profile = await repo.GetByUserIdAsync(userId, ct);
            return Results.Ok(MapAideProfile(profile));
        });

        group.MapPost("/", async (
            SaveMandatoryProfileRequest request,
            IUserContext userContext,
            IAttendeeProfileRepository repo,
            CancellationToken ct) =>
        {
            var validationErrors = ValidateMandatoryRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var userId = userContext.UserId;
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
                    GovernmentPhotoIdType = request.GovernmentPhotoIdType ?? string.Empty,
                    GovernmentIdLastFour = request.GovernmentIdLastFour ?? string.Empty,
                    LinkedInMemberId = string.Empty,
                    LinkedInProfileUrl = null,
                    GitHubLogin = string.Empty,
                    GitHubProfileUrl = string.Empty,
                    IsProfileComplete = true,
                    ProfileCompletedAt = DateTimeOffset.UtcNow
                };

                ApplyMandatoryFields(profile, request);

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
                IsSocialProfileComplete(profile),
                profile.IsAideProfileComplete,
                profile.ProfileCompletedAt,
                profile.AideProfileCompletedAt));
        });

        group.MapPost("/social", async (
            SaveSocialProfileRequest request,
            IUserContext userContext,
            IAttendeeProfileRepository repo,
            CancellationToken ct) =>
        {
            var userId = userContext.UserId;
            if (userId is null) return Results.Unauthorized();

            var profile = await repo.GetByUserIdAsync(userId, ct);
            var isMandatoryBypassEnabled = IsMandatoryBypassEnabled();

            if (profile is null)
            {
                if (!isMandatoryBypassEnabled)
                {
                    return Results.BadRequest("Mandatory profile must be completed first.");
                }

                profile = CreateBypassedProfile(userId);
                await repo.CreateAsync(profile, ct);
            }
            else if (!profile.IsProfileComplete && !isMandatoryBypassEnabled)
            {
                return Results.BadRequest("Mandatory profile must be completed first.");
            }

            if (!profile.IsProfileComplete && isMandatoryBypassEnabled)
            {
                profile.IsProfileComplete = true;
                profile.ProfileCompletedAt ??= DateTimeOffset.UtcNow;
            }

            var validationErrors = ValidateSocialRequest(request, profile.OccupationStatus);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            profile.LinkedInMemberId = request.LinkedInMemberId ?? string.Empty;
            profile.LinkedInProfileUrl = request.LinkedInProfileUrl;
            profile.GitHubLogin = request.GitHubLogin ?? string.Empty;
            profile.GitHubProfileUrl = request.GitHubProfileUrl ?? string.Empty;

            await repo.UpdateAsync(profile, ct);

            return Results.Ok(new ProfileCompletionStatusResponse(
                profile.IsProfileComplete,
                IsSocialProfileComplete(profile),
                profile.IsAideProfileComplete,
                profile.ProfileCompletedAt,
                profile.AideProfileCompletedAt));
        });

        group.MapPost("/aide", async (
            SaveAideProfileRequest request,
            IUserContext userContext,
            IAttendeeProfileRepository repo,
            CancellationToken ct) =>
        {
            var userId = userContext.UserId;
            if (userId is null) return Results.Unauthorized();

            var profile = await repo.GetByUserIdAsync(userId, ct);
            var isMandatoryBypassEnabled = IsMandatoryBypassEnabled();

            if (profile is null)
            {
                if (!isMandatoryBypassEnabled)
                {
                    return Results.BadRequest("Mandatory profile must be completed first.");
                }

                profile = CreateBypassedProfile(userId);
                await repo.CreateAsync(profile, ct);
            }
            else if (!profile.IsProfileComplete && !isMandatoryBypassEnabled)
            {
                return Results.BadRequest("Mandatory profile must be completed first.");
            }

            if (!profile.IsProfileComplete && isMandatoryBypassEnabled)
            {
                profile.IsProfileComplete = true;
                profile.ProfileCompletedAt ??= DateTimeOffset.UtcNow;
            }

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
                IsSocialProfileComplete(profile),
                profile.IsAideProfileComplete,
                profile.ProfileCompletedAt,
                profile.AideProfileCompletedAt));
        });
    }

    private static void ApplyMandatoryFields(AttendeeProfile profile, SaveMandatoryProfileRequest request)
    {
        profile.FirstName = request.FirstName;
        profile.LastName = request.LastName;
        profile.Email = request.Email;
        profile.MobileNumber = request.MobileNumber;
        profile.GovernmentPhotoIdType = string.IsNullOrWhiteSpace(request.GovernmentPhotoIdType) ? string.Empty : request.GovernmentPhotoIdType;
        profile.GovernmentIdLastFour = string.IsNullOrWhiteSpace(request.GovernmentIdLastFour) ? string.Empty : request.GovernmentIdLastFour;
        profile.OccupationStatus = request.OccupationStatus;
        profile.CompanyName = IsCompanyRequiredOccupation(request.OccupationStatus)
            ? request.CompanyName
            : null;
        profile.EducationInstitute = string.Equals(request.OccupationStatus, "Student", StringComparison.Ordinal)
            ? request.EducationInstitute
            : null;
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

    private static MandatoryProfileResponse MapMandatoryProfile(AttendeeProfile? profile)
        => profile is null
            ? new MandatoryProfileResponse(null, null, null, null, null, null, null, null, null, null, null, null, null)
            : new MandatoryProfileResponse(
                profile.FirstName,
                profile.LastName,
                profile.Email,
                profile.MobileNumber,
                profile.GovernmentPhotoIdType,
                profile.GovernmentIdLastFour,
                profile.OccupationStatus,
                profile.CompanyName,
                profile.EducationInstitute,
                profile.City,
                profile.State,
                profile.PostalCode,
                profile.Country);

    private static AideProfileResponse MapAideProfile(AttendeeProfile? profile)
        => profile is null
            ? new AideProfileResponse(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)
            : new AideProfileResponse(
                profile.GenderIdentity,
                profile.SelfDescribeGender,
                profile.AgeRange,
                profile.Ethnicity,
                profile.SelfDescribeEthnicity,
                profile.Disability,
                profile.DisabilityDetails,
                profile.DietaryRequirements,
                profile.LgbtqIdentity,
                profile.ParentalStatus,
                profile.Religion,
                profile.Caste,
                profile.Neighborhood,
                profile.ModeOfTransportation,
                profile.SocioeconomicBackground,
                profile.Neurodiversity,
                profile.CaregivingResponsibilities,
                profile.LanguageProficiency,
                profile.EducationalBackground,
                profile.HowDidYouHear,
                profile.AdditionalSupport);

    private static Dictionary<string, string[]> ValidateMandatoryRequest(SaveMandatoryProfileRequest request)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true);

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var result in validationResults)
        {
            var members = result.MemberNames.Any() ? result.MemberNames : [string.Empty];
            foreach (var member in members)
            {
                if (!errors.TryGetValue(member, out var memberErrors))
                {
                    memberErrors = [];
                    errors[member] = memberErrors;
                }

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    memberErrors.Add(result.ErrorMessage);
                }
            }
        }

        return errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.Ordinal);
    }

    private static Dictionary<string, string[]> ValidateSocialRequest(SaveSocialProfileRequest request, string? occupationStatus)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true);

        var errors = validationResults
            .SelectMany(result => (result.MemberNames.Any() ? result.MemberNames : [string.Empty])
                .Select(member => new { member, result.ErrorMessage }))
            .Where(item => !string.IsNullOrWhiteSpace(item.ErrorMessage))
            .GroupBy(item => item.member, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.ErrorMessage!).ToArray(),
                StringComparer.Ordinal);

        if (IsLinkedInRequired(occupationStatus) && string.IsNullOrWhiteSpace(request.LinkedInMemberId))
        {
            errors[nameof(SaveSocialProfileRequest.LinkedInMemberId)] =
            [
                "LinkedIn is required for working professionals. Connecting GitHub as well improves your chances for selection."
            ];
        }

        if (IsGitHubRequired(occupationStatus))
        {
            var githubErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.GitHubLogin))
            {
                githubErrors.Add("GitHub is required for students. Connecting LinkedIn as well improves your chances for selection.");
            }

            if (string.IsNullOrWhiteSpace(request.GitHubProfileUrl))
            {
                githubErrors.Add("Verified GitHub profile URL is required when GitHub is required.");
            }

            if (githubErrors.Count > 0)
            {
                errors[nameof(SaveSocialProfileRequest.GitHubLogin)] = githubErrors.ToArray();
            }
        }

        if (!string.IsNullOrWhiteSpace(request.GitHubLogin) && string.IsNullOrWhiteSpace(request.GitHubProfileUrl))
        {
            errors[nameof(SaveSocialProfileRequest.GitHubProfileUrl)] =
            [
                "Verified GitHub profile URL is required when GitHub is connected."
            ];
        }

        return errors;
    }

    private static bool IsSocialProfileComplete(AttendeeProfile profile)
        => (!IsLinkedInRequired(profile.OccupationStatus) || !string.IsNullOrWhiteSpace(profile.LinkedInMemberId))
            && (!IsGitHubRequired(profile.OccupationStatus) || (!string.IsNullOrWhiteSpace(profile.GitHubLogin) && !string.IsNullOrWhiteSpace(profile.GitHubProfileUrl)));

    private static bool IsLinkedInRequired(string? occupationStatus)
        => string.Equals(occupationStatus, "Working Professional", StringComparison.Ordinal)
            || string.Equals(occupationStatus, "Employee", StringComparison.Ordinal)
            || string.Equals(occupationStatus, "Full time employed", StringComparison.Ordinal)
            || string.Equals(occupationStatus, "Full-time employed", StringComparison.Ordinal);

    private static bool IsGitHubRequired(string? occupationStatus)
        => string.Equals(occupationStatus, "Student", StringComparison.Ordinal);

    private static bool IsCompanyRequiredOccupation(string? occupationStatus)
        => string.Equals(occupationStatus, "Working Professional", StringComparison.Ordinal)
            || string.Equals(occupationStatus, "Independent / Freelancer", StringComparison.Ordinal)
            || string.Equals(occupationStatus, "Employee", StringComparison.Ordinal)
            || string.Equals(occupationStatus, "Self-employed", StringComparison.Ordinal)
            || string.Equals(occupationStatus, "Founder", StringComparison.Ordinal)
            || string.Equals(occupationStatus, "Freelancer", StringComparison.Ordinal)
            || string.Equals(occupationStatus, "Full time employed", StringComparison.Ordinal)
            || string.Equals(occupationStatus, "Full-time employed", StringComparison.Ordinal);

    private static bool IsMandatoryBypassEnabled()
        => string.Equals(
            Environment.GetEnvironmentVariable("Onboarding__BypassMandatoryProfile"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static AttendeeProfile CreateBypassedProfile(string userId)
        => new()
        {
            UserId = userId,
            FirstName = string.Empty,
            LastName = string.Empty,
            Email = string.Empty,
            GovernmentPhotoIdType = string.Empty,
            GovernmentIdLastFour = string.Empty,
            LinkedInMemberId = string.Empty,
            GitHubLogin = string.Empty,
            GitHubProfileUrl = string.Empty,
            IsProfileComplete = true,
            ProfileCompletedAt = DateTimeOffset.UtcNow
        };
}
