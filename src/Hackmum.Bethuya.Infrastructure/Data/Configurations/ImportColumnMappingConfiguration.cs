using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class ImportColumnMappingConfiguration : IEntityTypeConfiguration<ImportColumnMapping>
{
    public void Configure(EntityTypeBuilder<ImportColumnMapping> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.SourceColumnName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.TargetField)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasIndex(m => m.ImportTemplateId);
    }
}
