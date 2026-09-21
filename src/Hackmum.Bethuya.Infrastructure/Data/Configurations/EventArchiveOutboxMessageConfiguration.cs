using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class EventArchiveOutboxMessageConfiguration : IEntityTypeConfiguration<EventArchiveOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EventArchiveOutboxMessage> builder)
    {
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id)
            .HasConversion(id => id.Value, value => EventArchiveOutboxMessageId.From(value));
        builder.Property(message => message.EventId)
            .HasConversion(id => id.Value, value => EventId.From(value));
        builder.Property(message => message.Destination).HasMaxLength(100).IsRequired();
        builder.Property(message => message.FolderPath).HasMaxLength(500).IsRequired();
        builder.Property(message => message.ReadmeMarkdown).IsRequired();
        builder.Property(message => message.MetadataJson).IsRequired();
        builder.Property(message => message.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(4000);
        builder.HasIndex(message => new { message.ProcessedAt, message.AvailableAt });
        builder.HasIndex(message => new { message.EventId, message.Destination, message.IdempotencyKey })
            .IsUnique();
    }
}
