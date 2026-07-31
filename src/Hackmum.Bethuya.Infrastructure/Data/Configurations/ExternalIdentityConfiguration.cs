using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class ExternalIdentityConfiguration : IEntityTypeConfiguration<ExternalIdentity>
{
    public void Configure(EntityTypeBuilder<ExternalIdentity> builder)
    {
        builder.HasKey(identity => identity.Id);

        builder.Property(identity => identity.Id)
            .HasConversion(id => id.Value, value => ExternalIdentityId.From(value))
            .ValueGeneratedNever();

        builder.Property(identity => identity.CommunityMemberId)
            .HasConversion(id => id.Value, value => CommunityMemberId.From(value));

        builder.Property(identity => identity.Provider)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(identity => identity.Subject)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(identity => identity.Username)
            .HasMaxLength(200);

        builder.Property(identity => identity.ProfileUrl)
            .HasMaxLength(500);

        builder.HasIndex(identity => new { identity.Provider, identity.Subject })
            .IsUnique();
    }
}
