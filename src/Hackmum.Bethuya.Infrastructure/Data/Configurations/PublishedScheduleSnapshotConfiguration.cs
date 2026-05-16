using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class PublishedScheduleSnapshotConfiguration : IEntityTypeConfiguration<PublishedScheduleSnapshot>
{
    public void Configure(EntityTypeBuilder<PublishedScheduleSnapshot> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.MarkdownAgenda)
            .IsRequired();

        builder.Property(s => s.AgendaJson)
            .IsRequired();

        builder.Property(s => s.PublishedBy)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.AgentVersionTag)
            .HasMaxLength(200);

        builder.HasIndex(s => new { s.EventId, s.PublishedAt });
        builder.HasIndex(s => new { s.PlanningCycleId, s.PublishedAt });
    }
}

