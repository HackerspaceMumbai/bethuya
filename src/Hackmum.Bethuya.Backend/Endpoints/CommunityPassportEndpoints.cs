using System.Collections.Generic;
using System.Security.Claims;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;

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
