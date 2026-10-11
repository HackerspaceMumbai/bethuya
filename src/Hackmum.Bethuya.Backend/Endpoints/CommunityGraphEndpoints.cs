using System.Security.Claims;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Models;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Backend.Endpoints;

/// <summary>Authenticated, read-only Community Graph API.</summary>
public static class CommunityGraphEndpoints
{
    /// <summary>Maps the graph projection for Scalar and typed clients.</summary>
    public static void MapCommunityGraphEndpoints(this WebApplication app)
    {
        app.MapGet("/api/community/graph", async (ClaimsPrincipal user, CommunityGraphService graph,
            HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var subject = user.GetSubject();
            return subject is null ? Results.Unauthorized() : Results.Ok(await graph.ReadAsync(subject.UserId, ct));
        }).RequireAuthorization().WithTags("Community").WithName("GetCommunityGraph")
            .WithSummary("Explore verified participation within your community")
            .Produces<CommunityGraphSnapshot>();
    }
}
