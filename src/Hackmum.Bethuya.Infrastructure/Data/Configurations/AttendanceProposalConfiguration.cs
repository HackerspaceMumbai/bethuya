using System.Text.Json;
using Hackmum.Bethuya.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hackmum.Bethuya.Infrastructure.Data.Configurations;

internal sealed class AttendanceProposalConfiguration : IEntityTypeConfiguration<AttendanceProposal>
{
    public void Configure(EntityTypeBuilder<AttendanceProposal> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ProposedAttendeeIds)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<Guid>>(v, JsonSerializerOptions.Default) ?? new List<Guid>());

        builder.Property(p => p.ReviewedBy)
            .HasMaxLength(200);

        builder.Property(p => p.ReviewNotes)
            .HasMaxLength(2000);

        builder.HasOne(p => p.Event)
            .WithMany()
            .HasForeignKey(p => p.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Insights)
            .WithOne()
            .HasForeignKey<CurationInsights>(c => c.AttendanceProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Budget)
            .WithOne()
            .HasForeignKey<FairnessBudget>(f => f.EventId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
