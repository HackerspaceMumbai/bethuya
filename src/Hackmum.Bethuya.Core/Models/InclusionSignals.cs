using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Privacy-safe, derived inclusion signals used for fairness aggregation.
/// Raw sensitive profile answers must never be exposed through this model.
/// </summary>
public sealed class InclusionSignals
{
    public static InclusionSignals Empty { get; } = new();

    public GeoBucket GeoBucket { get; set; } = GeoBucket.Unknown;
    public List<string> LanguagesNormalized { get; set; } = [];
    public bool SpeaksMarathi { get; set; }
    public bool SpeaksKonkani { get; set; }
    public bool HasLocalLanguage { get; set; }
    public EducationBucket EducationBucket { get; set; } = EducationBucket.Unknown;
    public SocioeconomicBucket? SocioeconomicBucket { get; set; }
    public bool OrganizerMarkedStandout { get; set; }
    public bool HasGenderDiversitySignal { get; set; }
}
