using System.Security.Claims;
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

        // Returns the Backend-observed identity so developers can verify that persona switching
        // propagated correctly end-to-end. Only mapped inside app.Environment.IsDevelopment()
        // (guaranteed by the call-site in Program.cs — no redundant gate needed here).
        group.MapGet("/identity", (HttpContext context) =>
        {
            var user = context.User;
            return Results.Ok(new
            {
                sub = user.FindFirst("sub")?.Value,
                email = user.FindFirst("email")?.Value,
                name = user.FindFirst("name")?.Value,
                roles = user.Claims
                    .Where(c => c.Type == "role")
                    .Select(c => c.Value)
                    .ToArray(),
            });
        });
    }
}
