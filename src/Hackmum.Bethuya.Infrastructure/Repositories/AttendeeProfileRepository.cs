using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Infrastructure.Repositories;

public sealed class AttendeeProfileRepository(BethuyaDbContext db) : IAttendeeProfileRepository
{
    public async Task<AttendeeProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default)
        => await db.AttendeeProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public async Task<AttendeeProfile> CreateAsync(AttendeeProfile profile, CancellationToken ct = default)
    {
        db.AttendeeProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task UpdateAsync(AttendeeProfile profile, CancellationToken ct = default)
    {
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        db.AttendeeProfiles.Update(profile);
        await db.SaveChangesAsync(ct);
    }
}
