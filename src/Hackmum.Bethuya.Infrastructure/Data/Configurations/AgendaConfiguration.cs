using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class AgendaConfiguration : IEntityTypeConfiguration<Agenda>
{
    public void Configure(EntityTypeBuilder<Agenda> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CreatedByAgent)
            .HasMaxLength(200);

        builder.HasOne(a => a.Event)
            .WithOne(e => e!.Agenda!)
            .HasForeignKey<Agenda>(a => a.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Sessions)
            .WithOne()
            .HasForeignKey(s => s.AgendaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
