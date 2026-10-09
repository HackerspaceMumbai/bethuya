using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class MemberOpportunityConfiguration : IEntityTypeConfiguration<MemberOpportunity>
{
    public void Configure(EntityTypeBuilder<MemberOpportunity> builder)
    {
        builder.HasKey(opportunity => opportunity.Id);
        builder.Property(opportunity => opportunity.Id)
            .HasConversion(id => id.Value, value => MemberOpportunityId.From(value))
            .ValueGeneratedNever();
        builder.Property(opportunity => opportunity.CommunityMemberId)
            .HasConversion(id => id.Value, value => CommunityMemberId.From(value));
        builder.Property(opportunity => opportunity.Kind).HasConversion<string>().HasMaxLength(50);
        builder.Property(opportunity => opportunity.CurrentStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(opportunity => opportunity.Title).IsRequired().HasMaxLength(200);
        builder.Property(opportunity => opportunity.Description).IsRequired().HasMaxLength(2000);
        builder.Property(opportunity => opportunity.Outcome).HasMaxLength(2000);
        builder.Property(opportunity => opportunity.LifecycleJson).IsRequired().HasDefaultValue("[]");
        builder.Ignore(opportunity => opportunity.Lifecycle);
        builder.HasIndex(opportunity => new { opportunity.CommunityMemberId, opportunity.OfferedAt });
    }
}
