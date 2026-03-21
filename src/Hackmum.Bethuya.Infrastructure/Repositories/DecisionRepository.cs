using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Infrastructure.Repositories;

public sealed class DecisionRepository(BethuyaDbContext db) : IDecisionRepository
{
    public async Task<Decision?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Decisions.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<Decision>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default)
        => await db.Decisions
            .Where(d => d.EntityType == entityType && d.EntityId == entityId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Decision>> GetPendingAsync(CancellationToken ct = default)
        => await db.Decisions
            .Where(d => d.Status == Hackmum.Bethuya.Core.Enums.DecisionStatus.Pending)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);

    public async Task<Decision> CreateAsync(Decision entity, CancellationToken ct = default)
    {
        db.Decisions.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(Decision entity, CancellationToken ct = default)
    {
        db.Decisions.Update(entity);
        await db.SaveChangesAsync(ct);
    }
}
