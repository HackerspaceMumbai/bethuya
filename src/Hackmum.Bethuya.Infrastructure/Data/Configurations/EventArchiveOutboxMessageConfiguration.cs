using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class EventArchiveOutboxMessageConfiguration : IEntityTypeConfiguration<EventArchiveOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EventArchiveOutboxMessage> builder)
    {
        builder.HasKey(message => message.Id);
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
