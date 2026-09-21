using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class ImportArtifactConfiguration : IEntityTypeConfiguration<ImportArtifact>
{
    public void Configure(EntityTypeBuilder<ImportArtifact> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(a => a.StorageKey)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(a => a.ContentType)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(a => a.Sha256Checksum)
            .IsRequired()
            .HasMaxLength(64);
    }
}
