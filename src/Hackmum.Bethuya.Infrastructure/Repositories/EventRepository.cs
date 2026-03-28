using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Infrastructure.Repositories;

public sealed class EventRepository(BethuyaDbContext db) : IEventRepository
{
    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Events
            .Include(e => e.Registrations)
            .Include(e => e.Agenda)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<Event?> GetByHashtagAsync(string hashtag, CancellationToken ct = default)
        => await db.Events
            .Include(e => e.Registrations)
            .Include(e => e.Agenda)
            .FirstOrDefaultAsync(e => e.Hashtag != null && e.Hashtag == hashtag, ct);

    public async Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken ct = default)
        => await db.Events.OrderByDescending(e => e.CreatedAt).ToListAsync(ct);

    public async Task<Event> CreateAsync(Event entity, CancellationToken ct = default)
    {
        db.Events.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(Event entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        db.Events.Update(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Events.FindAsync([id], ct);
        if (entity is not null)
        {
            db.Events.Remove(entity);
            await db.SaveChangesAsync(ct);
        }
    }
}
