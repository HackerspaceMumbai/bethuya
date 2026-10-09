using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class CommunityRelationshipConfiguration : IEntityTypeConfiguration<CommunityRelationship>
{
    public void Configure(EntityTypeBuilder<CommunityRelationship> builder)
    {
        builder.HasKey(relationship => relationship.Id);
        builder.Property(relationship => relationship.Id)
            .HasConversion(id => id.Value, value => CommunityRelationshipId.From(value))
            .ValueGeneratedNever();
        builder.Property(relationship => relationship.SourceMemberId)
            .HasConversion(id => id.Value, value => CommunityMemberId.From(value));
        builder.Property(relationship => relationship.TargetMemberId)
            .HasConversion(id => id.Value, value => CommunityMemberId.From(value));
        builder.Property(relationship => relationship.Kind).HasConversion<string>().HasMaxLength(50);
        builder.Property(relationship => relationship.Context).IsRequired().HasMaxLength(500);
        builder.Property(relationship => relationship.EvidenceEntryIdsJson).IsRequired().HasDefaultValue("[]");
        builder.Ignore(relationship => relationship.EvidenceEntryIds);
        builder.HasIndex(relationship => new
        {
            relationship.SourceMemberId,
            relationship.TargetMemberId,
            relationship.Kind
        }).IsUnique();
        builder.HasOne<CommunityMember>()
            .WithMany()
            .HasForeignKey(relationship => relationship.SourceMemberId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CommunityMember>()
            .WithMany()
            .HasForeignKey(relationship => relationship.TargetMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
