using System.Security.Claims;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class CommunityPassportEndpoints
{
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
