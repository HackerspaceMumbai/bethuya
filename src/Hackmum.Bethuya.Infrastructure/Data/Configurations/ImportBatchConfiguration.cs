using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.ImportKind)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(b => b.FailureReason)
            .HasMaxLength(2000);

        builder.Property(b => b.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOne(b => b.Event)
            .WithMany()
            .HasForeignKey(b => b.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.ImportTemplate)
            .WithMany()
            .HasForeignKey(b => b.ImportTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.ImportArtifact)
            .WithOne(a => a.ImportBatch)
            .HasForeignKey<ImportArtifact>(a => a.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.RawRows)
            .WithOne(r => r.ImportBatch)
            .HasForeignKey(r => r.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => new { b.EventId, b.CreatedAt });
    }
}
