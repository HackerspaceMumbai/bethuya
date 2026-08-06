using Hackmum.Bethuya.Backend.Services;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class DevelopmentEndpoints
{
    public static void MapDevelopmentEndpoints(this WebApplication app)
    {
        // Belt-and-suspenders: enforce the Development-only invariant at the method level.
        // The call-site in Program.cs already guards with IsDevelopment(), but an explicit
        // check here prevents accidental exposure if that guard is ever inadvertently removed —
        // especially important for the /curation/seed endpoint, which mutates data.
        if (!app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"Development endpoints (curation seeder + identity diagnostic) must only be " +
                $"registered in the Development environment. Current environment: " +
                $"'{app.Environment.EnvironmentName}'. Remove the {nameof(MapDevelopmentEndpoints)}() " +
                $"call from non-Development startup paths.");
        }

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
        // propagated correctly end-to-end. Reachability is doubly guarded: the environment check
        // above (method-level) and the call-site guard in Program.cs.
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
