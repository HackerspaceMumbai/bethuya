using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Infrastructure.Data.PII;

/// <summary>
/// Local SQLite DbContext for PII storage during curation phase.
/// CRITICAL: This database is LOCAL ONLY — never synced to cloud.
/// </summary>
public sealed class PiiDbContext(DbContextOptions<PiiDbContext> options) : DbContext(options)
{
    /// <summary>Registrant PII — deleted after curation approval.</summary>
    public DbSet<Registration> PiiRegistrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Minimal configuration for PII registrations
        modelBuilder.Entity<Registration>()
            .HasKey(r => r.Id);

        modelBuilder.Entity<Registration>()
            .HasIndex(r => r.EventId)
            .HasDatabaseName("idx_pii_registrations_event_id");

        modelBuilder.Entity<Registration>()
            .Property(r => r.Interests)
            .HasConversion(
                v => string.Join(",", v),
                v => v.Split(",", StringSplitOptions.RemoveEmptyEntries).ToList());
    }
}
