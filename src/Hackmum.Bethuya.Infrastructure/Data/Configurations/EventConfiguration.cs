using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.Location)
            .HasMaxLength(500);

        builder.Property(e => e.Hashtag)
            .HasMaxLength(100);

        builder.Property(e => e.CoverImageUrl)
            .HasMaxLength(2048);

        builder.Property(e => e.SessionizeEventId)
            .HasMaxLength(200);

        builder.Property(e => e.GitHubFolderUrl)
            .HasMaxLength(2048);

        builder.Property(e => e.TeamsAnnouncementMessageId)
            .HasMaxLength(200);

        builder.Property(e => e.RegistrationUrl)
            .HasMaxLength(2048);

        builder.Property(e => e.FairnessTargets)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<EventFairnessTargets>(v, JsonSerializerOptions.Default) ?? new EventFairnessTargets());

        builder.Ignore(e => e.DomainEvents);

        builder.HasIndex(e => e.Hashtag)
            .IsUnique()
            .HasFilter("\"Hashtag\" IS NOT NULL");

        builder.HasIndex(e => e.LifecycleState);

        builder.HasIndex(e => e.SessionizeEventId)
            .IsUnique()
            .HasFilter("\"SessionizeEventId\" IS NOT NULL");

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasMany(e => e.Registrations)
            .WithOne(r => r.Event)
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Agenda)
            .WithOne(a => a!.Event!)
            .HasForeignKey<Agenda>(a => a.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
