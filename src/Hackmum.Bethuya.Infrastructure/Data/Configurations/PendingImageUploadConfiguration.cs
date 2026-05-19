using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class PendingImageUploadConfiguration : IEntityTypeConfiguration<PendingImageUpload>
{
    public void Configure(EntityTypeBuilder<PendingImageUpload> builder)
    {
        builder.HasKey(upload => upload.PublicId);

        builder.Property(upload => upload.PublicId)
            .HasMaxLength(512);

        builder.Property(upload => upload.DeleteTokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(upload => new { upload.AttachedAt, upload.DeletedAt, upload.RequestedAt });
    }
}
