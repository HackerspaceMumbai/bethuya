using System.Globalization;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;

namespace Hackmum.Bethuya.Backend.Services;

public sealed class CurationFairnessService
{
    public async Task<CurationDashboardResponse> BuildDashboardAsync(
        Event evt,
        IReadOnlyList<Registration> registrations,
        IAttendeeProfileRepository attendeeProfileRepository,
        IRegistrationRepository registrationRepository,
        IReadOnlyList<string>? curationInsights = null,
        CancellationToken ct = default)
    {
        var targets = evt.FairnessTargets ?? new EventFairnessTargets();
        var selected = registrations
            .Where(r => r.Status is RegistrationStatus.Accepted or RegistrationStatus.CheckedIn)
            .ToList();
        var genderProgress = BuildGenderProgress(selected, targets);

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

        var curationRegistrants = registrations
            .Where(r => r.Status is not (RegistrationStatus.Rejected or RegistrationStatus.Cancelled))
            .ToList();

        var emails = curationRegistrants
            .Select(registration => registration.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var publicSummariesByEmail = await attendeeProfileRepository.GetPublicSummariesByEmailAsync(emails, ct);
        var historyByEmail = await registrationRepository.GetHistoricalByEmailsAsync(emails, evt.Id, ct);

        var registrants = curationRegistrants
            .Select(registration =>
            {
                publicSummariesByEmail.TryGetValue(registration.Email, out var publicSummary);
                historyByEmail.TryGetValue(registration.Email, out var history);
                var comparisonSelection = selected
                    .Where(selectedRegistration => selectedRegistration.Id != registration.Id)
                    .ToList();

                return BuildRegistrant(
                    registration,
                    comparisonSelection,
                    targets,
                    publicSummary,
                    history ?? [],
                    dimensions);
            })
            .ToList();

        return new CurationDashboardResponse(
            EventId: evt.Id,
            EventTitle: evt.Title,
            Capacity: evt.Capacity,
            Applicants: registrations.Count,
            Targets: ToContract(targets),
            GenderProgress: genderProgress,
            Dimensions: dimensions,
            Registrants: registrants,
            CurationInsights: curationInsights ?? []);
    }

    private static CurationRegistrantResponse BuildRegistrant(
        Registration registration,
        IReadOnlyList<Registration> selected,
        EventFairnessTargets targets,
        AttendeePublicSummary? publicSummary,
        IReadOnlyList<Registration> history,
        IReadOnlyList<FairnessDimensionProgressResponse> dimensions)
    {
        var impact = BuildImpactPreview(selected, registration, targets);
        var profile = BuildProfileSummary(registration, publicSummary, history);
        var reliability = BuildReliability(history);
        var intent = BuildIntentInsight(registration, impact);
        var recommendation = BuildRecommendation(registration, impact, profile, reliability, intent, dimensions);

        return new CurationRegistrantResponse(
            RegistrationId: registration.Id,
            FullName: registration.FullName,
            Email: registration.Email,
            Status: registration.Status.ToString(),
            RegisteredAt: registration.RegisteredAt,
            Bio: registration.Bio,
            Interests: registration.Interests,
            Profile: profile,
            Reliability: reliability,
            Intent: intent,
            Recommendation: recommendation,
            Impact: impact);
    }

    private static CurationProfileSummaryResponse BuildProfileSummary(
        Registration registration,
        AttendeePublicSummary? publicSummary,
        IReadOnlyList<Registration> history)
    {
        var pastAcceptedCount = history.Count(r => r.Status is RegistrationStatus.Accepted or RegistrationStatus.CheckedIn);
        var pastAttendedCount = history.Count(r => r.Status == RegistrationStatus.CheckedIn);
        var isFirstTimer = pastAcceptedCount == 0;
        var hasOrganizerStandoutContribution = history.Any(r => r.InclusionSignals.OrganizerMarkedStandout);

        var headline = ResolveHeadline(publicSummary);
        var organization = ResolveOrganization(publicSummary, registration.Email);
        var tags = new List<string>();

        if (isFirstTimer)
        {
            tags.Add("First timer");
        }
        else
        {
            tags.Add($"{pastAttendedCount} attended");
        }

        foreach (var interest in registration.Interests
                     .Where(interest => !string.IsNullOrWhiteSpace(interest))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Take(2))
        {
            tags.Add(interest);
        }

        var historyLabel = isFirstTimer
            ? "No prior Bethuya attendance history"
            : $"{pastAcceptedCount} prior approvals across community events";

        return new CurationProfileSummaryResponse(
            Headline: headline,
            Organization: organization,
            HistoryLabel: historyLabel,
            IsFirstTimer: isFirstTimer,
            PastAcceptedCount: pastAcceptedCount,
            PastAttendedCount: pastAttendedCount,
            HasOrganizerStandoutContribution: hasOrganizerStandoutContribution,
            Tags: tags);
    }

    private static CurationReliabilityResponse BuildReliability(IReadOnlyList<Registration> history)
    {
        var priorAccepted = history.Count(r => r.Status is RegistrationStatus.Accepted or RegistrationStatus.CheckedIn);
        var priorAttended = history.Count(r => r.Status == RegistrationStatus.CheckedIn);

        if (priorAccepted == 0)
        {
            return new CurationReliabilityResponse(
                HasHistory: false,
                Score: 0,
                Label: "Unscored",
                Summary: "No prior attendance or RSVP data linked yet.");
        }

        var score = (int)Math.Round((double)priorAttended / priorAccepted * 100, MidpointRounding.AwayFromZero);
        var label = score switch
        {
            >= 90 => "Excellent",
            >= 70 => "Stable",
            >= 45 => "Mixed",
            _ => "Needs review"
        };

        var summary = priorAttended == priorAccepted
            ? $"Attended all {priorAttended} prior approved events."
            : $"Attended {priorAttended} of {priorAccepted} prior approved events.";

        return new CurationReliabilityResponse(
            HasHistory: true,
            Score: score,
            Label: label,
            Summary: summary);
    }

    private static CurationIntentInsightResponse BuildIntentInsight(
        Registration registration,
        ImpactPreviewResponse impact)
    {
        var summary = !string.IsNullOrWhiteSpace(registration.Bio)
            ? registration.Bio!.Trim()
            : registration.Interests.Count > 0
                ? $"Interested in {string.Join(", ", registration.Interests.Take(3))} and wants to contribute to the event."
                : "No written intent provided yet; review interests and fairness impact together.";

        var lowerSummary = summary.ToLowerInvariant();
        var specificityScore = summary.Length >= 90 || registration.Interests.Count >= 3 ? 2 : summary.Length >= 45 ? 1 : 0;
        var evidenceScore = lowerSummary.Contains("build", StringComparison.Ordinal)
                            || lowerSummary.Contains("project", StringComparison.Ordinal)
                            || lowerSummary.Contains("research", StringComparison.Ordinal)
                            || lowerSummary.Contains("mentor", StringComparison.Ordinal)
                            || lowerSummary.Contains("community", StringComparison.Ordinal)
            ? 2
            : registration.Interests.Count >= 2 ? 1 : 0;
        var authenticityScore = !string.IsNullOrWhiteSpace(registration.Bio)
                                && (lowerSummary.Contains("i ", StringComparison.Ordinal)
                                    || lowerSummary.StartsWith("interested", StringComparison.Ordinal)
                                    || lowerSummary.Contains("want", StringComparison.Ordinal))
            ? 2
            : !string.IsNullOrWhiteSpace(registration.Bio) ? 1 : 0;

        var signals = new List<string>();

        if (lowerSummary.Contains("build", StringComparison.Ordinal) || lowerSummary.Contains("project", StringComparison.Ordinal))
        {
            signals.Add("Builder intent");
        }

        if (lowerSummary.Contains("community", StringComparison.Ordinal)
            || lowerSummary.Contains("collabor", StringComparison.Ordinal)
            || lowerSummary.Contains("network", StringComparison.Ordinal))
        {
            signals.Add("Community intent");
        }

        if (impact.DeltaPercentByDimension.Values.Any(value => value > 0.0001))
        {
            signals.Add("Fairness lift detected");
        }

        if (signals.Count == 0)
        {
            signals.Add("Manual review recommended");
        }

        return new CurationIntentInsightResponse(
            Summary: summary,
            Specificity: ToSignalLevel(specificityScore),
            Evidence: ToSignalLevel(evidenceScore),
            Authenticity: ToSignalLevel(authenticityScore),
            Signals: signals,
            Interpretation: BuildInterpretation(summary, signals));
    }

    private static CurationRecommendationResponse BuildRecommendation(
        Registration registration,
        ImpactPreviewResponse impact,
        CurationProfileSummaryResponse profile,
        CurationReliabilityResponse reliability,
        CurationIntentInsightResponse intent,
        IReadOnlyList<FairnessDimensionProgressResponse> dimensions)
    {
        var strongestPositiveDelta = impact.DeltaPercentByDimension.Values.DefaultIfEmpty(0).Max();
        var deficits = dimensions
            .Where(dimension => !dimension.IsSuppressed && dimension.DeficitPercent > 0)
            .OrderByDescending(dimension => dimension.DeficitPercent)
            .ToList();

        string label;
        string tone;
        string summary;

        if (strongestPositiveDelta > 0.02
            && intent.Evidence is "High" or "Medium"
            && profile.HasOrganizerStandoutContribution)
        {
            label = "Returning standout";
            tone = "positive";
            summary = "Organizer-marked contribution in a past meetup plus current fairness impact make this a standout returning candidate for human review.";
        }
        else if (strongestPositiveDelta > 0.02 && intent.Evidence is "High" or "Medium")
        {
            label = "Strong new candidate";
            tone = "positive";
            summary = "Concrete intent signal detected. Review alongside fairness impact, but no organizer-marked standout contribution is linked yet.";
        }
        else if (registration.Status == RegistrationStatus.Waitlisted
                 || reliability.HasHistory && reliability.Score < 45)
        {
            label = "Needs manual trade-off review";
            tone = "warning";
            summary = "Past follow-through or current review state suggests a closer organizer review before approving.";
        }
        else
        {
            label = "Good exploratory attendee";
            tone = "neutral";
            summary = "Fair candidate to review with queue context, intent signal, and cohort balance together.";
        }

        var highlights = new List<string>();

        foreach (var delta in impact.DeltaPercentByDimension
                     .Where(item => Math.Abs(item.Value) > 0.0001)
                     .OrderByDescending(item => item.Value)
                     .Take(3))
        {
            highlights.Add($"{ToDimensionLabel(delta.Key)} {ToSignedPercent(delta.Value)}");
        }

        if (highlights.Count == 0 && deficits.Count > 0)
        {
            highlights.Add($"Watch {deficits[0].Dimension.ToLowerInvariant()} gap");
        }

        if (reliability.HasHistory)
        {
            highlights.Add($"Reliability {reliability.Score}/100");
        }
        else
        {
            highlights.Add("No prior RSVP history");
        }

        return new CurationRecommendationResponse(
            Label: label,
            Tone: tone,
            Summary: summary,
            Highlights: highlights.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
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

    private static FairnessDimensionProgressResponse BuildGenderProgress(
        IReadOnlyCollection<Registration> selected,
        EventFairnessTargets targets)
    {
        return BuildProgress(
            dimension: "gender",
            selected: selected,
            targetPercent: targets.GenderDiversityMinPercent,
            kThreshold: targets.KAnonymityThreshold,
            numeratorAndDenominatorFactory: regs =>
            {
                var numerator = regs.Count(r => r.InclusionSignals.HasGenderDiversitySignal);
                return (numerator, regs.Count);
            },
            alertLabel: "consented gender diversity signals");
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
        var currentGender = BuildGenderProgress(selected, targets);
        var currentLanguage = BuildLanguageProgress(selected, targets);
        var currentEducation = BuildEducationProgress(selected, targets);

        var nextGeo = BuildGeoProgress(updatedSelected, targets);
        var nextGender = BuildGenderProgress(updatedSelected, targets);
        var nextLanguage = BuildLanguageProgress(updatedSelected, targets);
        var nextEducation = BuildEducationProgress(updatedSelected, targets);

        var deltas = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["geo"] = nextGeo.CurrentPercent - currentGeo.CurrentPercent,
            ["gender"] = nextGender.CurrentPercent - currentGender.CurrentPercent,
            ["language"] = nextLanguage.CurrentPercent - currentLanguage.CurrentPercent,
            ["education"] = nextEducation.CurrentPercent - currentEducation.CurrentPercent
        };

        if (targets.EnableSocioeconomicDimension && targets.UnderrepresentedSocioeconomicMinPercent is not null)
        {
            var currentSocioeconomic = BuildSocioeconomicProgress(selected, targets);
            var nextSocioeconomic = BuildSocioeconomicProgress(updatedSelected, targets);
            deltas["socioeconomic"] = nextSocioeconomic.CurrentPercent - currentSocioeconomic.CurrentPercent;
        }

        return new ImpactPreviewResponse(
            DeltaPercentByDimension: deltas,
            Explanation: DescribeImpact(deltas),
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

    private static string ResolveHeadline(AttendeePublicSummary? publicSummary)
    {
        if (string.IsNullOrWhiteSpace(publicSummary?.OccupationStatus))
        {
            return "Community participant";
        }

        return publicSummary.OccupationStatus.Trim() switch
        {
            "Employee" => "Working professional",
            "Student" => "Student attendee",
            "Freelancer" => "Independent builder",
            var value => value
        };
    }

    private static string ResolveOrganization(AttendeePublicSummary? publicSummary, string email)
    {
        if (!string.IsNullOrWhiteSpace(publicSummary?.CompanyName))
        {
            return publicSummary.CompanyName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(publicSummary?.EducationInstitute))
        {
            return publicSummary.EducationInstitute.Trim();
        }

        var atIndex = email.IndexOf('@');
        if (atIndex < 0 || atIndex == email.Length - 1)
        {
            return "Community network";
        }

        var domain = email[(atIndex + 1)..];
        var primaryLabel = domain.Split('.')[0];
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(primaryLabel.Replace('-', ' '));
    }

    private static string BuildInterpretation(string summary, List<string> signals)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return "Review interests and fairness impact together before making a human decision.";
        }

        return signals[0] switch
        {
            "Builder intent" => "Specific examples and hands-on motivation suggest a contributing attendee profile.",
            "Community intent" => "Collaborative language suggests this person can add community value beyond simple attendance.",
            "Fairness lift detected" => "Intent is modest, but approving this person would improve at least one fairness dimension.",
            _ => "Review intent, reliability, and fairness impact together before making a human decision."
        };
    }

    private static string ToSignalLevel(int score) => score switch
    {
        >= 2 => "High",
        1 => "Medium",
        _ => "Low"
    };

    private static string ToDimensionLabel(string key) => key switch
    {
        "geo" => "Geo",
        "gender" => "Gender",
        "language" => "Language",
        "education" => "Education",
        "socioeconomic" => "Socioeconomic",
        _ => key
    };

    private static string ToSignedPercent(double value)
    {
        var sign = value >= 0 ? "+" : string.Empty;
        return $"{sign}{value * 100:F1}%";
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
            GeoOutsideDominantMinPercent: source.GeoOutsideDominantMinPercent,
            LocalLanguageMinPercent: source.LocalLanguageMinPercent,
            UnderrepresentedEducationMinPercent: source.UnderrepresentedEducationMinPercent,
            EnableSocioeconomicDimension: source.EnableSocioeconomicDimension,
            UnderrepresentedSocioeconomicMinPercent: source.UnderrepresentedSocioeconomicMinPercent,
            KAnonymityThreshold: source.KAnonymityThreshold,
            GenderDiversityMinPercent: source.GenderDiversityMinPercent);
}
