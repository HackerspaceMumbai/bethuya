using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class CommunitySignalAwardConfiguration : IEntityTypeConfiguration<CommunitySignalAward>
{
    public void Configure(EntityTypeBuilder<CommunitySignalAward> builder)
    {
        builder.HasKey(award => award.Id);
        builder.Property(award => award.Id)
            .HasConversion(id => id.Value, value => CommunitySignalAwardId.From(value))
            .ValueGeneratedNever();
        builder.Property(award => award.CommunityMemberId)
            .HasConversion(id => id.Value, value => CommunityMemberId.From(value));
        builder.Property(award => award.Kind).HasConversion<string>().HasMaxLength(50);
        builder.Property(award => award.Rationale).IsRequired().HasMaxLength(1000);
        builder.Property(award => award.EvidenceEntryIdsJson).IsRequired().HasDefaultValue("[]");
        builder.Ignore(award => award.EvidenceEntryIds);
        builder.Property(award => award.AwardedBy).IsRequired().HasMaxLength(200);
        builder.Property(award => award.RevokedBy).HasMaxLength(200);
        builder.Property(award => award.RevocationReason).HasMaxLength(1000);
        builder.Property(award => award.RevokedAt).IsConcurrencyToken();
        builder.HasIndex(award => new { award.CommunityMemberId, award.Kind })
            .HasFilter("\"RevokedAt\" IS NULL")
            .IsUnique();
    }
}
