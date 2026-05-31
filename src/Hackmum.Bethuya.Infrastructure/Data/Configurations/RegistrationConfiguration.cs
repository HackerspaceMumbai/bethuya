using System.Text.Json;
using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class RegistrationConfiguration : IEntityTypeConfiguration<Registration>
{
    public void Configure(EntityTypeBuilder<Registration> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Bio)
            .HasMaxLength(2000);

        builder.Property(r => r.Intent)
            .HasMaxLength(4000);

        builder.Property(r => r.Goals)
            .HasMaxLength(1000);

        builder.Property(r => r.Interests)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>());

        builder.Property(r => r.ContributionPreferences)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>());

        builder.Property(r => r.ExperienceLevel)
            .HasMaxLength(50);

        builder.Property(r => r.AttendanceLikelihood)
            .HasMaxLength(50);

        builder.Property(r => r.TravelRequirement)
            .HasMaxLength(50);

        builder.Property(r => r.DietaryRequirements)
            .HasMaxLength(500);

        builder.Property(r => r.AccessibilityNeeds)
            .HasMaxLength(1000);

        builder.Property(r => r.GovernmentIdFileName)
            .HasMaxLength(260);

        builder.Property(r => r.GovernmentIdContentType)
            .HasMaxLength(100);

        builder.Property(r => r.GovernmentIdProtectedPayload);

        builder.Property(r => r.InclusionSignals)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<InclusionSignals>(v, JsonSerializerOptions.Default) ?? new InclusionSignals());

        builder.HasOne(r => r.Event)
            .WithMany(e => e!.Registrations)
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
