using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class CommunityPortfolioEntryConfiguration : IEntityTypeConfiguration<CommunityPortfolioEntry>
{
    public void Configure(EntityTypeBuilder<CommunityPortfolioEntry> builder)
    {
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id)
            .HasConversion(id => id.Value, value => CommunityPortfolioEntryId.From(value))
            .ValueGeneratedNever();
        builder.Property(entry => entry.CommunityMemberId)
            .HasConversion(id => id.Value, value => CommunityMemberId.From(value));
        builder.Property(entry => entry.Title).IsRequired().HasMaxLength(160);
        builder.Property(entry => entry.Description).IsRequired().HasMaxLength(2000);
        builder.Property(entry => entry.LinksJson).IsRequired().HasDefaultValue("[]");
        builder.Property(entry => entry.EvidenceEntryIdsJson).IsRequired().HasDefaultValue("[]");
        builder.Ignore(entry => entry.Links);
        builder.Ignore(entry => entry.EvidenceEntryIds);
        builder.HasIndex(entry => new { entry.CommunityMemberId, entry.DisplayOrder });
    }
}
