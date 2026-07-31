using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class ParticipationLedgerEntryConfiguration : IEntityTypeConfiguration<ParticipationLedgerEntry>
{
    public void Configure(EntityTypeBuilder<ParticipationLedgerEntry> builder)
    {
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasConversion(id => id.Value, value => ParticipationLedgerEntryId.From(value))
            .ValueGeneratedNever();

        builder.Property(entry => entry.CommunityMemberId)
            .HasConversion(id => id.Value, value => CommunityMemberId.From(value));

        builder.Property(entry => entry.Connector)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(entry => entry.ExternalMemberKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(entry => entry.ExternalEventId)
            .HasMaxLength(200);

        builder.Property(entry => entry.ExternalRecordId)
            .HasMaxLength(200);

        builder.Property(entry => entry.Activity)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(entry => entry.Evidence)
            .IsRequired()
            .HasMaxLength(600);

        builder.Property(entry => entry.ProvenanceKey)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(entry => entry.SourceCorrelationId)
            .HasMaxLength(200);

        builder.HasIndex(entry => entry.ProvenanceKey)
            .IsUnique();

        builder.HasIndex(entry => new { entry.CommunityMemberId, entry.OccurredAt });

        builder.HasIndex(entry => entry.EventId);

        builder.HasOne(entry => entry.CommunityMember)
            .WithMany(member => member.ParticipationLedgerEntries)
            .HasForeignKey(entry => entry.CommunityMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(entry => entry.EventId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
