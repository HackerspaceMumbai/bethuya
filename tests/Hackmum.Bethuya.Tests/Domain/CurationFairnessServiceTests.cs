using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using NSubstitute;

namespace Hackmum.Bethuya.Tests.Domain;

public class CurationFairnessServiceTests
{
    [Test]
    public async Task InclusionSignalsNormalizer_NormalizesGeoLanguageAndEducationBuckets()
    {
        var normalizer = new InclusionSignalsNormalizer();

        var signals = normalizer.FromSource(new AttendeeInclusionSource(
            Neighborhood: "Andheri West",
            LanguageProficiency: "English, Marathi, मराठी, Konkani",
            EducationalBackground: "Bachelor's degree",
            SocioeconomicBackground: "Lower middle class",
            GenderIdentity: "Woman"));

        await Assert.That(signals.GeoBucket).IsEqualTo(GeoBucket.MumbaiSuburban);
        await Assert.That(signals.SpeaksMarathi).IsTrue();
        await Assert.That(signals.SpeaksKonkani).IsTrue();
        await Assert.That(signals.HasLocalLanguage).IsTrue();
        await Assert.That(signals.LanguagesNormalized).Contains("marathi");
        await Assert.That(signals.LanguagesNormalized).Contains("konkani");
        await Assert.That(signals.EducationBucket).IsEqualTo(EducationBucket.Undergraduate);
        await Assert.That(signals.SocioeconomicBucket).IsEqualTo(SocioeconomicBucket.LowerMiddleClass);
        await Assert.That(signals.HasGenderDiversitySignal).IsTrue();
    }

    [Test]
    public async Task CurationFairnessService_AggregatesDimensionsAndDeficits_WithKAnonymity()
    {
        var evt = new Event
        {
            Title = "Mumbai AI",
            CreatedBy = "organizer",
            Capacity = 100,
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddHours(2),
            FairnessTargets = new EventFairnessTargets
            {
                GeoOutsideDominantMinPercent = 0.60,
                LocalLanguageMinPercent = 0.50,
                UnderrepresentedEducationMinPercent = 0.40,
                KAnonymityThreshold = 5
            }
        };

        var registrations = new List<Registration>
        {
            CreateAcceptedRegistration("A", GeoBucket.MumbaiSuburban, true, EducationBucket.SchoolOrLower),
            CreateAcceptedRegistration("B", GeoBucket.MumbaiSuburban, true, EducationBucket.DiplomaOrCertificate),
            CreateAcceptedRegistration("C", GeoBucket.MumbaiSuburban, false, EducationBucket.Undergraduate),
            CreateAcceptedRegistration("D", GeoBucket.MumbaiIslandCity, false, EducationBucket.Undergraduate),
            CreateAcceptedRegistration("E", GeoBucket.MumbaiMetropolitanRegion, false, EducationBucket.AlternativePath)
        };

        var dashboard = await new CurationFairnessService().BuildDashboardAsync(
            evt,
            registrations,
            CreateProfileRepository(),
            CreateRegistrationRepository());
        var geo = dashboard.Dimensions.Single(d => d.Dimension == "Geo diversity");
        var language = dashboard.Dimensions.Single(d => d.Dimension.StartsWith("Language diversity", StringComparison.Ordinal));
        var education = dashboard.Dimensions.Single(d => d.Dimension == "Education diversity");

        await Assert.That(geo.IsSuppressed).IsFalse();
        await Assert.That(geo.CurrentPercent).IsEqualTo(0.4d).Within(0.0001d);
        await Assert.That(geo.TargetPercent).IsEqualTo(0.6d).Within(0.0001d);
        await Assert.That(geo.NeededCount).IsEqualTo(3);

        await Assert.That(language.CurrentPercent).IsEqualTo(0.4d).Within(0.0001d);
        await Assert.That(language.NeededCount).IsEqualTo(1);

        await Assert.That(education.CurrentPercent).IsEqualTo(0.6d).Within(0.0001d);
        await Assert.That(education.NeededCount).IsEqualTo(0);
    }

