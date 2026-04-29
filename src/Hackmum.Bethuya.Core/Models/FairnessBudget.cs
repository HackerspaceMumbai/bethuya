namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Fairness-driven attendee selection scoring system.
/// Ensures diverse, balanced event attendance within venue capacity constraints.
/// Targets are aspirational nudges, not hard quotas.
/// </summary>
public sealed class FairnessBudget
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EventId { get; init; }
    public Guid AttendanceProposalId { get; init; }
    
    public int TotalCapacity { get; set; }
    public int AcceptanceCount { get; set; }
    public int WaitlistCount { get; set; }
    
    public Dictionary<string, double> DiversityTargets { get; set; } = [];
    public List<string> EquityPrompts { get; set; } = [];
    public Dictionary<string, double> ActualMetrics { get; set; } = [];

    public decimal ThemeAlignmentWeight { get; set; } = 0.3m;
    public decimal DeiDiversityWeight { get; set; } = 0.4m;
    public decimal AttendanceHistoryWeight { get; set; } = 0.3m;

    public Event? Event { get; init; }
    public AttendanceProposal? AttendanceProposal { get; init; }

    /// <summary>
    /// Score a registrant for fairness-based selection.
    /// Returns score between 0-100 (higher = more likely to accept).
    /// </summary>
    public decimal ScoreRegistrant(
        Registration registrant,
        List<Registration> otherRegistrants,
        object? priorEventContext = null)
    {
        decimal score = 0;

        // 1. Theme Alignment (30%)
        var themeScore = CalculateThemeAlignment(registrant, priorEventContext);
        score += themeScore * ThemeAlignmentWeight;

        // 2. DEI Diversity (40%)
        var deiScore = CalculateDeiDiversity(registrant, otherRegistrants);
        score += deiScore * DeiDiversityWeight;

        // 3. Attendance History (30%)
        var historyScore = CalculateAttendanceHistory(registrant);
        score += historyScore * AttendanceHistoryWeight;

        return Math.Min(score, 100);
    }

    private static decimal CalculateThemeAlignment(
        Registration registrant,
        object? priorEventContext)
    {
        // Compare registrant interests with event theme
        // Return 0-100 score based on overlap
        if (registrant.Interests == null || registrant.Interests.Count == 0)
            return 50; // Neutral if no interests stated

        // For now, return neutral score; priorEventContext can be used in future enhancements
        return 50;
    }

    private static decimal CalculateDeiDiversity(
        Registration registrant,
        List<Registration> otherRegistrants)
    {
        // Check if this registrant's DEI attributes are underrepresented
        // in current acceptance list. Boost score if underrepresented.
        decimal score = 50; // Baseline neutral
        return Math.Max(0, Math.Min(score, 100));
    }

    private static decimal CalculateAttendanceHistory(Registration registrant)
    {
        // Prior attendees get slight boost (0-10 points)
        return 50; // Neutral baseline for now
    }

    /// <summary>
    /// Rank attendees for curation selection.
    /// Returns sorted list: (Registrant, Score).
    /// </summary>
    public List<(Registration, decimal)> RankForSelection(
        List<Registration> registrants,
        object? priorEventContext = null)
    {
        var ranked = registrants
            .Select(r => (r, ScoreRegistrant(r, registrants, priorEventContext)))
            .OrderByDescending(x => x.Item2)
            .ThenBy(_ => Guid.NewGuid()) // Tiebreaker: random for equal scores
            .ToList();

        return ranked;
    }
}
