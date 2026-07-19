using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class AgendaSessionConfiguration : IEntityTypeConfiguration<AgendaSession>
{
    public void Configure(EntityTypeBuilder<AgendaSession> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Speaker)
            .HasMaxLength(200);

        builder.Property(s => s.SpeakerGitHubHandle)
            .HasMaxLength(100);

        builder.Property(s => s.SpeakerTwitterHandle)
            .HasMaxLength(100);

        builder.Property(s => s.SpeakerAvatarUrl)
            .HasMaxLength(2048);

        builder.Property(s => s.Description)
            .HasMaxLength(2000);

        builder.Property(s => s.SourceSessionId)
            .HasMaxLength(200);

        builder.Property(s => s.SlidesUrl)
            .HasMaxLength(2048);

        builder.Property(s => s.RecordingUrl)
            .HasMaxLength(2048);

        builder.HasIndex(s => s.AgendaId);

        builder.HasIndex(s => new { s.AgendaId, s.Source, s.SourceSessionId })
            .IsUnique()
            .HasFilter("\"SourceSessionId\" IS NOT NULL");
    }
}
