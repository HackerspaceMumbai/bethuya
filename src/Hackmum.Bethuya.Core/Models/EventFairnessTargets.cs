namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Organizer-configurable fairness target settings for event curation.
/// </summary>
public sealed class EventFairnessTargets
{
    public const int DefaultKAnonymityThreshold = 5;

    public static EventFairnessTargets Default { get; } = new();

    /// <summary>Target minimum ratio of selected attendees outside the dominant geo bucket.</summary>
    public double GeoOutsideDominantMinPercent { get; set; } = 0.35;

    /// <summary>Target minimum ratio of selected attendees who speak Marathi and/or Konkani.</summary>
    public double LocalLanguageMinPercent { get; set; } = 0.25;

    /// <summary>Target minimum ratio of selected attendees from underrepresented education buckets.</summary>
    public double UnderrepresentedEducationMinPercent { get; set; } = 0.25;

    /// <summary>Whether socioeconomic dimension is enabled for this event.</summary>
    public bool EnableSocioeconomicDimension { get; set; }

    /// <summary>Optional target minimum ratio for underrepresented socioeconomic buckets.</summary>
    public double? UnderrepresentedSocioeconomicMinPercent { get; set; }

    /// <summary>Target minimum ratio for consented gender diversity signals in the selected cohort.</summary>
    public double GenderDiversityMinPercent { get; set; } = 0.40;

    /// <summary>k-anonymity threshold for fairness breakdown visibility.</summary>
    public int KAnonymityThreshold { get; set; } = DefaultKAnonymityThreshold;
}
