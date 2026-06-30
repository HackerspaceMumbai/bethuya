using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class PlannerInvocationAuditConfiguration : IEntityTypeConfiguration<PlannerInvocationAudit>
{
    public void Configure(EntityTypeBuilder<PlannerInvocationAudit> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.WorkItemId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.ConversationId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.CycleState)
            .IsRequired();

        builder.Property(a => a.InputHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(a => a.MarkdownAgenda)
            .IsRequired();

        builder.Property(a => a.AgendaJson)
            .IsRequired();

        builder.Property(a => a.ResponseId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.AgentName)
            .HasMaxLength(200);

        builder.Property(a => a.AgentVersionTag)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.TraceParent)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.CorrelationId)
            .HasMaxLength(200);

        builder.HasIndex(a => new { a.PlanningCycleId, a.WorkItemId })
            .IsUnique();
    }
}

