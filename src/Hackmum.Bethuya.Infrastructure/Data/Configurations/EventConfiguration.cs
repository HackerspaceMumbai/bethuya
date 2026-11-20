using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Enums;
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

        builder.Property(e => e.ArchiveFolderPath)
            .HasMaxLength(500);

        builder.Property(e => e.ArchivePublicationStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(e => e.ArchiveBranchName)
            .HasMaxLength(255);

        builder.Property(e => e.ArchivePublicationIdempotencyKey)
            .HasMaxLength(200);

        builder.Property(e => e.ArchivePullRequestUrl)
            .HasMaxLength(2048);

        builder.Property(e => e.ArchiveAdoptedBy)
            .HasMaxLength(200);

        builder.Property(e => e.ArchivePublicationRequestedBy)
            .HasMaxLength(200);

        builder.Property(e => e.ArchiveSourceReadmeSha)
            .HasMaxLength(100);

        builder.Property(e => e.ArchiveSourceMetadataSha)
            .HasMaxLength(100);

        builder.HasIndex(e => e.ArchiveFolderPath)
            .IsUnique()
            .HasFilter("\"ArchiveFolderPath\" IS NOT NULL");

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
