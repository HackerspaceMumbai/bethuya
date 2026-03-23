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
        {
            var events = await repo.GetAllAsync(ct);
            var response = events.Select(MapToResponse).ToList();
            return Results.Ok(response);
        });

        group.MapGet("/{id:guid}", async (Guid id, IEventRepository repo, CancellationToken ct) =>
        {
            var evt = await repo.GetByIdAsync(id, ct);
            return evt is not null
                ? Results.Ok(MapToResponse(evt))
                : Results.NotFound();
        });

        group.MapPost("/", async (CreateEventRequest request, IEventRepository repo, CancellationToken ct) =>
        {
            // Validate request
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                errors[nameof(request.Title)] = ["Title is required."];
            }
            else if (request.Title.Length > 200)
            {
                errors[nameof(request.Title)] = ["Title must be 200 characters or fewer."];
            }

            if (request.Capacity < 1 || request.Capacity > 10_000)
            {
                errors[nameof(request.Capacity)] = ["Capacity must be between 1 and 10,000."];
            }

            if (request.EndDate < request.StartDate)
            {
                errors[nameof(request.EndDate)] = ["End date must be on or after the start date."];
            }

            if (string.IsNullOrWhiteSpace(request.CreatedBy))
            {
                errors[nameof(request.CreatedBy)] = ["CreatedBy is required."];
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

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
            var response = MapToResponse(created);
            return Results.Created($"/api/events/{created.Id}", response);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateEventRequest request, IEventRepository repo, CancellationToken ct) =>
        {
            var evt = await repo.GetByIdAsync(id, ct);
            if (evt is null) return Results.NotFound();

            // Validate request
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                errors[nameof(request.Title)] = ["Title is required."];
            }
            else if (request.Title.Length > 200)
            {
                errors[nameof(request.Title)] = ["Title must be 200 characters or fewer."];
            }

            if (request.Capacity < 1 || request.Capacity > 10_000)
            {
                errors[nameof(request.Capacity)] = ["Capacity must be between 1 and 10,000."];
            }

            if (request.EndDate < request.StartDate)
            {
                errors[nameof(request.EndDate)] = ["End date must be on or after the start date."];
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            evt.Title = request.Title;
            evt.Description = request.Description;
            evt.Type = request.Type;
            evt.Capacity = request.Capacity;
            evt.StartDate = request.StartDate;
            evt.EndDate = request.EndDate;
            evt.Location = request.Location;
            evt.Status = request.Status;

            await repo.UpdateAsync(evt, ct);
            return Results.Ok(MapToResponse(evt));
        });

        group.MapDelete("/{id:guid}", async (Guid id, IEventRepository repo, CancellationToken ct) =>
        {
            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static EventResponse MapToResponse(Event evt) =>
        new(
            evt.Id,
            evt.Title,
            evt.Description,
            evt.Type.ToString(),
            evt.Status.ToString(),
            evt.Capacity,
            evt.StartDate,
            evt.EndDate,
            evt.Location,
            evt.CreatedBy,
            evt.CreatedAt);
}
