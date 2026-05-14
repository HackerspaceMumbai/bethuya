using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class PlanningCycleConfiguration : IEntityTypeConfiguration<PlanningCycle>
{
    public void Configure(EntityTypeBuilder<PlanningCycle> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ConversationId)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(c => new { c.EventId, c.ConversationId })
            .IsUnique();

        builder.HasIndex(c => new { c.EventId, c.Status });

        builder.HasOne(c => c.Event)
            .WithMany()
            .HasForeignKey(c => c.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Drafts)
            .WithOne(d => d.PlanningCycle)
            .HasForeignKey(d => d.PlanningCycleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.InvocationAudits)
            .WithOne(a => a.PlanningCycle)
            .HasForeignKey(a => a.PlanningCycleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

