using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Backend.Services;

public sealed class CurationFairnessService
{
    public CurationDashboardResponse BuildDashboard(
        Event evt,
        IReadOnlyList<Registration> registrations,
        IReadOnlyList<string>? curationInsights = null)
    {
        var targets = evt.FairnessTargets ?? new EventFairnessTargets();
        var selected = registrations
            .Where(r => r.Status == RegistrationStatus.Accepted || r.Status == RegistrationStatus.CheckedIn)
            .ToList();

        var dimensions = new List<FairnessDimensionProgressResponse>
        {
            BuildGeoProgress(selected, targets),
            BuildLanguageProgress(selected, targets),
            BuildEducationProgress(selected, targets)
        };

        if (targets.EnableSocioeconomicDimension && targets.UnderrepresentedSocioeconomicMinPercent is not null)
        {
            dimensions.Add(BuildSocioeconomicProgress(selected, targets));
        }

        var registrants = registrations
            .Where(r => r.Status is RegistrationStatus.Pending or RegistrationStatus.Waitlisted)
            .Select(r => new CurationRegistrantResponse(
                r.Id,
                r.FullName,
                r.Email,
                r.Status.ToString(),
                r.Interests,
                BuildImpactPreview(selected, r, targets)))
            .ToList();

        return new CurationDashboardResponse(
            EventId: evt.Id,
            Capacity: evt.Capacity,
            Applicants: registrations.Count,
            Targets: ToContract(targets),
            Dimensions: dimensions,
            Registrants: registrants,
            CurationInsights: curationInsights ?? []);
    }

    private static FairnessDimensionProgressResponse BuildGeoProgress(
        IReadOnlyCollection<Registration> selected,
        EventFairnessTargets targets)
    {
        return BuildProgress(
            dimension: "Geo diversity",
            selected: selected,
            targetPercent: targets.GeoOutsideDominantMinPercent,
            kThreshold: targets.KAnonymityThreshold,
            numeratorAndDenominatorFactory: regs =>
            {
                if (regs.Count == 0)
                {
                    return (0, 0);
                }

                var dominantBucketCount = regs
                    .GroupBy(r => r.InclusionSignals.GeoBucket)
                    .Select(g => g.Count())
                    .DefaultIfEmpty(0)
                    .Max();

                var outsideDominant = regs.Count - dominantBucketCount;
                return (outsideDominant, regs.Count);
            },
            alertLabel: "outside dominant geo bucket");
    }

    private static FairnessDimensionProgressResponse BuildLanguageProgress(
        IReadOnlyCollection<Registration> selected,
        EventFairnessTargets targets)
    {
        return BuildProgress(
            dimension: "Language diversity (Marathi/Konkani)",
            selected: selected,
            targetPercent: targets.LocalLanguageMinPercent,
            kThreshold: targets.KAnonymityThreshold,
            numeratorAndDenominatorFactory: regs =>
            {
                var numerator = regs.Count(r => r.InclusionSignals.HasLocalLanguage);
                return (numerator, regs.Count);
            },
            alertLabel: "Marathi/Konkani speakers");
    }

    private static FairnessDimensionProgressResponse BuildEducationProgress(
        IReadOnlyCollection<Registration> selected,
        EventFairnessTargets targets)
    {
        return BuildProgress(
            dimension: "Education diversity",
            selected: selected,
            targetPercent: targets.UnderrepresentedEducationMinPercent,
            kThreshold: targets.KAnonymityThreshold,
            numeratorAndDenominatorFactory: regs =>
            {
                var numerator = regs.Count(r => IsUnderrepresentedEducation(r.InclusionSignals.EducationBucket));
                return (numerator, regs.Count);
            },
            alertLabel: "underrepresented education attendees");
    }

    private static FairnessDimensionProgressResponse BuildSocioeconomicProgress(
        IReadOnlyCollection<Registration> selected,
        EventFairnessTargets targets)
    {
        return BuildProgress(
            dimension: "Socioeconomic diversity",
            selected: selected,
            targetPercent: targets.UnderrepresentedSocioeconomicMinPercent ?? 0,
            kThreshold: targets.KAnonymityThreshold,
            numeratorAndDenominatorFactory: regs =>
            {
                var numerator = regs.Count(r => IsUnderrepresentedSocioeconomic(r.InclusionSignals.SocioeconomicBucket));
                return (numerator, regs.Count);
            },
            alertLabel: "underrepresented socioeconomic attendees");
    }

