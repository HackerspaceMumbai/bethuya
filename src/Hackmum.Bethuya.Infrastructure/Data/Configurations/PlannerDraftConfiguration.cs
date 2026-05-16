using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class PlannerDraftConfiguration : IEntityTypeConfiguration<PlannerDraft>
{
    public void Configure(EntityTypeBuilder<PlannerDraft> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.WorkItemId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.InputHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(d => d.MarkdownAgenda)
            .IsRequired();

        builder.Property(d => d.AgendaJson)
            .IsRequired();

        builder.Property(d => d.ResponseId)
            .HasMaxLength(200);

        builder.Property(d => d.AgentName)
            .HasMaxLength(200);

        builder.Property(d => d.AgentVersionTag)
            .HasMaxLength(200);

        builder.Property(d => d.TraceParent)
            .HasMaxLength(200);

        builder.Property(d => d.CorrelationId)
            .HasMaxLength(200);

        builder.HasIndex(d => new { d.PlanningCycleId, d.WorkItemId })
            .IsUnique();
    }
}

