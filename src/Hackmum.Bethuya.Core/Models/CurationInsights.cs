namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Explainable curation signals — never opaque scoring.
/// All data is self-reported and consented.
/// </summary>
public sealed class CurationInsights
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid AttendanceProposalId { get; init; }
    public Dictionary<Guid, double> ThemeAlignmentScores { get; set; } = [];
    public List<string> DEINudges { get; set; } = [];
    public List<string> OverRepresentationAlerts { get; set; } = [];
    public List<string> CommunitySignals { get; set; } = [];
    public List<string> FirstComeSignals { get; set; } = [];
}
