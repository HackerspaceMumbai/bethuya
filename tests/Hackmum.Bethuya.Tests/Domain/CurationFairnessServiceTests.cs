using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

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
            SocioeconomicBackground: "Lower middle class"));

        await Assert.That(signals.GeoBucket).IsEqualTo(GeoBucket.MumbaiSuburban);
        await Assert.That(signals.SpeaksMarathi).IsTrue();
        await Assert.That(signals.SpeaksKonkani).IsTrue();
        await Assert.That(signals.HasLocalLanguage).IsTrue();
        await Assert.That(signals.LanguagesNormalized).Contains("marathi");
        await Assert.That(signals.LanguagesNormalized).Contains("konkani");
        await Assert.That(signals.EducationBucket).IsEqualTo(EducationBucket.Undergraduate);
        await Assert.That(signals.SocioeconomicBucket).IsEqualTo(SocioeconomicBucket.LowerMiddleClass);
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

        var dashboard = new CurationFairnessService().BuildDashboard(evt, registrations);
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

        var dashboard = new CurationFairnessService().BuildDashboard(evt, registrations);
        var candidate = dashboard.Registrants.Single();

        await Assert.That(candidate.Impact.IsSuppressed).IsFalse();
        await Assert.That(candidate.Impact.DeltaPercentByDimension["geo"]).IsGreaterThan(0d);
        await Assert.That(candidate.Impact.DeltaPercentByDimension["language"]).IsGreaterThan(0d);
        await Assert.That(candidate.Impact.Explanation.Contains("disability", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(candidate.Impact.Explanation.Contains("neuro", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(candidate.Impact.Explanation).Contains("diversity");
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
}
