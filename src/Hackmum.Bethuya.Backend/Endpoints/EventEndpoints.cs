using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;

namespace Hackmum.Bethuya.Backend.Endpoints;

public static class EventEndpoints
{
    public static void MapEventEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/events").WithTags("Events");

        group.MapGet("/", async (IEventRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetAllAsync(ct)));

        group.MapGet("/{id:guid}", async (Guid id, IEventRepository repo, CancellationToken ct) =>
            await repo.GetByIdAsync(id, ct) is { } evt
                ? Results.Ok(evt)
                : Results.NotFound());

        group.MapPost("/", async (CreateEventRequest request, IEventRepository repo, CancellationToken ct) =>
        {
            var evt = new Event
            {
                Title = request.Title,
                Description = request.Description,
                Type = request.Type,
                Capacity = request.Capacity,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Location = request.Location,
                CreatedBy = request.CreatedBy
            };

            var created = await repo.CreateAsync(evt, ct);
            return Results.Created($"/api/events/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateEventRequest request, IEventRepository repo, CancellationToken ct) =>
        {
            var evt = await repo.GetByIdAsync(id, ct);
            if (evt is null) return Results.NotFound();

            evt.Title = request.Title;
            evt.Description = request.Description;
            evt.Type = request.Type;
            evt.Capacity = request.Capacity;
            evt.StartDate = request.StartDate;
            evt.EndDate = request.EndDate;
            evt.Location = request.Location;
            evt.Status = request.Status;

            await repo.UpdateAsync(evt, ct);
            return Results.Ok(evt);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IEventRepository repo, CancellationToken ct) =>
        {
            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
