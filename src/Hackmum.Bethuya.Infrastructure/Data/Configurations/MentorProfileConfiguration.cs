using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class MentorProfileConfiguration : IEntityTypeConfiguration<MentorProfile>
{
    public void Configure(EntityTypeBuilder<MentorProfile> builder)
    {
        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Id)
            .HasConversion(id => id.Value, value => MentorProfileId.From(value))
            .ValueGeneratedNever();

        builder.Property(profile => profile.MemberId)
            .HasConversion(id => id.Value, value => CommunityMemberId.From(value));

        builder.Property(profile => profile.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        // Stored as a JSON string to avoid a separate join table while keeping the schema simple.
        builder.Property(profile => profile.ExpertiseAreasJson)
            .HasColumnName("ExpertiseAreas")
            .IsRequired()
            .HasDefaultValue("[]");

        builder.Ignore(profile => profile.ExpertiseAreas);

        builder.Property(profile => profile.IntroductionBio)
            .HasMaxLength(500);

        builder.HasIndex(profile => profile.MemberId)
            .IsUnique();

        builder.HasIndex(profile => new { profile.Status, profile.IsDiscoverable });

        builder.HasOne(profile => profile.Member)
            .WithOne()
            .HasForeignKey<MentorProfile>(profile => profile.MemberId)
            .HasPrincipalKey<CommunityMember>(member => member.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
