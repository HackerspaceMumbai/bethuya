using Hackmum.Bethuya.Backend.Services;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class DevelopmentEndpoints
{
    public static void MapDevelopmentEndpoints(this WebApplication app)
    {
        MapDevelopmentRoutes(app.MapGroup("/api/admin/dev")
            .WithTags("Development")
            .RequireAuthorization(BethuyaPolicyNames.RequireAdmin));
        MapDevelopmentRoutes(app.MapGroup("/api/dev")
            .WithTags("Development")
            .RequireAuthorization(BethuyaPolicyNames.RequireAdmin));
    }

    private static void MapDevelopmentRoutes(RouteGroupBuilder group)
    {
        group.MapPost("/curation/seed", static async (
            int reviewableCount,
            CurationSampleSeeder seeder,
            CancellationToken ct) =>
        {
            var result = await seeder.SeedAsync(reviewableCount == 0 ? 50 : reviewableCount, ct);
            return Results.Ok(result);
        });
    }
}
