using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Microsoft.AspNetCore.DataProtection;
using ServiceDefaults.Auth;
using System.Security.Claims;

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
        group.MapGet("/event/{eventId:guid}", static async (Guid eventId, IRegistrationRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetByEventIdAsync(eventId, ct)));

        group.MapGet("/{id:guid}", static async (Guid id, IRegistrationRepository repo, CancellationToken ct) =>
            await repo.GetByIdAsync(id, ct) is { } reg
                ? Results.Ok(reg)
                : Results.NotFound());

        group.MapPost("/", CreateRegistrationAsync);

        group.MapPost("/{id:guid}/government-id", UploadGovernmentIdAsync);

        group.MapDelete("/{id:guid}", static async (Guid id, IRegistrationRepository repo, CancellationToken ct) =>
        {
            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> CreateRegistrationAsync(
        CreateRegistrationRequest request,
        IRegistrationRepository repo,
        IAttendeeProfileRepository profileRepo,
        InclusionSignalsNormalizer inclusionSignalsNormalizer,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Intent))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["intent"] = ["Why do you want to attend this event? is required."]
            });

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
