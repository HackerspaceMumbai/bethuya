using System.Text.Json;
using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class EventReportConfiguration : IEntityTypeConfiguration<EventReport>
{
    public void Configure(EntityTypeBuilder<EventReport> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Summary)
            .HasMaxLength(5000);

        builder.Property(r => r.DraftedByAgent)
            .HasMaxLength(200);

        builder.Property(r => r.EditedBy)
            .HasMaxLength(200);

        builder.Property(r => r.Highlights)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>());

        builder.Property(r => r.ActionItems)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>());

        builder.HasOne(r => r.Event)
            .WithMany()
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
