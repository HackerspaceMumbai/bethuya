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

        var fairnessBudget = BuildFairnessBudget(evt, targets);
        var opportunityEngine = BuildOpportunityEngine(evt, registrants, dimensions, fairnessBudget);

        return new CurationDashboardResponse(
            EventId: evt.Id,
            EventTitle: evt.Title,
            Capacity: evt.Capacity,
            Applicants: registrations.Count,
            Targets: ToContract(targets),
            GenderProgress: genderProgress,
            Dimensions: dimensions,
            Registrants: registrants,
            CurationInsights: curationInsights ?? [],
            OpportunityEngine: opportunityEngine);
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
            GitHubRepoCount: publicSummary?.GitHubRepoCount,
            IsGitHubLinked: publicSummary?.IsGitHubLinked ?? false,
            IsLinkedInVerified: publicSummary?.IsLinkedInVerified ?? false,
            MemberSinceYear: publicSummary?.MemberSinceYear ?? registration.RegisteredAt.Year,
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
        var strongestPositiveDimension = impact.DeltaPercentByDimension
            .Where(item => item.Value > 0.0001)
            .OrderByDescending(item => item.Value)
            .Select(item => ToDimensionLabel(item.Key))
            .FirstOrDefault();

        string label;
        string tone;
        string summary;
        string? assessmentText;

        if (strongestPositiveDelta > 0.02
            && intent.Evidence is "High" or "Medium"
            && profile.HasOrganizerStandoutContribution)
        {
            label = "Returning standout";
            tone = "positive";
            summary = "Returning standout";
            assessmentText = BuildAssessmentText(
            [
                "+ Proven standout contribution",
                "+ Strong intent",
                strongestPositiveDimension is null ? null : $"+ Fairness gain ({strongestPositiveDimension})",
                "\u26a0\ufe0f Org concentration risk"
            ]);
        }
        else if (strongestPositiveDelta > 0.02 && intent.Evidence is "High" or "Medium")
        {
            label = "Strong new candidate";
            tone = "positive";
            summary = "Strong new candidate";
            assessmentText = BuildAssessmentText(
            [
                "+ Strong intent",
                strongestPositiveDimension is null ? null : $"+ Fairness gain ({strongestPositiveDimension})",
                "\u26a0\ufe0f Org concentration risk"
            ]);
        }
        else if (registration.Status == RegistrationStatus.Waitlisted
                 || reliability.HasHistory && reliability.Score < 45)
        {
            label = "Needs manual trade-off review";
            tone = "warning";
            summary = "Needs manual trade-off review";
            assessmentText = BuildAssessmentText(
            [
                $"\u26a0\ufe0f Reliability concern ({reliability.Score}/100)",
                strongestPositiveDimension is null ? "+ Review fairness impact" : $"+ Review fairness impact ({strongestPositiveDimension})",
                "\u26a0\ufe0f Human trade-off review needed"
            ]);
        }
        else
        {
            label = "Good exploratory attendee";
            tone = "neutral";
            summary = "Good exploratory attendee";
            assessmentText = BuildAssessmentText(
            [
                "+ Exploratory attendee profile",
                strongestPositiveDimension is null ? "+ Fairness impact is neutral" : $"+ Fairness gain ({strongestPositiveDimension})",
                "\u26a0\ufe0f Review alongside reliability"
            ]);
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
            Highlights: highlights.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            AssessmentText: assessmentText);
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

    private static string BuildAssessmentText(IEnumerable<string?> lines)
        => string.Join('\n', lines.Where(line => !string.IsNullOrWhiteSpace(line)));

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

    private static FairnessBudget BuildFairnessBudget(Event evt, EventFairnessTargets targets)
    {
        var diversityTargets = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["geo_outside_dominant"] = ClampPercent(targets.GeoOutsideDominantMinPercent),
            ["local_language_marathi_konkani"] = ClampPercent(targets.LocalLanguageMinPercent),
            ["education_underrepresented"] = ClampPercent(targets.UnderrepresentedEducationMinPercent),
            ["gender_diversity"] = ClampPercent(targets.GenderDiversityMinPercent)
        };

        if (targets.EnableSocioeconomicDimension && targets.UnderrepresentedSocioeconomicMinPercent is not null)
        {
            diversityTargets["socioeconomic_underrepresented"] = ClampPercent(targets.UnderrepresentedSocioeconomicMinPercent.Value);
        }

        return new FairnessBudget
        {
            EventId = evt.Id,
            DiversityTargets = diversityTargets,
            EquityPrompts =
            [
                "Curator must never auto-accept or auto-reject attendees.",
                "Use only consented derived inclusion signals and privacy-safe aggregates.",
                "Do not use disability, neurodiversity, or additional support fields for ranking."
            ]
        };
    }

    private static OpportunityEngineResponse BuildOpportunityEngine(
        Event evt,
        List<CurationRegistrantResponse> registrants,
        IReadOnlyList<FairnessDimensionProgressResponse> dimensions,
        FairnessBudget budget)
    {
        var shifts = BuildVolunteerShifts(evt);
        var roles = BuildVolunteerRoles(shifts, budget);
        var rules = BuildShiftAssignmentRules();
        var fairnessPriorityDimension = ResolveFairnessPriorityDimension(dimensions);

        var candidateSuggestions = new List<OpportunityCandidateSuggestion>(registrants.Count);
        foreach (var registrant in registrants)
        {
            var suggestion = BuildCandidateSuggestion(registrant, fairnessPriorityDimension);
            if (suggestion is not null)
            {
                candidateSuggestions.Add(suggestion);
            }
        }

        candidateSuggestions.Sort((left, right) => right.Candidate.Score.CompareTo(left.Candidate.Score));

        var conflicts = BuildOpportunityConflicts(candidateSuggestions, roles);

        IReadOnlyList<string> organizerWorkflow =
        [
            "Generate curation proposal to establish a baseline fairness cohort.",
            "Review suggested volunteer assignments with fairness deltas and reliability context.",
            "Apply organizer/curator decisions manually before publishing final assignments."
        ];

        return new OpportunityEngineResponse(
            VolunteerRoles: roles,
            VolunteerShifts: shifts,
            ShiftAssignmentRules: rules,
            Candidates: candidateSuggestions.Select(candidate => candidate.Candidate).ToList(),
            Conflicts: conflicts,
            OrganizerWorkflow: organizerWorkflow);
    }

    private static IReadOnlyList<VolunteerShiftDefinitionResponse> BuildVolunteerShifts(Event evt)
    {
        var arrivalEnd = DateTimeOffset.Compare(evt.StartDate.AddMinutes(30), evt.EndDate) < 0
            ? evt.StartDate.AddMinutes(30)
            : evt.EndDate;
        var liveStart = arrivalEnd;
        var liveEnd = DateTimeOffset.Compare(evt.EndDate.AddMinutes(-30), liveStart) > 0
            ? evt.EndDate.AddMinutes(-30)
            : evt.EndDate;
        var closeoutStart = DateTimeOffset.Compare(liveEnd, evt.EndDate) < 0
            ? liveEnd
            : evt.EndDate.AddMinutes(-15);

        return
        [
            new VolunteerShiftDefinitionResponse(
                ShiftKey: "arrival",
                Label: "Arrival & onboarding",
                StartsAt: evt.StartDate.AddMinutes(-45),
                EndsAt: arrivalEnd,
                RequiredVolunteers: 2),
            new VolunteerShiftDefinitionResponse(
                ShiftKey: "live-core",
                Label: "Live session support",
                StartsAt: liveStart,
                EndsAt: liveEnd,
                RequiredVolunteers: 2),
            new VolunteerShiftDefinitionResponse(
                ShiftKey: "closeout",
                Label: "Closeout & follow-up",
                StartsAt: closeoutStart,
                EndsAt: evt.EndDate.AddMinutes(45),
                RequiredVolunteers: 1)
        ];
    }

    private static IReadOnlyList<VolunteerRoleDefinitionResponse> BuildVolunteerRoles(
        IReadOnlyList<VolunteerShiftDefinitionResponse> shifts,
        FairnessBudget budget)
    {
        var shiftKeys = shifts.Select(shift => shift.ShiftKey).ToList();
        var fairnessKeys = budget.DiversityTargets.Keys
            .Select(ToFairnessDimensionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return
        [
            new VolunteerRoleDefinitionResponse(
                RoleKey: "welcome-desk",
                Label: "Welcome desk volunteer",
                Summary: "Greets attendees, handles check-in support, and helps first-timers settle in.",
                RequiredVolunteersPerShift: 2,
                SupportedShiftKeys: ["arrival", "live-core"],
                PreferredDimensionKeys: BuildPreferredDimensions(["geo", "language", "gender"], fairnessKeys)),
            new VolunteerRoleDefinitionResponse(
                RoleKey: "community-facilitator",
                Label: "Community facilitator",
                Summary: "Guides Q&A and session transitions with reliable event-time presence.",
                RequiredVolunteersPerShift: 1,
                SupportedShiftKeys: ["live-core", "closeout"],
                PreferredDimensionKeys: BuildPreferredDimensions(["gender", "geo"], fairnessKeys)),
            new VolunteerRoleDefinitionResponse(
                RoleKey: "build-mentor",
                Label: "Build mentor",
                Summary: "Supports project demos and mentoring during hands-on blocks.",
                RequiredVolunteersPerShift: 1,
                SupportedShiftKeys: shiftKeys,
                PreferredDimensionKeys: BuildPreferredDimensions(["education", "socioeconomic"], fairnessKeys))
        ];
    }

    private static IReadOnlyList<ShiftAssignmentRuleResponse> BuildShiftAssignmentRules()
    {
        return
        [
            new ShiftAssignmentRuleResponse(
                RuleKey: "single-role-per-shift",
                Description: "A volunteer can hold at most one role in the same shift window.",
                Severity: "blocking"),
            new ShiftAssignmentRuleResponse(
                RuleKey: "max-two-shifts-per-volunteer",
                Description: "Assignments should cap each volunteer to two shifts to avoid burnout.",
                Severity: "warning"),
            new ShiftAssignmentRuleResponse(
                RuleKey: "facilitator-needs-reliability",
                Description: "Community facilitator assignments require a reliability score of at least 45 when history exists.",
                Severity: "blocking"),
            new ShiftAssignmentRuleResponse(
                RuleKey: "fairness-priority-bias",
                Description: "When capacity is constrained, prioritize assignments that improve the top unmet fairness dimension.",
                Severity: "advisory")
        ];
    }

    private static OpportunityCandidateSuggestion? BuildCandidateSuggestion(
        CurationRegistrantResponse registrant,
        string fairnessPriorityDimension)
    {
        if (registrant.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase)
            || registrant.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var suggestedRole = ResolveSuggestedRole(registrant);
        var suggestedShift = ResolveSuggestedShift(suggestedRole, registrant);
        var score = ComputeOpportunityScore(registrant, fairnessPriorityDimension);
        var rationale = BuildCandidateRationale(registrant, fairnessPriorityDimension, suggestedRole);

        return new OpportunityCandidateSuggestion(
            new OpportunityCandidateResponse(
                RegistrationId: registrant.RegistrationId,
                FullName: registrant.FullName,
                SuggestedRoleKey: suggestedRole,
                SuggestedShiftKey: suggestedShift,
                Score: score,
                Rationale: rationale),
            registrant.Reliability.Score,
            registrant.Reliability.HasHistory);
    }

    private static List<OpportunityConflictResponse> BuildOpportunityConflicts(
        IReadOnlyList<OpportunityCandidateSuggestion> candidateSuggestions,
        IReadOnlyList<VolunteerRoleDefinitionResponse> roles)
    {
        var conflicts = new List<OpportunityConflictResponse>();
        var capacityByRole = roles.ToDictionary(
            role => role.RoleKey,
            role => role.RequiredVolunteersPerShift,
            StringComparer.OrdinalIgnoreCase);

        var groupedSuggestions = candidateSuggestions
            .GroupBy(candidate => (candidate.Candidate.SuggestedRoleKey, candidate.Candidate.SuggestedShiftKey));

        foreach (var group in groupedSuggestions)
        {
            var entries = group.OrderByDescending(candidate => candidate.Candidate.Score).ToList();
            var roleKey = entries[0].Candidate.SuggestedRoleKey;
            var shiftKey = entries[0].Candidate.SuggestedShiftKey;
            var capacity = capacityByRole.TryGetValue(roleKey, out var value) ? value : 1;

            foreach (var overflow in entries.Skip(capacity))
            {
                conflicts.Add(new OpportunityConflictResponse(
                    ConflictKey: "capacity-overflow",
                    RegistrationId: overflow.Candidate.RegistrationId,
                    RoleKey: roleKey,
                    ShiftKey: shiftKey,
                    Severity: "warning",
                    Message: $"{overflow.Candidate.FullName} exceeds {roleKey} capacity for {shiftKey}; manual reassignment required."));
            }
        }

        foreach (var candidate in candidateSuggestions.Where(candidate =>
                     candidate.Candidate.SuggestedRoleKey.Equals("community-facilitator", StringComparison.OrdinalIgnoreCase)
                     && candidate.HasReliabilityHistory
                     && candidate.ReliabilityScore < 45))
        {
            conflicts.Add(new OpportunityConflictResponse(
                ConflictKey: "facilitator-low-reliability",
                RegistrationId: candidate.Candidate.RegistrationId,
                RoleKey: candidate.Candidate.SuggestedRoleKey,
                ShiftKey: candidate.Candidate.SuggestedShiftKey,
                Severity: "blocking",
                Message: $"{candidate.Candidate.FullName} has reliability {candidate.ReliabilityScore}/100, below facilitator threshold."));
        }

        return conflicts;
    }

    private static string ResolveSuggestedRole(CurationRegistrantResponse registrant)
    {
        if (registrant.Intent.Signals.Contains("Builder intent", StringComparer.OrdinalIgnoreCase))
        {
            return "build-mentor";
        }

        if (registrant.Reliability.HasHistory
            && registrant.Reliability.Score >= 70
            && registrant.Intent.Evidence is "High" or "Medium")
        {
            return "community-facilitator";
        }

        return "welcome-desk";
    }

    private static string ResolveSuggestedShift(string roleKey, CurationRegistrantResponse registrant)
    {
        return roleKey switch
        {
            "community-facilitator" => registrant.Reliability.HasHistory && registrant.Reliability.Score >= 85
                ? "live-core"
                : "closeout",
            "build-mentor" => "live-core",
            _ => "arrival"
        };
    }

    private static double ComputeOpportunityScore(CurationRegistrantResponse registrant, string fairnessPriorityDimension)
    {
        var reliabilityWeight = registrant.Reliability.HasHistory
            ? registrant.Reliability.Score * 0.25
            : 12;
        var intentWeight = registrant.Intent.Evidence switch
        {
            "High" => 20,
            "Medium" => 12,
            _ => 6
        };
        var fairnessLift = registrant.Impact.DeltaPercentByDimension.Values
            .Where(delta => delta > 0)
            .DefaultIfEmpty(0)
            .Max() * 100;
        var priorityLift = registrant.Impact.DeltaPercentByDimension.TryGetValue(fairnessPriorityDimension, out var delta)
            ? Math.Max(0, delta) * 100
            : 0;
        var standoutBonus = registrant.Profile.HasOrganizerStandoutContribution ? 10 : 0;
        var cautionPenalty = registrant.Reliability.HasHistory && registrant.Reliability.Score < 45 ? 18 : 0;

        var rawScore = 25 + reliabilityWeight + intentWeight + fairnessLift + (priorityLift * 0.5) + standoutBonus - cautionPenalty;
        return Math.Clamp(Math.Round(rawScore, 2, MidpointRounding.AwayFromZero), 0, 100);
    }

    private static List<string> BuildCandidateRationale(
        CurationRegistrantResponse registrant,
        string fairnessPriorityDimension,
        string roleKey)
    {
        var rationale = new List<string>();

        if (registrant.Intent.Signals.Count > 0)
        {
            rationale.Add(registrant.Intent.Signals[0]);
        }

        if (registrant.Reliability.HasHistory)
        {
            rationale.Add($"Reliability {registrant.Reliability.Score}/100");
        }
        else
        {
            rationale.Add("No prior reliability history");
        }

        if (registrant.Impact.DeltaPercentByDimension.TryGetValue(fairnessPriorityDimension, out var priorityDelta)
            && priorityDelta > 0)
        {
            rationale.Add($"{ToDimensionLabel(fairnessPriorityDimension)} lift {ToSignedPercent(priorityDelta)}");
        }

        if (registrant.Profile.IsFirstTimer)
        {
            rationale.Add("First-timer inclusion");
        }

        rationale.Add($"Suggested for {roleKey}");
        return rationale.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string ResolveFairnessPriorityDimension(IReadOnlyList<FairnessDimensionProgressResponse> dimensions)
    {
        var highestDeficit = dimensions
            .Where(dimension => !dimension.IsSuppressed)
            .OrderByDescending(dimension => dimension.DeficitPercent)
            .FirstOrDefault();

        if (highestDeficit is null || highestDeficit.DeficitPercent <= 0)
        {
            return "geo";
        }

        return highestDeficit.Dimension switch
        {
            var value when value.StartsWith("Geo", StringComparison.OrdinalIgnoreCase) => "geo",
            var value when value.StartsWith("Language", StringComparison.OrdinalIgnoreCase) => "language",
            var value when value.StartsWith("Education", StringComparison.OrdinalIgnoreCase) => "education",
            var value when value.StartsWith("Socioeconomic", StringComparison.OrdinalIgnoreCase) => "socioeconomic",
            var value when value.StartsWith("Gender", StringComparison.OrdinalIgnoreCase) => "gender",
            _ => "geo"
        };
    }

    private static IReadOnlyList<string> BuildPreferredDimensions(
        IReadOnlyList<string> defaults,
        List<string> available)
    {
        var preferred = defaults
            .Where(item => available.Contains(item, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (preferred.Count > 0)
        {
            return preferred;
        }

        return available.Count > 0 ? available : defaults;
    }

    private static string ToFairnessDimensionKey(string key)
        => key switch
        {
            "geo_outside_dominant" => "geo",
            "local_language_marathi_konkani" => "language",
            "education_underrepresented" => "education",
            "gender_diversity" => "gender",
            "socioeconomic_underrepresented" => "socioeconomic",
            _ => key
        };

    private sealed record OpportunityCandidateSuggestion(
        OpportunityCandidateResponse Candidate,
        int ReliabilityScore,
        bool HasReliabilityHistory);

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
