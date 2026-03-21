using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class DecisionConfiguration : IEntityTypeConfiguration<Decision>
{
    public void Configure(EntityTypeBuilder<Decision> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.DecidedBy)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Reason)
            .HasMaxLength(2000);

        builder.Property(d => d.Diff);
    }
}
