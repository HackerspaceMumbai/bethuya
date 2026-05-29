using Hackmum.Bethuya.Backend.Services;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class DevelopmentEndpoints
{
    public static void MapDevelopmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dev").WithTags("Development");

        group.MapPost("/curation/seed", async (
            int reviewableCount,
            CurationSampleSeeder seeder,
            CancellationToken ct) =>
        {
            var result = await seeder.SeedAsync(reviewableCount == 0 ? 50 : reviewableCount, ct);
            return Results.Ok(result);
        });
    }
}
