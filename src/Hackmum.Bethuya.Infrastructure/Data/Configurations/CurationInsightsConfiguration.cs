using System.Text.Json;
using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class CurationInsightsConfiguration : IEntityTypeConfiguration<CurationInsights>
{
    public void Configure(EntityTypeBuilder<CurationInsights> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ThemeAlignmentScores)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<Dictionary<Guid, double>>(v, JsonSerializerOptions.Default) ?? new Dictionary<Guid, double>());

        builder.Property(c => c.DEINudges)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>());

        builder.Property(c => c.OverRepresentationAlerts)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>());

        builder.Property(c => c.CommunitySignals)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>());

        builder.Property(c => c.FirstComeSignals)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>());
    }
}
