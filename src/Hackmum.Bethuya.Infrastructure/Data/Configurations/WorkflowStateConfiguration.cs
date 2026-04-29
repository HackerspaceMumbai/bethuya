using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

public sealed class WorkflowStateConfiguration : IEntityTypeConfiguration<WorkflowState>
{
    public void Configure(EntityTypeBuilder<WorkflowState> builder)
    {
        builder.HasKey(w => w.EventId);

        builder.Property(w => w.CurrentPhase)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(w => w.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(w => w.LastUpdated)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");
    }
}