    private static FairnessDimensionProgressResponse BuildProgress(
        string dimension,
        IReadOnlyCollection<Registration> selected,
        double targetPercent,
        int kThreshold,
        Func<IReadOnlyCollection<Registration>, (int Numerator, int Denominator)> numeratorAndDenominatorFactory,
        string alertLabel)
    {
        targetPercent = ClampPercent(targetPercent);
        if (selected.Count < kThreshold)
        {
            return new FairnessDimensionProgressResponse(
                Dimension: dimension,
                CurrentPercent: 0,
                TargetPercent: targetPercent,
                DeficitPercent: 0,
                NeededCount: 0,
                IsSuppressed: true,
                Alert: $"Suppressed until at least {kThreshold} selected attendees.");
        }

        var (numerator, denominator) = numeratorAndDenominatorFactory(selected);
        var current = denominator == 0 ? 0 : (double)numerator / denominator;
        var deficit = Math.Max(0, targetPercent - current);
        var needed = ComputeNeededCount(numerator, denominator, targetPercent);

        return new FairnessDimensionProgressResponse(
            Dimension: dimension,
            CurrentPercent: current,
            TargetPercent: targetPercent,
            DeficitPercent: deficit,
            NeededCount: needed,
            IsSuppressed: false,
            Alert: needed > 0 ? $"Need {needed} more {alertLabel} to meet target." : null);
    }

    private static ImpactPreviewResponse BuildImpactPreview(
        IReadOnlyList<Registration> selected,
        Registration candidate,
        EventFairnessTargets targets)
    {
        var updatedSelected = selected.Append(candidate).ToList();

        if (updatedSelected.Count < targets.KAnonymityThreshold)
        {
            return new ImpactPreviewResponse(
                DeltaPercentByDimension: new Dictionary<string, double>(),
                Explanation: $"Impact preview hidden until at least {targets.KAnonymityThreshold} attendees are selected.",
                IsSuppressed: true);
        }

        var currentGeo = BuildGeoProgress(selected, targets);
        var currentLanguage = BuildLanguageProgress(selected, targets);
        var currentEducation = BuildEducationProgress(selected, targets);

        var nextGeo = BuildGeoProgress(updatedSelected, targets);
        var nextLanguage = BuildLanguageProgress(updatedSelected, targets);
        var nextEducation = BuildEducationProgress(updatedSelected, targets);

        var deltas = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["geo"] = nextGeo.CurrentPercent - currentGeo.CurrentPercent,
            ["language"] = nextLanguage.CurrentPercent - currentLanguage.CurrentPercent,
            ["education"] = nextEducation.CurrentPercent - currentEducation.CurrentPercent
        };

        if (targets.EnableSocioeconomicDimension && targets.UnderrepresentedSocioeconomicMinPercent is not null)
        {
            var currentSocioeconomic = BuildSocioeconomicProgress(selected, targets);
            var nextSocioeconomic = BuildSocioeconomicProgress(updatedSelected, targets);
            deltas["socioeconomic"] = nextSocioeconomic.CurrentPercent - currentSocioeconomic.CurrentPercent;
        }

        var explanation = DescribeImpact(deltas);

        return new ImpactPreviewResponse(
            DeltaPercentByDimension: deltas,
            Explanation: explanation,
            IsSuppressed: false);
    }

    private static string DescribeImpact(IReadOnlyDictionary<string, double> deltas)
    {
        var best = deltas
            .OrderByDescending(kvp => kvp.Value)
            .FirstOrDefault();

        if (best.Value <= 0)
        {
            return "No positive fairness delta from approving this registrant.";
        }

        return best.Key switch
        {
            "geo" => "Improves geo diversity toward target.",
            "language" => "Improves language diversity toward Marathi/Konkani target.",
            "education" => "Improves education diversity toward target.",
            "socioeconomic" => "Improves socioeconomic diversity toward target.",
            _ => "Improves fairness budget progress."
        };
    }

    private static bool IsUnderrepresentedEducation(EducationBucket bucket)
        => bucket is EducationBucket.SchoolOrLower
            or EducationBucket.DiplomaOrCertificate
            or EducationBucket.AlternativePath;

    private static bool IsUnderrepresentedSocioeconomic(SocioeconomicBucket? bucket)
        => bucket is SocioeconomicBucket.WorkingClass or SocioeconomicBucket.LowerMiddleClass;

    private static int ComputeNeededCount(int numerator, int denominator, double targetPercent)
    {
        if (denominator <= 0 || targetPercent <= 0)
        {
            return 0;
        }

        if ((double)numerator / denominator >= targetPercent)
        {
            return 0;
        }

        if (targetPercent >= 1)
        {
            return int.MaxValue;
        }

        var needed = (targetPercent * denominator - numerator) / (1 - targetPercent);
        return Math.Max(0, (int)Math.Ceiling(needed));
    }

    private static double ClampPercent(double value)
        => value switch
        {
            < 0 => 0,
            > 1 => 1,
            _ => value
        };

    private static EventFairnessTargetsContract ToContract(EventFairnessTargets source)
        => new(
            source.GeoOutsideDominantMinPercent,
            source.LocalLanguageMinPercent,
            source.UnderrepresentedEducationMinPercent,
            source.EnableSocioeconomicDimension,
            source.UnderrepresentedSocioeconomicMinPercent,
            source.KAnonymityThreshold);
}
