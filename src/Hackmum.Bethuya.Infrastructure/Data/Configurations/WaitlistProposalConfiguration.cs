using System.Text.Json;
using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class WaitlistProposalConfiguration : IEntityTypeConfiguration<WaitlistProposal>
{
    public void Configure(EntityTypeBuilder<WaitlistProposal> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.WaitlistedRegistrationIds)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<Guid>>(v, JsonSerializerOptions.Default) ?? new List<Guid>());

        builder.Property(p => p.Reason)
            .HasMaxLength(2000);

        builder.HasOne(p => p.Event)
            .WithMany()
            .HasForeignKey(p => p.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
