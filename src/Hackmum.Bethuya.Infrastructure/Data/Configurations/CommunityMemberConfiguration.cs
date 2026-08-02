using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class CommunityMemberConfiguration : IEntityTypeConfiguration<CommunityMember>
{
    public void Configure(EntityTypeBuilder<CommunityMember> builder)
    {
        builder.HasKey(member => member.Id);

        builder.Property(member => member.Id)
            .HasConversion(id => id.Value, value => CommunityMemberId.From(value))
            .ValueGeneratedNever();

        builder.Property(member => member.UserId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(member => member.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(member => member.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(member => member.CommunitySlug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(member => member.OccupationStatus)
            .HasMaxLength(200);

        builder.Property(member => member.CompanyName)
            .HasMaxLength(200);

        builder.Property(member => member.EducationInstitute)
            .HasMaxLength(200);

        builder.Property(member => member.Visibility)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(member => member.ResidencyRegion)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(member => member.ResidencyMode)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(member => member.ComplianceProfile)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(member => member.UserId)
            .IsUnique();

        builder.HasIndex(member => new { member.CommunitySlug, member.Email });

        builder.HasMany(member => member.ExternalIdentities)
            .WithOne(identity => identity.CommunityMember)
            .HasForeignKey(identity => identity.CommunityMemberId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
