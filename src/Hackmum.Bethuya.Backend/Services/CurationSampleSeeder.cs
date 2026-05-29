using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

public sealed partial class CurationSampleSeeder(
    BethuyaDbContext dbContext,
    InclusionSignalsNormalizer inclusionSignalsNormalizer,
    TimeProvider timeProvider,
    ILogger<CurationSampleSeeder> logger)
{
    private const string SeedCreator = "seed-curation@hackerspacemumbai.dev";
    private const int SandboxCapacity = 25;
    private const int MaxReviewableRegistrants = 200;

    public async Task<CurationSeedResult> SeedAsync(int reviewableCount = 50, CancellationToken ct = default)
    {
        reviewableCount = Math.Clamp(reviewableCount, SandboxCapacity + 1, MaxReviewableRegistrants);

        var now = timeProvider.GetUtcNow();
        var seedRunSlug = $"{now:MMddHHmmss}-{Guid.CreateVersion7().ToString("N")[..6]}";
        var currentEvent = new Event
        {
            Title = $"Curation Sandbox {now:dd MMM HH:mm}",
            Description = "Generated sandbox event for exercising curation fairness, queue, and reliability edge cases.",
            Type = EventType.Conference,
            Status = EventStatus.RegistrationOpen,
            Capacity = SandboxCapacity,
            StartDate = now.AddDays(18),
            EndDate = now.AddDays(18).AddHours(6),
            Location = "Hackerspace Mumbai",
            Hashtag = $"curation-sandbox-{seedRunSlug}",
            CreatedBy = SeedCreator,
            FairnessTargets = new EventFairnessTargets
            {
                GeoOutsideDominantMinPercent = 0.36,
                LocalLanguageMinPercent = 0.30,
                UnderrepresentedEducationMinPercent = 0.28,
                EnableSocioeconomicDimension = true,
                UnderrepresentedSocioeconomicMinPercent = 0.18,
                KAnonymityThreshold = 5
            }
        };

        var historyEventOne = new Event
        {
            Title = $"Community Systems Night {now.AddMonths(-4):MMM yyyy}",
            Description = "Historical event used for seeded curation attendance history.",
            Type = EventType.Meetup,
            Status = EventStatus.Completed,
            Capacity = 40,
            StartDate = now.AddMonths(-4).AddDays(-5),
            EndDate = now.AddMonths(-4).AddDays(-5).AddHours(3),
            Location = "Mumbai",
            Hashtag = $"systems-night-{seedRunSlug}",
            CreatedBy = SeedCreator
        };

        var historyEventTwo = new Event
        {
            Title = $"Open Hardware Jam {now.AddMonths(-2):MMM yyyy}",
            Description = "Historical event used for seeded curation attendance history.",
            Type = EventType.Workshop,
            Status = EventStatus.Completed,
            Capacity = 35,
            StartDate = now.AddMonths(-2).AddDays(-7),
            EndDate = now.AddMonths(-2).AddDays(-7).AddHours(4),
            Location = "Mumbai",
            Hashtag = $"hardware-jam-{seedRunSlug}",
            CreatedBy = SeedCreator
        };

        dbContext.Events.AddRange(historyEventOne, historyEventTwo, currentEvent);

        var acceptedCurrentRegistrants = new List<Registration>();
        var reviewableRegistrants = new List<Registration>();
        var historicalRegistrants = new List<Registration>();
        var attendeeProfiles = new List<AttendeeProfile>();

        for (var index = 0; index < 8; index++)
        {
            var persona = SeedPersonas[index % SeedPersonas.Length];
            var identity = CreateIdentity(index, "selected", seedRunSlug);
            var source = persona.ToSource(index);

            attendeeProfiles.Add(CreateProfile(identity, persona, source));
            acceptedCurrentRegistrants.Add(CreateRegistration(
                currentEvent.Id,
                identity,
                persona,
                source,
                index % 3 == 0 ? RegistrationStatus.CheckedIn : RegistrationStatus.Accepted,
                now.AddDays(-(20 - index))));
        }

        for (var index = 0; index < reviewableCount; index++)
        {
            var persona = SeedPersonas[index % SeedPersonas.Length];
            var identity = CreateIdentity(index, "review", seedRunSlug);
            var source = persona.ToSource(index);

            attendeeProfiles.Add(CreateProfile(identity, persona, source));
            reviewableRegistrants.Add(CreateRegistration(
                currentEvent.Id,
                identity,
                persona,
                source,
                index % 6 == 0 ? RegistrationStatus.Waitlisted : RegistrationStatus.Pending,
                now.AddDays(-(index % 9 + 1)),
                index));

            historicalRegistrants.AddRange(CreateHistoricalRegistrations(
                identity,
                persona,
                historyEventOne.Id,
                historyEventTwo.Id,
                now,
                index));
        }

        dbContext.AttendeeProfiles.AddRange(attendeeProfiles);
        dbContext.Registrations.AddRange(acceptedCurrentRegistrants);
        dbContext.Registrations.AddRange(reviewableRegistrants);
        dbContext.Registrations.AddRange(historicalRegistrants);

        await dbContext.SaveChangesAsync(ct);

        LogSeedCompleted(
            logger,
            currentEvent.Id,
            reviewableRegistrants.Count,
            acceptedCurrentRegistrants.Count,
            historicalRegistrants.Count);

        return new CurationSeedResult(
            currentEvent.Id,
            currentEvent.Title,
            reviewableRegistrants.Count,
            acceptedCurrentRegistrants.Count,
            $"/curation/{currentEvent.Id}");
    }

    private List<Registration> CreateHistoricalRegistrations(
        SeedIdentity identity,
        SeedPersona persona,
        Guid historyEventOneId,
        Guid historyEventTwoId,
        DateTimeOffset now,
        int index)
    {
        var source = persona.ToSource(index + 3);
        var registrations = new List<Registration>();
        var historyPattern = index % 5;

        if (historyPattern is 1 or 3)
        {
            registrations.Add(CreateHistoricalRegistration(
                historyEventOneId,
                identity,
                persona,
                source,
                RegistrationStatus.CheckedIn,
                now.AddMonths(-4).AddDays(index % 6),
                organizerMarkedStandout: historyPattern == 3));
        }

        if (historyPattern is 2 or 3)
        {
            registrations.Add(CreateHistoricalRegistration(
                historyEventTwoId,
                identity,
                persona,
                source,
                historyPattern == 2 ? RegistrationStatus.Accepted : RegistrationStatus.CheckedIn,
                now.AddMonths(-2).AddDays(index % 8),
                organizerMarkedStandout: false));
        }

        if (historyPattern == 4)
        {
            registrations.Add(CreateHistoricalRegistration(
                historyEventOneId,
                identity,
                persona,
                source,
                RegistrationStatus.Cancelled,
                now.AddMonths(-4).AddDays(index % 4),
                organizerMarkedStandout: false));
        }

        return registrations;
    }

    private Registration CreateHistoricalRegistration(
        Guid eventId,
        SeedIdentity identity,
        SeedPersona persona,
        AttendeeInclusionSource source,
        RegistrationStatus status,
        DateTimeOffset registeredAt,
        bool organizerMarkedStandout)
    {
        var registration = CreateRegistration(eventId, identity, persona, source, status, registeredAt, null);
        registration.InclusionSignals.OrganizerMarkedStandout = organizerMarkedStandout;
        return registration;
    }

    private Registration CreateRegistration(
        Guid eventId,
        SeedIdentity identity,
        SeedPersona persona,
        AttendeeInclusionSource source,
        RegistrationStatus status,
        DateTimeOffset registeredAt,
        int? index = null)
    {
        var interests = persona.Interests
            .Concat(index is not null && index % 4 == 0 ? MentoringInterest : Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new Registration
        {
            EventId = eventId,
            FullName = identity.FullName,
            Email = identity.Email,
            Bio = BuildBio(persona, index),
            Interests = interests,
            InclusionSignals = inclusionSignalsNormalizer.FromSource(source),
            Status = status,
            RegisteredAt = registeredAt
        };
    }

    private static AttendeeProfile CreateProfile(SeedIdentity identity, SeedPersona persona, AttendeeInclusionSource source)
    {
        var occupationStatus = persona.ProfileMode switch
        {
            SeedProfileMode.Employee => "Employee",
            SeedProfileMode.Student => "Student",
            _ => "Freelancer"
        };

        return new AttendeeProfile
        {
            UserId = identity.UserId,
            FirstName = identity.FirstName,
            LastName = identity.LastName,
            Email = identity.Email,
            MobileNumber = "9999999999",
            GovernmentPhotoIdType = "Aadhaar Card",
            GovernmentIdLastFour = $"{identity.Index % 10000:0000}",
            OccupationStatus = occupationStatus,
            CompanyName = occupationStatus == "Employee" ? persona.Organization : null,
            EducationInstitute = occupationStatus == "Student" ? persona.Organization : null,
            LinkedInMemberId = $"seed-linkedin-{identity.Index:D4}",
            LinkedInProfileUrl = $"https://linkedin.com/in/{identity.Email.Split('@')[0]}",
            GitHubLogin = $"seeduser{identity.Index:D4}",
            GitHubProfileUrl = $"https://github.com/seeduser{identity.Index:D4}",
            City = "Mumbai",
            State = "Maharashtra",
            PostalCode = "400001",
            Country = "India",
            IsProfileComplete = true,
            ProfileCompletedAt = DateTimeOffset.UtcNow,
            IsAideProfileComplete = true,
            AideProfileCompletedAt = DateTimeOffset.UtcNow,
            Neighborhood = source.Neighborhood,
            LanguageProficiency = source.LanguageProficiency,
            EducationalBackground = source.EducationalBackground,
            SocioeconomicBackground = source.SocioeconomicBackground,
            GenderIdentity = source.GenderIdentity
        };
    }

    private static string? BuildBio(SeedPersona persona, int? index)
    {
        if (index is not null && index % 7 == 0)
        {
            return null;
        }

        return index is not null && index % 5 == 0
            ? $"Interested in {string.Join(", ", persona.Interests.Take(2))} and wants to meet collaborators building practical community projects."
            : persona.Bio;
    }

    private static SeedIdentity CreateIdentity(int index, string cohort, string seedRunSlug)
    {
        string[] firstNames = ["Asha", "Rohan", "Mira", "Dev", "Isha", "Kabir", "Naina", "Arjun", "Tara", "Vikram"];
        string[] lastNames = ["Patel", "Sharma", "Kulkarni", "Das", "Nair", "Bose", "Joshi", "Khan", "Iyer", "Fernandes"];

        var firstName = firstNames[index % firstNames.Length];
        var lastName = lastNames[(index / firstNames.Length) % lastNames.Length];
        var label = $"{seedRunSlug}-{cohort}-{index:D3}";

        return new SeedIdentity(
            index,
            firstName,
            lastName,
            $"{firstName} {lastName}",
            $"seed-{label}@example.com",
            $"seed-user-{label}");
    }

    private sealed record SeedPersona(
        string Headline,
        string Organization,
        SeedProfileMode ProfileMode,
        string Neighborhood,
        string Languages,
        string EducationBackground,
        string SocioeconomicBackground,
        IReadOnlyList<string> Interests,
        string Bio)
    {
        public AttendeeInclusionSource ToSource(int offset = 0)
        {
            var neighborhoods = (offset % 3) switch
            {
                0 => Neighborhood,
                1 => "Andheri West",
                _ => "Thane"
            };
            var genderIdentity = (offset % 5) switch
            {
                0 => "Woman",
                2 => "Non-binary",
                4 => "Prefer not to say",
                _ => "Man"
            };

            return new AttendeeInclusionSource(
                neighborhoods,
                Languages,
                EducationBackground,
                SocioeconomicBackground,
                genderIdentity);
        }
    }

    private sealed record SeedIdentity(
        int Index,
        string FirstName,
        string LastName,
        string FullName,
        string Email,
        string UserId);

    private enum SeedProfileMode
    {
        Employee,
        Student,
        Freelancer
    }

    private static readonly SeedPersona[] SeedPersonas =
    [
        new(
            "AI community builder",
            "Dominant Labs",
            SeedProfileMode.Employee,
            "Bandra East",
            "English, Marathi",
            "Alternative Path",
            "Lower middle class",
            ["AI", "community", "robotics"],
            "Love building practical community projects and helping first-time attendees find collaborators."),
        new(
            "Open source mentor",
            "OpenForge Collective",
            SeedProfileMode.Freelancer,
            "Dadar",
            "English, Hindi",
            "Bachelor's degree",
            "Middle class",
            ["Open Source", "DevTools", "Mentoring"],
            "Want to meet maintainers and share hands-on lessons from sustaining open source communities."),
        new(
            "Student researcher",
            "VJTI",
            SeedProfileMode.Student,
            "Chembur",
            "English, Marathi, Konkani",
            "School or lower",
            "Working class",
            ["Research", "AI", "Workshops"],
            "Interested in applied AI research and wants to learn from people shipping useful tools."),
        new(
            "Design systems collaborator",
            "Studio Orbit",
            SeedProfileMode.Employee,
            "Lower Parel",
            "English, Hindi",
            "Diploma / certificate",
            "Lower middle class",
            ["Design", "Accessibility", "Community"],
            "Would like to contribute design systems experience to more inclusive community events."),
        new(
            "Hardware tinkerer",
            "Makers Guild",
            SeedProfileMode.Freelancer,
            "Thane",
            "English, Marathi",
            "Alternative Path",
            "Working class",
            ["Hardware", "IoT", "Workshops"],
            "Enjoys hands-on demos and wants to prototype low-cost hardware ideas with other builders."),
        new(
            "Platform engineer",
            "Cloud Current",
            SeedProfileMode.Employee,
            "Powai",
            "English, Hindi",
            "Master's degree",
            "Upper middle class",
            ["Cloud", "Platform", "Reliability"],
            "Looking to exchange platform engineering lessons and understand what local organizers need from infra tooling.")
    ];

    private static readonly string[] MentoringInterest =
    [
        "mentoring"
    ];

    [LoggerMessage(
        EventId = 2401,
        Level = LogLevel.Information,
        Message = "Seeded curation sandbox event {EventId} with {ReviewableCount} reviewable registrants, {SelectedCount} selected attendees, and {HistoricalCount} historical registrations.")]
    private static partial void LogSeedCompleted(
        ILogger logger,
        Guid eventId,
        int reviewableCount,
        int selectedCount,
        int historicalCount);
}

public sealed record CurationSeedResult(
    Guid EventId,
    string EventTitle,
    int ReviewableRegistrants,
    int SelectedRegistrants,
    string CurationPath);
