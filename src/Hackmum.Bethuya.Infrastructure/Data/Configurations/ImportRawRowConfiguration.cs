using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class ImportRawRowConfiguration : IEntityTypeConfiguration<ImportRawRow>
{
    public void Configure(EntityTypeBuilder<ImportRawRow> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RawDataJson)
            .IsRequired();

        builder.HasIndex(r => new { r.ImportBatchId, r.RowIndex })
            .IsUnique();
    }
}