    [Test]
    public async Task CurationFairnessService_GenderCoreMetric_UsesAggregateSignalOnly()
    {
        var evt = new Event
        {
            Title = "Mumbai AI",
            CreatedBy = "organizer",
            Capacity = 100,
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddHours(2),
            FairnessTargets = new EventFairnessTargets
            {
                GenderDiversityMinPercent = 0.40,
                KAnonymityThreshold = 5
            }
        };

        var registrations = new List<Registration>
        {
            CreateAcceptedRegistration("A", GeoBucket.MumbaiSuburban, true, EducationBucket.SchoolOrLower),
            CreateAcceptedRegistration("B", GeoBucket.MumbaiSuburban, true, EducationBucket.DiplomaOrCertificate),
            CreateAcceptedRegistration("C", GeoBucket.MumbaiSuburban, false, EducationBucket.Undergraduate),
            CreateAcceptedRegistration("D", GeoBucket.MumbaiIslandCity, false, EducationBucket.Undergraduate),
            CreateAcceptedRegistration("E", GeoBucket.MumbaiMetropolitanRegion, false, EducationBucket.AlternativePath)
        };
        registrations[0].InclusionSignals.HasGenderDiversitySignal = true;
        registrations[1].InclusionSignals.HasGenderDiversitySignal = true;

        var dashboard = await new CurationFairnessService().BuildDashboardAsync(
            evt,
            registrations,
            CreateProfileRepository(),
            CreateRegistrationRepository());

        await Assert.That(dashboard.GenderProgress.Dimension).IsEqualTo("gender");
        await Assert.That(dashboard.GenderProgress.IsSuppressed).IsFalse();
        await Assert.That(dashboard.GenderProgress.CurrentPercent).IsEqualTo(0.4d).Within(0.0001d);
        await Assert.That(dashboard.GenderProgress.NeededCount).IsEqualTo(0);
    }

