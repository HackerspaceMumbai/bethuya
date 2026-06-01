using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Infrastructure.Repositories;

public sealed class RegistrationRepository(BethuyaDbContext db) : IRegistrationRepository
{
    public async Task<Registration?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Registrations.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Registration>> GetByEventIdAsync(Guid eventId, CancellationToken ct = default)
        => await db.Registrations
            .Where(r => r.EventId == eventId)
            .OrderBy(r => r.RegisteredAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<Registration>>> GetHistoricalByEmailsAsync(
        IReadOnlyCollection<string> emails,
        Guid excludedEventId,
        CancellationToken ct = default)
    {
        if (emails.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<Registration>>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedEmails = emails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var registrations = await db.Registrations
            .AsNoTracking()
            .Where(r => normalizedEmails.Contains(r.Email) && r.EventId != excludedEventId)
            .OrderByDescending(r => r.RegisteredAt)
            .ToListAsync(ct);

        return registrations
            .GroupBy(registration => registration.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Registration>)group.ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Registration> CreateAsync(Registration entity, CancellationToken ct = default)
    {
        db.Registrations.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(Registration entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        db.Registrations.Update(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Registrations.FindAsync([id], ct);
        if (entity is not null)
        {
            db.Registrations.Remove(entity);
            await db.SaveChangesAsync(ct);
        }
    }
}
