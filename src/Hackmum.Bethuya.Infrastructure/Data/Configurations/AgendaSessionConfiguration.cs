using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class AgendaSessionConfiguration : IEntityTypeConfiguration<AgendaSession>
{
    public void Configure(EntityTypeBuilder<AgendaSession> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Speaker)
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .HasMaxLength(2000);
    }
}
