using System.Security.Claims;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class RegistrationEndpoints
{
    private static readonly HashSet<string> AllowedGovernmentIdContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "application/pdf"
    };

    private static readonly HashSet<string> AllowedGovernmentIdExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".pdf"
    };

    public static void MapRegistrationEndpoints(this WebApplication app)
    {
        MapRegistrationRoutes(app.MapGroup("/api/attendee/registrations")
            .WithTags("Registrations")
            .RequireAuthorization(BethuyaPolicyNames.RequireAttendee));
        MapRegistrationRoutes(app.MapGroup("/api/registrations")
            .WithTags("Registrations")
            .RequireAuthorization(BethuyaPolicyNames.RequireAttendee));
    }

    private static void MapRegistrationRoutes(RouteGroupBuilder group)
    {
        // Listing every registrant for an event exposes other attendees' PII, so it is restricted to
        // operational bypass roles (organizer/curator/admin). A null-owner ResourceOwner check succeeds
        // only for bypass roles; a bare attendee is denied with 403 (role-scope denial, not an
        // existence-hiding 404 — the route plainly exists).
        group.MapGet("/event/{eventId:guid}", static async (
            Guid eventId,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            IRegistrationRepository repo,
            CancellationToken ct) =>
        {
            var staffOnly = await authorization.AuthorizeAsync(
                user, new ResourceOwnerContext(null), BethuyaPolicyNames.ResourceOwner);
            if (!staffOnly.Succeeded)
            {
                return Results.Forbid();
            }

            return Results.Ok(await repo.GetByEventIdAsync(eventId, ct));
        });

        group.MapGet("/{id:guid}", static async (
            Guid id,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            IRegistrationRepository repo,
            CancellationToken ct) =>
        {
            if (await repo.GetByIdAsync(id, ct) is not { } reg)
            {
                return Results.NotFound();
            }

            // Ownership failure is reported as 404 (not 403) so callers cannot probe which registration
            // ids exist for other attendees.
            return await IsOwnerOrBypassAsync(authorization, user, reg)
                ? Results.Ok(reg)
                : Results.NotFound();
        });

        group.MapPost("/", CreateRegistrationAsync);

        // Government-ID upload is a bearer-token multipart API (no browser cookie/form), and the
        // backend pipeline intentionally registers no antiforgery middleware. IFormFile endpoints
        // otherwise carry implicit antiforgery metadata that makes them unreachable, so disable it
        // explicitly. Ownership of the target registration is still enforced inside the handler.
        group.MapPost("/{id:guid}/government-id", UploadGovernmentIdAsync).DisableAntiforgery();

        group.MapDelete("/{id:guid}", static async (
            Guid id,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            IRegistrationRepository repo,
            CancellationToken ct) =>
        {
            if (await repo.GetByIdAsync(id, ct) is not { } reg
                || !await IsOwnerOrBypassAsync(authorization, user, reg))
            {
                return Results.NotFound();
            }

            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static async Task<bool> IsOwnerOrBypassAsync(
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        Registration registration)
    {
        var result = await authorization.AuthorizeAsync(
            user,
            new ResourceOwnerContext(registration.UserId),
            BethuyaPolicyNames.ResourceOwner);
        return result.Succeeded;
    }

    private static async Task<IResult> CreateRegistrationAsync(
        CreateRegistrationRequest request,
        IRegistrationRepository repo,
        IAttendeeProfileRepository profileRepo,
        InclusionSignalsNormalizer inclusionSignalsNormalizer,
        IUserContext userContext,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Intent))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["intent"] = ["Why do you want to attend this event? is required."]
            });

        // A registration must have a server-authoritative owner. The route requires an authenticated
        // attendee, so a missing subject is an authentication failure (401) — never trust a body field.
        if (userContext.UserId is not { } ownerUserId)
        {
            return Results.Unauthorized();
        }

        // M1 (PR3): the backend independently enforces mandatory-profile completion before allowing
        // registration, regardless of the UI onboarding gate. Honors Onboarding:BypassMandatoryProfile.
        if (!OnboardingEnforcement.IsBypassEnabled(configuration))
        {
            if (await profileRepo.GetByUserIdAsync(ownerUserId, ct) is not { IsProfileComplete: true })
            {
                return Results.Problem(
                    "Complete your mandatory attendee profile before registering for an event.",
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }

        var profileInclusionSource = await ResolveProfileInclusionSourceAsync(userContext, request.Email, profileRepo, ct);
        var inclusionSignals = profileInclusionSource is not null
            ? inclusionSignalsNormalizer.FromSource(profileInclusionSource)
            : new InclusionSignals();

        var reg = new Registration
        {
            EventId = request.EventId,
            UserId = ownerUserId,
            FullName = request.FullName,
            Email = request.Email,
            Bio = request.Bio,
            Interests = request.Interests,
            Intent = request.Intent.Trim(),
            Goals = request.Goals,
            ContributionPreferences = request.ContributionPreferences ?? [],
            ExperienceLevel = request.ExperienceLevel,
            DietaryRequirements = request.DietaryRequirements,
            AccessibilityNeeds = request.AccessibilityNeeds,
            InclusionSignals = inclusionSignals
        };

        var created = await repo.CreateAsync(reg, ct);
        return Results.Created($"/api/registrations/{created.Id}", created);
    }

    private static async Task<AttendeeInclusionSource?> ResolveProfileInclusionSourceAsync(
        IUserContext userContext,
        string email,
        IAttendeeProfileRepository profileRepo,
        CancellationToken ct)
    {
        var userId = userContext.UserId;

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

        var claimedEmail = userContext.Email;
        if (string.IsNullOrWhiteSpace(claimedEmail)
            || !string.Equals(claimedEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await profileRepo.GetInclusionSourceByEmailAsync(email, ct);
    }

    private static async Task<IResult> UploadGovernmentIdAsync(
        Guid id,
        IFormFile file,
        ClaimsPrincipal user,
        IAuthorizationService authorization,
        IDataProtectionProvider dataProtectionProvider,
        IRegistrationRepository repo,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Government ID file is required."]
            });

        if (file.Length > 10 * 1024 * 1024)
            return Results.Problem("File exceeds the 10 MB limit.", statusCode: 413);

        var safeFileName = Path.GetFileName(file.FileName ?? string.Empty);
        var extension = Path.GetExtension(safeFileName);
        if (!AllowedGovernmentIdExtensions.Contains(extension)
            || !AllowedGovernmentIdContentTypes.Contains(file.ContentType))
        {
            return Results.Problem("Only .jpg, .jpeg, .png, and .pdf government ID files are supported.", statusCode: 415);
        }

        var reg = await repo.GetByIdAsync(id, ct);
        if (reg is null)
            return Results.NotFound();

        // Uploading a government ID against a registration you do not own is an IDOR; reported as 404 so
        // ownership of other attendees' registrations cannot be probed.
        if (!await IsOwnerOrBypassAsync(authorization, user, reg))
            return Results.NotFound();

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var payloadBytes = ms.ToArray();

        if (!MatchesAllowedSignature(extension, payloadBytes))
            return Results.Problem("Uploaded file content does not match the provided file type.", statusCode: 415);

        var protector = dataProtectionProvider.CreateProtector("Bethuya.Registration.GovernmentIdPayload.v1");
        var protectedPayload = protector.Protect(Convert.ToBase64String(payloadBytes));

        reg.GovernmentIdFileName = safeFileName;
        reg.GovernmentIdContentType = file.ContentType;
        reg.GovernmentIdProtectedPayload = protectedPayload;
        reg.GovernmentIdUploadedAt = DateTimeOffset.UtcNow;

        await repo.UpdateAsync(reg, ct);
        return Results.NoContent();
    }

    private static bool MatchesAllowedSignature(string extension, byte[] bytes)
    {
        if (bytes.Length < 4)
        {
            return false;
        }

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46, // %PDF
            ".png" => bytes.Length >= 8
                      && bytes[0] == 0x89
                      && bytes[1] == 0x50
                      && bytes[2] == 0x4E
                      && bytes[3] == 0x47
                      && bytes[4] == 0x0D
                      && bytes[5] == 0x0A
                      && bytes[6] == 0x1A
                      && bytes[7] == 0x0A,
            ".jpg" or ".jpeg" => bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[^2] == 0xFF && bytes[^1] == 0xD9,
            _ => false
        };
    }
}
