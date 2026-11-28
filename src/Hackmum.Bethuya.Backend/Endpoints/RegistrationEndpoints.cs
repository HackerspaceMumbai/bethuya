using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ServiceDefaults.Auth;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class RegistrationEndpoints
{
    /// <summary>
    /// Response header on registration create, and request header on government ID upload, carrying a
    /// short-lived token that binds the new registration to the signed-in user who created it.
    /// </summary>
    public const string GovernmentIdUploadTokenHeader = "X-Bethuya-Government-Id-Upload-Token";

    private const string GovernmentIdUploadTokenPurpose = "Bethuya.Registration.GovernmentIdUploadToken.v1";
    private static readonly TimeSpan GovernmentIdUploadTokenLifetime = TimeSpan.FromHours(1);

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
        var group = app.MapGroup("/api/registrations").WithTags("Registrations");

        group.MapGet("/event/{eventId:guid}", async (Guid eventId, IRegistrationRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetByEventIdAsync(eventId, ct)))
            .RequireAuthorization(BethuyaPolicyNames.RequireOrganizerOrCurator);

        group.MapGet("/{id:guid}", async (Guid id, IRegistrationRepository repo, CancellationToken ct) =>
            await repo.GetByIdAsync(id, ct) is { } reg
                ? Results.Ok(reg)
                : Results.NotFound())
            .RequireAuthorization(BethuyaPolicyNames.RequireOrganizerOrCurator);

        group.MapPost("/", async (
            CreateRegistrationRequest request,
            IRegistrationRepository repo,
            IAttendeeProfileRepository profileRepo,
            CommunityPassportService passportService,
            InclusionSignalsNormalizer inclusionSignalsNormalizer,
            IDataProtectionProvider dataProtectionProvider,
            HttpResponse response,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Intent))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["intent"] = ["Why do you want to attend this event? is required."]
                });

            var email = request.Email.Trim();
            var profileInclusionSource = await ResolveProfileInclusionSourceAsync(user, email, profileRepo, ct);
            var inclusionSignals = profileInclusionSource is not null
                ? inclusionSignalsNormalizer.FromSource(profileInclusionSource)
                : new InclusionSignals();
            var subject = user.GetSubject();
            var member = subject is null
                ? null
                : await passportService.EnsureMemberProvisionedAsync(subject, ct);
            var memberId = member is not null
                && string.Equals(email, member.Email, StringComparison.OrdinalIgnoreCase)
                    ? member.Id
                    : (Hackmum.Bethuya.Core.ValueObjects.CommunityMemberId?)null;

            var reg = new Registration
            {
                EventId = request.EventId,
                CommunityMemberId = memberId,
                FullName = request.FullName,
                Email = email,
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
            if (subject is not null)
            {
                response.Headers[GovernmentIdUploadTokenHeader] = CreateGovernmentIdUploadTokenProtector(dataProtectionProvider)
                    .Protect(GetGovernmentIdUploadTokenPayload(created.Id, subject.UserId), GovernmentIdUploadTokenLifetime);
            }

            return Results.Created($"/api/registrations/{created.Id}", created);
        });

        // The backend is called by the Web tier with a bearer token, not a browser form post,
        // so the antiforgery metadata implied by IFormFile does not apply here.
        group.MapPost("/{id:guid}/government-id", UploadGovernmentIdAsync)
            .RequireAuthorization()
            .DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (Guid id, IRegistrationRepository repo, CancellationToken ct) =>
        {
            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization(BethuyaPolicyNames.RequireOrganizer);
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

    private static async Task<IResult> UploadGovernmentIdAsync(
        Guid id,
        IFormFile file,
        IDataProtectionProvider dataProtectionProvider,
        IRegistrationRepository repo,
        BethuyaDbContext db,
        ClaimsPrincipal user,
        HttpRequest request,
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

        if (!HasValidGovernmentIdUploadToken(request, user, reg.Id, dataProtectionProvider)
            && !await CanUploadGovernmentIdAsync(user, reg, db, ct))
            return Results.Forbid();

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

    private static async Task<bool> CanUploadGovernmentIdAsync(
        ClaimsPrincipal user,
        Registration registration,
        BethuyaDbContext db,
        CancellationToken ct)
    {
        if (user.IsInRole(BethuyaRoleNames.Admin) || user.IsInRole(BethuyaRoleNames.Organizer))
        {
            return true;
        }

        var subject = user.GetSubject();
        if (subject is null || registration.CommunityMemberId is not { } memberId)
        {
            return false;
        }

        return await db.CommunityMembers.AnyAsync(m => m.Id == memberId && m.UserId == subject.UserId, ct);
    }

    private static ITimeLimitedDataProtector CreateGovernmentIdUploadTokenProtector(IDataProtectionProvider dataProtectionProvider)
        => dataProtectionProvider.CreateProtector(GovernmentIdUploadTokenPurpose).ToTimeLimitedDataProtector();

    private static string GetGovernmentIdUploadTokenPayload(Guid registrationId, string userId)
        => $"{registrationId:N}:{userId}";

    private static bool HasValidGovernmentIdUploadToken(
        HttpRequest request,
        ClaimsPrincipal user,
        Guid registrationId,
        IDataProtectionProvider dataProtectionProvider)
    {
        var token = request.Headers[GovernmentIdUploadTokenHeader].FirstOrDefault();
        var subject = user.GetSubject();
        if (string.IsNullOrWhiteSpace(token) || subject is null)
        {
            return false;
        }

        try
        {
            var payload = CreateGovernmentIdUploadTokenProtector(dataProtectionProvider).Unprotect(token);
            return string.Equals(payload, GetGovernmentIdUploadTokenPayload(registrationId, subject.UserId), StringComparison.Ordinal);
        }
        catch (CryptographicException)
        {
            return false;
        }
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
