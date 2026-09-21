using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class ImportTemplateConfiguration : IEntityTypeConfiguration<ImportTemplate>
{
    public void Configure(EntityTypeBuilder<ImportTemplate> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Scope)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.SourceKind)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.ImportKind)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.OwnerUserId)
            .HasMaxLength(200);

        builder.HasMany(t => t.ColumnMappings)
            .WithOne(m => m.ImportTemplate)
            .HasForeignKey(m => m.ImportTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.Scope, t.OwnerUserId });
    }
}
