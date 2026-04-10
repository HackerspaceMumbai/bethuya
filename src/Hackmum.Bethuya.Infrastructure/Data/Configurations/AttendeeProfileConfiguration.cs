using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class AttendeeProfileConfiguration : IEntityTypeConfiguration<AttendeeProfile>
{
    public void Configure(EntityTypeBuilder<AttendeeProfile> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.UserId).IsUnique();

        builder.Property(p => p.UserId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.MobileNumber).HasMaxLength(30);
        builder.Property(p => p.OccupationStatus).HasMaxLength(200);
        builder.Property(p => p.City).HasMaxLength(100);
        builder.Property(p => p.State).HasMaxLength(100);
        builder.Property(p => p.PostalCode).HasMaxLength(20);
        builder.Property(p => p.Country).HasMaxLength(100);

        // AIDE optional fields
        builder.Property(p => p.GenderIdentity).HasMaxLength(100);
        builder.Property(p => p.SelfDescribeGender).HasMaxLength(200);
        builder.Property(p => p.AgeRange).HasMaxLength(50);
        builder.Property(p => p.Ethnicity).HasMaxLength(100);
        builder.Property(p => p.SelfDescribeEthnicity).HasMaxLength(200);
        builder.Property(p => p.Disability).HasMaxLength(100);
        builder.Property(p => p.DisabilityDetails).HasMaxLength(1000);
        builder.Property(p => p.DietaryRequirements).HasMaxLength(500);
        builder.Property(p => p.LgbtqIdentity).HasMaxLength(100);
        builder.Property(p => p.ParentalStatus).HasMaxLength(100);
        builder.Property(p => p.Religion).HasMaxLength(100);
        builder.Property(p => p.Caste).HasMaxLength(100);
        builder.Property(p => p.Neighborhood).HasMaxLength(200);
        builder.Property(p => p.ModeOfTransportation).HasMaxLength(100);
        builder.Property(p => p.SocioeconomicBackground).HasMaxLength(200);
        builder.Property(p => p.Neurodiversity).HasMaxLength(200);
        builder.Property(p => p.CaregivingResponsibilities).HasMaxLength(200);
        builder.Property(p => p.LanguageProficiency).HasMaxLength(500);
        builder.Property(p => p.EducationalBackground).HasMaxLength(200);
        builder.Property(p => p.HowDidYouHear).HasMaxLength(200);
        builder.Property(p => p.AdditionalSupport).HasMaxLength(1000);
    }
}
