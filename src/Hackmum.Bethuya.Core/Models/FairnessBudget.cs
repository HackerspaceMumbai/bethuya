namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Diversity and equity targets for attendee curation.
/// Targets are aspirational nudges, not hard quotas.
/// </summary>
public sealed class FairnessBudget
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EventId { get; init; }
    public Guid AttendanceProposalId { get; init; }
    public Dictionary<string, double> DiversityTargets { get; set; } = [];
    public List<string> EquityPrompts { get; set; } = [];
    public Dictionary<string, double> ActualMetrics { get; set; } = [];

    public Event? Event { get; init; }
    public AttendanceProposal? AttendanceProposal { get; init; }
}
