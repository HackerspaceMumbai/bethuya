using System.IO.Compression;
using System.Text;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class CommunityPassportPhase1Tests
{
    [Test]
    public async Task DeterministicStory_ProducesStableEvidenceBackedNarrative()
    {
        var contribution = new PassportContributionResponse(
            Guid.NewGuid(),
            "ProjectContributed",
            "Built the registration flow",
            "Implemented registration.",
            "Hackerspace Mumbai",
            "Platform",
            new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero),
            true,
            true);
        var input = new CommunityStoryInput(
            "Asha",
            2025,
            [new CommunitySignalResponse(CommunitySignalKind.Builder, "Builder", false, "Verified project work.", ["Built the registration flow"])],
            [contribution],
            []);
        var generator = new DeterministicCommunityStoryGenerator();

        var first = generator.Generate(input);
        var second = generator.Generate(input);

        await Assert.That(second.Narrative).IsEqualTo(first.Narrative);
        await Assert.That(second.Evidence).IsEquivalentTo(first.Evidence);
        await Assert.That(first.Narrative).Contains("Builder");
        await Assert.That(first.Evidence).Contains("Built the registration flow");
        await Assert.That(first.Narrative).DoesNotContain("score");
        await Assert.That(first.Narrative).DoesNotContain("rank");
    }

    [Test]
    public async Task Portfolio_RejectsEvidenceOwnedByAnotherMember()
    {
        await using var db = CreateDbContext();
        var owner = BuildMember("owner", "owner@example.com");
        var other = BuildMember("other", "other@example.com");
        var foreignEvidence = BuildLedgerEntry(other, "foreign:evidence");
        db.AddRange(owner, other, foreignEvidence);
        await db.SaveChangesAsync();
        var service = new CommunityPortfolioService(db, new CommunityPassportService(db));
        var request = new UpsertPortfolioEntryRequest(
            "Community toolkit",
            "A reusable toolkit built for organizers.",
            true,
            0,
            [new PortfolioLinkResponse(PortfolioLinkKind.Repository, "https://github.com/example/toolkit", "Source")],
            [foreignEvidence.Id.Value]);

        var action = async () => await service.UpsertAsync(
            new CommunitySubjectContext(owner.UserId, owner.DisplayName, owner.Email),
            null,
            request);

        await Assert.That(action).Throws<ArgumentException>();
        await Assert.That(await db.CommunityPortfolioEntries.CountAsync()).IsEqualTo(0);
    }

    [Test]
    public async Task Portfolio_RejectsNullCollectionsAndElementsAsInvalidInput()
    {
        await using var db = CreateDbContext();
        var member = BuildMember("portfolio-null", "portfolio-null@example.com");
        db.Add(member);
        await db.SaveChangesAsync();
        var service = new CommunityPortfolioService(db, new CommunityPassportService(db));
        var nullCollectionRequest = new UpsertPortfolioEntryRequest(
            "Community toolkit",
            "A reusable toolkit built for organizers.",
            true,
            0,
            null!,
            []);
        var nullElementRequest = nullCollectionRequest with
        {
            Links = [null!]
        };
        var nullCollectionAction = async () => await service.UpsertAsync(
            new CommunitySubjectContext(member.UserId, member.DisplayName, member.Email),
            null,
            nullCollectionRequest);
        var nullElementAction = async () => await service.UpsertAsync(
            new CommunitySubjectContext(member.UserId, member.DisplayName, member.Email),
            null,
            nullElementRequest);

        await Assert.That(nullCollectionAction).Throws<ArgumentNullException>();
        await Assert.That(nullElementAction).Throws<ArgumentException>();
    }

    [Test]
    public async Task ChampionAward_IsIdempotentAndRevocationPreservesAuditHistory()
    {
        await using var db = CreateDbContext();
        var member = BuildMember("champion", "champion@example.com");
        var evidence = BuildLedgerEntry(member, "champion:evidence");
        db.AddRange(member, evidence);
        await db.SaveChangesAsync();
        var service = new CommunitySignalAwardService(db, new CommunityPassportAccessPolicy());

        var first = await service.AwardChampionAsync(
            member.Id,
            new AwardChampionSignalRequest("Sustained, generous community leadership.", [evidence.Id.Value]),
            "organizer-1");
        var second = await service.AwardChampionAsync(
            member.Id,
            new AwardChampionSignalRequest("A duplicate request must not replace the original audit record.", [evidence.Id.Value]),
            "organizer-2");
        await service.RevokeChampionAsync(
            member.Id,
            new RevokeChampionSignalRequest("Recognition period concluded."),
            "organizer-3");

        var award = await db.CommunitySignalAwards.SingleAsync();
        await Assert.That(second.Kind).IsEqualTo(first.Kind);
        await Assert.That(second.Explanation).IsEqualTo(first.Explanation);
        await Assert.That(second.Evidence).IsEquivalentTo(first.Evidence);
        await Assert.That(award.AwardedBy).IsEqualTo("organizer-1");
        await Assert.That(award.Rationale).IsEqualTo("Sustained, generous community leadership.");
        await Assert.That(award.RevokedBy).IsEqualTo("organizer-3");
        await Assert.That(award.RevocationReason).IsEqualTo("Recognition period concluded.");
        await Assert.That(award.RevokedAt).IsNotNull();
    }

    [Test]
    public async Task ChampionAward_ModelEnforcesOneActiveAwardPerMember()
    {
        await using var db = CreateDbContext();
        var entityType = db.Model.FindEntityType(typeof(CommunitySignalAward))
            ?? throw new InvalidOperationException("Signal award model metadata was not found.");
        var index = entityType.GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(CommunitySignalAward.CommunityMemberId), nameof(CommunitySignalAward.Kind)]));

        await Assert.That(index.IsUnique).IsTrue();
        await Assert.That(index.GetFilter()).IsEqualTo("\"RevokedAt\" IS NULL");
    }

    [Test]
    public async Task LegacyPrivacyUpdate_SynchronizesGranularDiscoveryPreferences()
    {
        await using var db = CreateDbContext();
        var member = BuildMember("privacy", "privacy@example.com");
        db.Add(member);
        await db.SaveChangesAsync();
        var service = new CommunityPassportService(db);

        _ = await service.UpdatePrivacyAsync(
            new CommunitySubjectContext(member.UserId, member.DisplayName, member.Email),
            new UpdateCommunityPassportPrivacyRequest(ProfileVisibilityScope.CommunityOnly, true, false));

        await db.Entry(member).ReloadAsync();
        await Assert.That(member.IsDiscoverableToCommunity).IsFalse();
        await Assert.That(member.AppearInMentorshipRecommendations).IsFalse();
        await Assert.That(member.AppearInOpportunityRecommendations).IsFalse();
        await Assert.That(member.AppearInCollaboratorDiscovery).IsFalse();
        await Assert.That(member.AppearInSpeakerRecommendations).IsFalse();
        await Assert.That(member.AppearInVolunteerLeadershipRecommendations).IsFalse();
    }

    [Test]
    public async Task ChampionRevocation_RejectsPrivatePassport()
    {
        await using var db = CreateDbContext();
        var member = BuildMember("private-champion", "private-champion@example.com");
        member.Visibility = ProfileVisibilityScope.Private;
        var award = new CommunitySignalAward
        {
            CommunityMemberId = member.Id,
            Kind = CommunitySignalKind.Champion,
            Rationale = "Sustained community leadership.",
            AwardedBy = "organizer-1",
            EvidenceEntryIds = []
        };
        db.AddRange(member, award);
        await db.SaveChangesAsync();
        var service = new CommunitySignalAwardService(db, new CommunityPassportAccessPolicy());

        var action = async () => await service.RevokeChampionAsync(
            member.Id,
            new RevokeChampionSignalRequest("Should remain private."),
            "organizer-2");

        await Assert.That(action).Throws<UnauthorizedAccessException>();
        await db.Entry(award).ReloadAsync();
        await Assert.That(award.RevokedAt).IsNull();
    }

    [Test]
    public async Task Directory_AppliesSharingFilterBeforePagination()
    {
        await using var db = CreateDbContext();
        var hidden = BuildMember("a-hidden", "hidden@example.com");
        hidden.DisplayName = "A Hidden";
        hidden.ShareParticipationWithOrganizers = false;
        var shared = BuildMember("b-shared", "shared@example.com");
        shared.DisplayName = "B Shared";
        shared.ShareParticipationWithOrganizers = true;
        db.AddRange(hidden, shared);
        await db.SaveChangesAsync();
        var passportService = new CommunityPassportService(db);
        var service = new CommunityPassportReadModelService(
            db,
            passportService,
            new CommunityPassportAccessPolicy(),
            new DeterministicCommunityStoryGenerator());

        var result = await service.GetDirectoryAsync(null, true, 0, 1);

        await Assert.That(result.TotalCount).IsEqualTo(1);
        await Assert.That(result.Entries).HasSingleItem();
        await Assert.That(result.Entries[0].DisplayName).IsEqualTo("B Shared");
    }

    [Test]
    public async Task Directory_PreservesRegistrationDerivedVolunteerSignal()
    {
        await using var db = CreateDbContext();
        var member = BuildMember("volunteer-intent", "volunteer-intent@example.com");
        var registration = new Registration
        {
            EventId = Guid.NewGuid(),
            CommunityMemberId = member.Id,
            FullName = member.DisplayName,
            Email = member.Email,
            ContributionPreferences = ["Volunteer at check-in"]
        };
        db.AddRange(member, registration);
        await db.SaveChangesAsync();
        var passportService = new CommunityPassportService(db);
        var service = new CommunityPassportReadModelService(
            db,
            passportService,
            new CommunityPassportAccessPolicy(),
            new DeterministicCommunityStoryGenerator());

        var result = await service.GetDirectoryAsync(null, true, 0, 24);

        await Assert.That(result.Entries).HasSingleItem();
        await Assert.That(result.Entries[0].Signals).Contains("Volunteer");
    }

    [Test]
    public async Task Directory_UsesImmutableRegistrationMemberLink()
    {
        await using var db = CreateDbContext();
        var member = BuildMember("literal-email", "member_name@EXAMPLE.com");
        var wildcardNeighbor = BuildMember("wildcard-neighbor", "memberXname@example.com");
        var registration = new Registration
        {
            EventId = Guid.NewGuid(),
            CommunityMemberId = member.Id,
            FullName = member.DisplayName,
            Email = "member_name@example.com",
            ContributionPreferences = ["Volunteer at check-in"]
        };
        db.AddRange(member, wildcardNeighbor, registration);
        await db.SaveChangesAsync();
        var passportService = new CommunityPassportService(db);
        var service = new CommunityPassportReadModelService(
            db,
            passportService,
            new CommunityPassportAccessPolicy(),
            new DeterministicCommunityStoryGenerator());

        var result = await service.GetDirectoryAsync(null, true, 0, 24);

        var literalEntry = result.Entries.Single(entry => entry.MemberId == member.Id.Value);
        var neighborEntry = result.Entries.Single(entry => entry.MemberId == wildcardNeighbor.Id.Value);
        await Assert.That(literalEntry.Signals).Contains("Volunteer");
        await Assert.That(neighborEntry.Signals).DoesNotContain("Volunteer");
    }

    [Test]
    public async Task Journey_DeduplicatesAttendanceEvidenceForSameEvent()
    {
        await using var db = CreateDbContext();
        var member = BuildMember("attendee", "attendee@example.com");
        var evt = new Event { Title = "Community Night", CreatedBy = "organizer@example.com" };
        var registration = new Registration
        {
            EventId = evt.Id,
            CommunityMemberId = member.Id,
            FullName = member.DisplayName,
            Email = member.Email,
            Status = RegistrationStatus.CheckedIn
        };
        var firstLedgerEntry = new ParticipationLedgerEntry
        {
            CommunityMemberId = member.Id,
            Connector = ParticipationConnectorKind.Luma,
            ExternalMemberKey = member.UserId,
            EventId = evt.Id,
            Activity = ParticipationActivityKind.Attended,
            Evidence = "Checked in",
            ProvenanceKey = "attendance:1",
            OccurredAt = DateTimeOffset.UtcNow
        };
        var secondLedgerEntry = new ParticipationLedgerEntry
        {
            CommunityMemberId = member.Id,
            Connector = ParticipationConnectorKind.Luma,
            ExternalMemberKey = member.UserId,
            EventId = evt.Id,
            Activity = ParticipationActivityKind.Attended,
            Evidence = "Attendance synchronized",
            ProvenanceKey = "attendance:2",
            OccurredAt = DateTimeOffset.UtcNow
        };
        db.AddRange(member, evt, registration, firstLedgerEntry, secondLedgerEntry);
        await db.SaveChangesAsync();
        var passportService = new CommunityPassportService(db);
        var service = new CommunityPassportReadModelService(
            db,
            passportService,
            new CommunityPassportAccessPolicy(),
            new DeterministicCommunityStoryGenerator());

        var result = await service.GetMineAsync(
            new CommunitySubjectContext(member.UserId, member.DisplayName, member.Email));

        var participation = result.Journey.Single(pathway => pathway.Name == "Participation");
        await Assert.That(participation.CurrentStage).IsEqualTo("First event");
        await Assert.That(result.Contributions).IsNotEmpty();
        await Assert.That(result.Contributions.All(contribution => contribution.EventId == evt.Id)).IsTrue();
    }

    [Test]
    public async Task RegistrationContribution_IsNotEligibleAsChampionEvidence()
    {
        await using var db = CreateDbContext();
        var member = BuildMember("registrant", "registrant@example.com");
        var eventDate = new DateTimeOffset(2026, 9, 20, 18, 0, 0, TimeSpan.FromHours(5.5));
        var evt = new Event
        {
            Title = "Community Night",
            CreatedBy = "organizer@example.com",
            StartDate = eventDate
        };
        var registration = new Registration
        {
            EventId = evt.Id,
            CommunityMemberId = member.Id,
            FullName = member.DisplayName,
            Email = member.Email,
            Status = RegistrationStatus.CheckedIn,
            UpdatedAt = eventDate.AddMonths(1)
        };
        db.AddRange(member, evt, registration);
        await db.SaveChangesAsync();
        var passportService = new CommunityPassportService(db);
        var service = new CommunityPassportReadModelService(
            db,
            passportService,
            new CommunityPassportAccessPolicy(),
            new DeterministicCommunityStoryGenerator());

        var result = await service.GetMineAsync(
            new CommunitySubjectContext(member.UserId, member.DisplayName, member.Email));

        await Assert.That(result.Contributions).HasSingleItem();
        await Assert.That(result.Contributions[0].IsLedgerEvidence).IsFalse();
        await Assert.That(result.Contributions[0].OccurredAt).IsEqualTo(eventDate);
        await Assert.That(result.Contributions[0].EventId).IsEqualTo(evt.Id);
    }

    [Test]
    public async Task Experience_DoesNotClaimUnlinkedRegistrationByMutableEmail()
    {
        await using var db = CreateDbContext();
        var member = BuildMember("mutable-email", "victim@example.com");
        var evt = new Event { Title = "Private Event", CreatedBy = "organizer@example.com" };
        db.AddRange(
            member,
            evt,
            new Registration
            {
                EventId = evt.Id,
                FullName = "Victim",
                Email = member.Email,
                Status = RegistrationStatus.CheckedIn
            });
        await db.SaveChangesAsync();
        var service = new CommunityPassportReadModelService(
            db,
            new CommunityPassportService(db),
            new CommunityPassportAccessPolicy(),
            new DeterministicCommunityStoryGenerator());

        var result = await service.GetMineAsync(
            new CommunitySubjectContext(member.UserId, member.DisplayName, member.Email));

        await Assert.That(result.Contributions).IsEmpty();
        await Assert.That(result.Journey.Single(pathway => pathway.Name == "Participation").CurrentStage)
            .IsEqualTo("Getting started");
    }

    [Test]
    public async Task Connections_HideTargetsThatOptOutOfCollaboratorDiscovery()
    {
        await using var db = CreateDbContext();
        var source = BuildMember("source", "source@example.com");
        var target = BuildMember("target", "target@example.com");
        target.AppearInCollaboratorDiscovery = false;
        var relationship = new CommunityRelationship
        {
            SourceMemberId = source.Id,
            TargetMemberId = target.Id,
            Kind = CommunityRelationshipKind.Collaborator,
            Context = "Built a community project together"
        };
        db.AddRange(source, target, relationship);
        await db.SaveChangesAsync();
        var passportService = new CommunityPassportService(db);
        var service = new CommunityPassportReadModelService(
            db,
            passportService,
            new CommunityPassportAccessPolicy(),
            new DeterministicCommunityStoryGenerator());

        var result = await service.GetMineAsync(
            new CommunitySubjectContext(source.UserId, source.DisplayName, source.Email));

        await Assert.That(result.Connections).IsEmpty();
    }

    [Test]
    public async Task Connections_LimitAfterApplyingMemberEligibility()
    {
        await using var db = CreateDbContext();
        var source = BuildMember("connection-source", "connection-source@example.com");
        var eligible = BuildMember("z-eligible", "z-eligible@example.com");
        var evt = new Event { Title = "Community Night", CreatedBy = "organizer@example.com" };
        var registrations = new List<Registration>
        {
            new()
            {
                EventId = evt.Id,
                CommunityMemberId = source.Id,
                FullName = source.DisplayName,
                Email = source.Email,
                Status = RegistrationStatus.CheckedIn
            },
            new()
            {
                EventId = evt.Id,
                CommunityMemberId = eligible.Id,
                FullName = eligible.DisplayName,
                Email = eligible.Email,
                Status = RegistrationStatus.CheckedIn
            }
        };
        var ineligibleMembers = Enumerable.Range(0, 6)
            .Select(index =>
            {
                var member = BuildMember($"a-private-{index}", $"private-{index}@example.com");
                member.Visibility = ProfileVisibilityScope.Private;
                registrations.Add(new Registration
                {
                    EventId = evt.Id,
                    CommunityMemberId = member.Id,
                    FullName = member.DisplayName,
                    Email = member.Email,
                    Status = RegistrationStatus.CheckedIn
                });
                return member;
            })
            .ToArray();
        db.AddRange(source, eligible, evt);
        db.AddRange(ineligibleMembers);
        db.AddRange(registrations);
        await db.SaveChangesAsync();
        var passportService = new CommunityPassportService(db);
        var service = new CommunityPassportReadModelService(
            db,
            passportService,
            new CommunityPassportAccessPolicy(),
            new DeterministicCommunityStoryGenerator());

        var result = await service.GetMineAsync(
            new CommunitySubjectContext(source.UserId, source.DisplayName, source.Email));

        await Assert.That(result.Connections).HasSingleItem();
        await Assert.That(result.Connections[0].MemberId).IsEqualTo(eligible.Id.Value);
    }

    [Test]
    public async Task Connections_UseImmutableRegistrationMemberLink()
    {
        await using var db = CreateDbContext();
        var source = BuildMember("connection-source", "connection-source@example.com");
        var literalCandidate = BuildMember("literal-candidate", "member_name@EXAMPLE.com");
        var wildcardNeighbor = BuildMember("wildcard-neighbor", "memberXname@example.com");
        var evt = new Event { Title = "Community Night", CreatedBy = "organizer@example.com" };
        db.AddRange(
            source,
            literalCandidate,
            wildcardNeighbor,
            evt,
            new Registration
            {
                EventId = evt.Id,
                CommunityMemberId = source.Id,
                FullName = source.DisplayName,
                Email = source.Email,
                Status = RegistrationStatus.CheckedIn
            },
            new Registration
            {
                EventId = evt.Id,
                CommunityMemberId = literalCandidate.Id,
                FullName = literalCandidate.DisplayName,
                Email = "member_name@example.com",
                Status = RegistrationStatus.CheckedIn
            });
        await db.SaveChangesAsync();
        var passportService = new CommunityPassportService(db);
        var service = new CommunityPassportReadModelService(
            db,
            passportService,
            new CommunityPassportAccessPolicy(),
            new DeterministicCommunityStoryGenerator());

        var result = await service.GetMineAsync(
            new CommunitySubjectContext(source.UserId, source.DisplayName, source.Email));

        await Assert.That(result.Connections).HasSingleItem();
        await Assert.That(result.Connections[0].MemberId).IsEqualTo(literalCandidate.Id.Value);
    }

    [Test]
    public async Task OrganizerConnections_HideStoredTargetsThatWithholdParticipation()
    {
        await using var db = CreateDbContext();
        var source = BuildMember("organizer-view-source", "source@example.com");
        var target = BuildMember("organizer-view-target", "target@example.com");
        target.ShareParticipationWithOrganizers = false;
        db.AddRange(
            source,
            target,
            new CommunityRelationship
            {
                SourceMemberId = source.Id,
                TargetMemberId = target.Id,
                Kind = CommunityRelationshipKind.Collaborator,
                Context = "Built a community project together"
            });
        await db.SaveChangesAsync();
        var service = new CommunityPassportReadModelService(
            db,
            new CommunityPassportService(db),
            new CommunityPassportAccessPolicy(),
            new DeterministicCommunityStoryGenerator());

        var result = await service.GetForOrganizerAsync(source.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Connections).IsEmpty();
    }

    [Test]
    public async Task OrganizerConnections_HideInferredTargetsThatWithholdParticipation()
    {
        await using var db = CreateDbContext();
        var source = BuildMember("organizer-inferred-source", "source@example.com");
        var target = BuildMember("organizer-inferred-target", "target@example.com");
        target.ShareParticipationWithOrganizers = false;
        var evt = new Event { Title = "Community Night", CreatedBy = "organizer@example.com" };
        db.AddRange(
            source,
            target,
            evt,
            new Registration
            {
                EventId = evt.Id,
                CommunityMemberId = source.Id,
                FullName = source.DisplayName,
                Email = source.Email,
                Status = RegistrationStatus.CheckedIn
            },
            new Registration
            {
                EventId = evt.Id,
                CommunityMemberId = target.Id,
                FullName = target.DisplayName,
                Email = target.Email,
                Status = RegistrationStatus.CheckedIn
            });
        await db.SaveChangesAsync();
        var service = new CommunityPassportReadModelService(
            db,
            new CommunityPassportService(db),
            new CommunityPassportAccessPolicy(),
            new DeterministicCommunityStoryGenerator());

        var result = await service.GetForOrganizerAsync(source.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Connections).IsEmpty();
    }

    [Test]
    public async Task ExportArchive_ContainsCanonicalFilesAndNeutralizesActiveContent()
    {
        var passport = BuildExportFixture();
        var bytes = new CommunityPassportExportService().BuildArchive(passport);
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var expectedNames = new[]
        {
            "passport.json",
            "contributions.csv",
            "opportunities.csv",
            "relationships.csv",
            "portfolio.csv",
            "community-story.md",
            "passport-summary.html"
        };

        await Assert.That(archive.Entries.Select(entry => entry.FullName)).IsEquivalentTo(expectedNames);
        var contributionsCsv = await ReadEntryAsync(archive, "contributions.csv");
        var html = await ReadEntryAsync(archive, "passport-summary.html");
        await Assert.That(contributionsCsv).Contains("\"'=HYPERLINK(\"");
        await Assert.That(html).DoesNotContain("<script>");
        await Assert.That(html).Contains("&lt;script&gt;");
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"community-passport-phase1-{Guid.NewGuid():N}")
            .Options;
        return new BethuyaDbContext(options);
    }

    private static CommunityMember BuildMember(string userId, string email)
        => new()
        {
            UserId = userId,
            DisplayName = userId,
            Email = email,
            Visibility = ProfileVisibilityScope.CommunityOnly
        };

    private static ParticipationLedgerEntry BuildLedgerEntry(CommunityMember member, string provenanceKey)
        => new()
        {
            CommunityMemberId = member.Id,
            Connector = ParticipationConnectorKind.GitHub,
            ExternalMemberKey = member.UserId,
            Activity = ParticipationActivityKind.ProjectContributed,
            Evidence = "Verified project contribution",
            ProvenanceKey = provenanceKey,
            OccurredAt = DateTimeOffset.UtcNow
        };

    private static async Task<string> ReadEntryAsync(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidOperationException($"Archive entry {name} was not found.");
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static CommunityPassportExperienceResponse BuildExportFixture()
        => new(
            "1.0",
            new PassportViewerResponse(true, false, true, false),
            new PassportIdentitySummaryResponse(
                Guid.NewGuid(),
                "<script>alert(1)</script>",
                "member@example.com",
                "MA",
                "Hackerspace Mumbai",
                DateTimeOffset.UtcNow.AddYears(-1),
                null,
                null),
            new CommunityStoryResponse("An evidence-backed community story.", ["Verified contribution"]),
            [],
            [],
            [
                new PassportContributionResponse(
                    Guid.NewGuid(),
                    "ProjectContributed",
                    "=HYPERLINK(\"https://example.com\")",
                    "Contribution description",
                    "Hackerspace Mumbai",
                    "Platform",
                    DateTimeOffset.UtcNow,
                    true,
                    true)
            ],
            [],
            new PassportPortfolioResponse([], []),
            [],
            [],
            new PassportPrivacyPreferencesResponse(
                ProfileVisibilityScope.CommunityOnly,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                true),
            new PassportResidencyResponse("South India", SensitiveDataResidencyMode.SovereignRegion, "DPDP-ready"));
}