    [Test]
    public async Task CurationFairnessService_ImpactPreview_ComputesDeltasWithoutRawSensitiveLeak()
    {
        var evt = new Event
        {
            Title = "Curation",
            CreatedBy = "organizer",
            Capacity = 50,
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddHours(1),
            FairnessTargets = new EventFairnessTargets
            {
                GeoOutsideDominantMinPercent = 0.5,
                LocalLanguageMinPercent = 0.5,
                UnderrepresentedEducationMinPercent = 0.2,
                KAnonymityThreshold = 5
            }
        };

        var registrations = new List<Registration>
        {
            CreateAcceptedRegistration("A", GeoBucket.MumbaiSuburban, false, EducationBucket.Undergraduate),
            CreateAcceptedRegistration("B", GeoBucket.MumbaiSuburban, false, EducationBucket.Undergraduate),
            CreateAcceptedRegistration("C", GeoBucket.MumbaiSuburban, false, EducationBucket.Undergraduate),
            CreateAcceptedRegistration("D", GeoBucket.MumbaiSuburban, false, EducationBucket.Undergraduate),
            new()
            {
                EventId = evt.Id,
                FullName = "Candidate",
                Email = "candidate@example.com",
                Status = RegistrationStatus.Pending,
                InclusionSignals = new InclusionSignals
                {
                    GeoBucket = GeoBucket.MumbaiMetropolitanRegion,
                    HasLocalLanguage = true,
                    SpeaksMarathi = true,
                    EducationBucket = EducationBucket.SchoolOrLower
                }
            }
        };

        var dashboard = await new CurationFairnessService().BuildDashboardAsync(
            evt,
            registrations,
            CreateProfileRepository(),
            CreateRegistrationRepository());
        var candidate = dashboard.Registrants.Single(registrant => registrant.FullName == "Candidate");

        await Assert.That(dashboard.Registrants.Count).IsEqualTo(5);
        await Assert.That(candidate.Impact.IsSuppressed).IsFalse();
        await Assert.That(candidate.Impact.DeltaPercentByDimension["geo"]).IsGreaterThan(0d);
        await Assert.That(candidate.Impact.DeltaPercentByDimension["language"]).IsGreaterThan(0d);
        await Assert.That(candidate.Impact.Explanation.Contains("disability", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(candidate.Impact.Explanation.Contains("neuro", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(candidate.Impact.Explanation).Contains("diversity");
    }

    [Test]
    public async Task CurationFairnessService_DashboardQueue_IncludesAlreadySelectedRegistrants()
    {
        var evt = new Event
        {
            Title = "Historical curation",
            CreatedBy = "organizer",
            Capacity = 30,
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddHours(1)
        };

        var accepted = CreateAcceptedRegistration("Accepted", GeoBucket.MumbaiSuburban, true, EducationBucket.Undergraduate);
        var checkedIn = CreateAcceptedRegistration("CheckedIn", GeoBucket.MumbaiIslandCity, false, EducationBucket.AlternativePath);
        checkedIn.Status = RegistrationStatus.CheckedIn;
        var rejected = CreateAcceptedRegistration("Rejected", GeoBucket.MumbaiMetropolitanRegion, false, EducationBucket.SchoolOrLower);
        rejected.Status = RegistrationStatus.Rejected;

        var dashboard = await new CurationFairnessService().BuildDashboardAsync(
            evt,
            [accepted, checkedIn, rejected],
            CreateProfileRepository(),
            CreateRegistrationRepository());

        await Assert.That(dashboard.Applicants).IsEqualTo(3);
        var visibleStatuses = dashboard.Registrants.Select(registrant => registrant.Status).ToList();
        await Assert.That(dashboard.Registrants.Count).IsEqualTo(2);
        await Assert.That(visibleStatuses).Contains("Accepted");
        await Assert.That(visibleStatuses).Contains("CheckedIn");
    }

    [Test]
    public async Task CurationFairnessService_DoesNotInferStandoutFromPositiveIntentWithoutOrganizerMark()
    {
        var evt = CreateRecommendationEvent();
        var candidate = CreatePendingCandidate(evt.Id, "candidate@example.com");
        var registrations = CreateRecommendationBaseline(evt.Id).Append(candidate).ToList();

        var dashboard = await new CurationFairnessService().BuildDashboardAsync(
            evt,
            registrations,
            CreateProfileRepository(),
            CreateRegistrationRepository());

        var recommendation = dashboard.Registrants.Single(registrant => registrant.Email == candidate.Email).Recommendation;

        await Assert.That(recommendation.Label).IsEqualTo("Strong new candidate");
        await Assert.That(recommendation.Label.Contains("standout", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(recommendation.AssessmentText).IsEqualTo("+ Strong intent\n+ Fairness gain (Geo)\n⚠️ Org concentration risk");
    }

    [Test]
    public async Task CurationFairnessService_UsesOrganizerMarkedContributionForReturningStandout()
    {
        var evt = CreateRecommendationEvent();
        var candidate = CreatePendingCandidate(evt.Id, "standout@example.com");
        var registrations = CreateRecommendationBaseline(evt.Id).Append(candidate).ToList();
        var history = new Registration
        {
            EventId = Guid.Parse("019e263d-effb-7a55-86ec-170baeee3719"),
            FullName = candidate.FullName,
            Email = candidate.Email,
            Status = RegistrationStatus.CheckedIn,
            InclusionSignals = new InclusionSignals
            {
                GeoBucket = GeoBucket.MumbaiMetropolitanRegion,
                HasLocalLanguage = true,
                SpeaksMarathi = true,
                EducationBucket = EducationBucket.SchoolOrLower,
                OrganizerMarkedStandout = true
            }
        };

        var dashboard = await new CurationFairnessService().BuildDashboardAsync(
            evt,
            registrations,
            CreateProfileRepository(),
            CreateRegistrationRepository(new Dictionary<string, IReadOnlyList<Registration>>(StringComparer.OrdinalIgnoreCase)
            {
                [candidate.Email] = [history]
            }));

        var registrant = dashboard.Registrants.Single(item => item.Email == candidate.Email);

        await Assert.That(registrant.Profile.HasOrganizerStandoutContribution).IsTrue();
        await Assert.That(registrant.Recommendation.Label).IsEqualTo("Returning standout");
    }

    private static Registration CreateAcceptedRegistration(
        string suffix,
        GeoBucket geo,
        bool hasLocalLanguage,
        EducationBucket educationBucket)
        => new()
        {
            EventId = Guid.Parse("019e263d-effb-7a55-86ec-170baeee3718"),
            FullName = $"User {suffix}",
            Email = $"user{suffix}@example.com",
            Status = RegistrationStatus.Accepted,
            InclusionSignals = new InclusionSignals
            {
                GeoBucket = geo,
                HasLocalLanguage = hasLocalLanguage,
                SpeaksMarathi = hasLocalLanguage,
                EducationBucket = educationBucket
            }
        };

    private static Event CreateRecommendationEvent()
        => new()
        {
            Title = "Recommendation curation",
            CreatedBy = "organizer",
            Capacity = 30,
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddHours(1),
            FairnessTargets = new EventFairnessTargets
            {
                GeoOutsideDominantMinPercent = 0.5,
                LocalLanguageMinPercent = 0.5,
                UnderrepresentedEducationMinPercent = 0.2,
                KAnonymityThreshold = 5
            }
        };

    private static IReadOnlyList<Registration> CreateRecommendationBaseline(Guid eventId)
        =>
        [
            CreateAcceptedRegistrationForEvent(eventId, "BaselineA"),
            CreateAcceptedRegistrationForEvent(eventId, "BaselineB"),
            CreateAcceptedRegistrationForEvent(eventId, "BaselineC"),
            CreateAcceptedRegistrationForEvent(eventId, "BaselineD")
        ];

    private static Registration CreateAcceptedRegistrationForEvent(Guid eventId, string suffix)
        => new()
        {
            EventId = eventId,
            FullName = $"User {suffix}",
            Email = $"user{suffix}@example.com",
            Status = RegistrationStatus.Accepted,
            InclusionSignals = new InclusionSignals
            {
                GeoBucket = GeoBucket.MumbaiSuburban,
                HasLocalLanguage = false,
                EducationBucket = EducationBucket.Undergraduate
            }
        };

    private static Registration CreatePendingCandidate(Guid eventId, string email)
        => new()
        {
            EventId = eventId,
            FullName = "Candidate Builder",
            Email = email,
            Status = RegistrationStatus.Pending,
            Bio = "I want to contribute to community robotics projects, help mentor first-time builders, and share practical demos.",
            InclusionSignals = new InclusionSignals
            {
                GeoBucket = GeoBucket.MumbaiMetropolitanRegion,
                HasLocalLanguage = true,
                SpeaksMarathi = true,
                EducationBucket = EducationBucket.SchoolOrLower
            }
        };

    private static IAttendeeProfileRepository CreateProfileRepository()
    {
        var repository = Substitute.For<IAttendeeProfileRepository>();
        repository.GetPublicSummariesByEmailAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
           .Returns(Task.FromResult<IReadOnlyDictionary<string, AttendeePublicSummary>>(
               new Dictionary<string, AttendeePublicSummary>(StringComparer.OrdinalIgnoreCase)));
        return repository;
    }

    private static IRegistrationRepository CreateRegistrationRepository(
        IReadOnlyDictionary<string, IReadOnlyList<Registration>>? historyByEmail = null)
    {
        var repository = Substitute.For<IRegistrationRepository>();
        repository.GetHistoricalByEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
           .Returns(Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<Registration>>>(
               historyByEmail ?? new Dictionary<string, IReadOnlyList<Registration>>(StringComparer.OrdinalIgnoreCase)));
        return repository;
    }
}
