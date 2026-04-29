using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

public sealed class ApprovalStateConfiguration : IEntityTypeConfiguration<ApprovalState>
{
    public void Configure(EntityTypeBuilder<ApprovalState> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasDefaultValueSql("NEWID()");

        builder.Property(a => a.EventId)
            .IsRequired();

        builder.Property(a => a.WorkflowPhase)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(a => a.Approver)
            .HasMaxLength(100);

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(a => a.EventId);
        builder.HasIndex(a => a.Status);
    }
}
