using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class RegistrationEndpoints
{
    public static void MapRegistrationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/registrations").WithTags("Registrations");

        group.MapGet("/event/{eventId:guid}", async (Guid eventId, IRegistrationRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetByEventIdAsync(eventId, ct)));

        group.MapGet("/{id:guid}", async (Guid id, IRegistrationRepository repo, CancellationToken ct) =>
            await repo.GetByIdAsync(id, ct) is { } reg
                ? Results.Ok(reg)
                : Results.NotFound());

        group.MapPost("/", async (CreateRegistrationRequest request, IRegistrationRepository repo, CancellationToken ct) =>
        {
            var reg = new Registration
            {
                EventId = request.EventId,
                FullName = request.FullName,
                Email = request.Email,
                Bio = request.Bio,
                Interests = request.Interests
            };

            var created = await repo.CreateAsync(reg, ct);
            return Results.Created($"/api/registrations/{created.Id}", created);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IRegistrationRepository repo, CancellationToken ct) =>
        {
            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
