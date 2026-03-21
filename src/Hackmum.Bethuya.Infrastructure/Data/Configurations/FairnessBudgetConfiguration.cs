using System.Text.Json;
using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class FairnessBudgetConfiguration : IEntityTypeConfiguration<FairnessBudget>
{
    public void Configure(EntityTypeBuilder<FairnessBudget> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.DiversityTargets)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<Dictionary<string, double>>(v, JsonSerializerOptions.Default) ?? new Dictionary<string, double>());

        builder.Property(f => f.ActualMetrics)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<Dictionary<string, double>>(v, JsonSerializerOptions.Default) ?? new Dictionary<string, double>());

        builder.Property(f => f.EquityPrompts)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>());

        builder.HasOne(f => f.Event)
            .WithMany()
            .HasForeignKey(f => f.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
