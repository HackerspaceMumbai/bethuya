using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Deterministically provisions the six canonical development personas as
/// <see cref="CommunityMember"/> rows with linked <see cref="ExternalIdentity"/> records,
/// varied <see cref="ParticipationLedgerEntry"/> history, a shared fixture
/// <see cref="Event"/>, and one <see cref="Registration"/> per persona against it.
/// All writes are idempotent — running <see cref="SeedAsync"/> twice produces the
/// same final state with no duplicate rows. Concurrent calls are safe — a unique-constraint
/// race is handled defensively with re-query fallbacks.
/// </summary>
public sealed partial class CommunitySimulationSeeder(
    BethuyaDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<CommunitySimulationSeeder> logger)
{
    private const string SeedCreator = "community-simulation-seed@hackerspacemumbai.dev";
    private const string FixtureHashtag = "community-simulation-fixture";

    /// <summary>
    /// Seed (or verify already-seeded) community simulation fixtures and return bounded counts.
    /// Safe to call multiple times concurrently — duplicate rows are silently skipped.
    /// </summary>
    public async Task<CommunitySimulationSeedResult> SeedAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var personas = DevelopmentPersonaCatalog.All
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .ToList();

        int membersCreated = 0;
        int membersAlreadyExisted = 0;
        int externalIdentitiesCreated = 0;
        int ledgerEntriesCreated = 0;
        int ledgerEntriesAlreadyExisted = 0;
        int registrationsCreated = 0;
        int registrationsAlreadyExisted = 0;
        Guid fixtureEventId = Guid.Empty;
        string fixtureEventTitle = string.Empty;

        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // Detach any entities that may be tracked from a prior failed attempt.
            foreach (var entry in dbContext.ChangeTracker.Entries<CommunityMember>().ToList())
                entry.State = EntityState.Detached;
            foreach (var entry in dbContext.ChangeTracker.Entries<ExternalIdentity>().ToList())
                entry.State = EntityState.Detached;
            foreach (var entry in dbContext.ChangeTracker.Entries<ParticipationLedgerEntry>().ToList())
                entry.State = EntityState.Detached;
            foreach (var entry in dbContext.ChangeTracker.Entries<Event>().ToList())
                entry.State = EntityState.Detached;
            foreach (var entry in dbContext.ChangeTracker.Entries<Registration>().ToList())
                entry.State = EntityState.Detached;

            // Reset attempt counters for retry safety.
            membersCreated = 0;
            membersAlreadyExisted = 0;
            externalIdentitiesCreated = 0;
            ledgerEntriesCreated = 0;
            ledgerEntriesAlreadyExisted = 0;
            registrationsCreated = 0;
            registrationsAlreadyExisted = 0;

            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

            // ── 1. Find-or-create the shared fixture Event ───────────────────────
            var fixtureEvent = await dbContext.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Hashtag == FixtureHashtag, ct);

            if (fixtureEvent is null)
            {
                var candidateEvent = new Event
                {
                    Title = "Community Simulation Fixture",
                    Description = "Shared fixture event for the six canonical development personas.",
                    Type = EventType.Meetup,
                    Status = EventStatus.Completed,
                    Capacity = 10,
                    StartDate = now.AddDays(-14),
                    EndDate = now.AddDays(-14).AddHours(3),
                    Location = "Hackerspace Mumbai",
                    Hashtag = FixtureHashtag,
                    CreatedBy = SeedCreator
                };
                dbContext.Events.Add(candidateEvent);
                try
                {
                    await dbContext.SaveChangesAsync(ct);
                    fixtureEvent = candidateEvent;
                }
                catch (DbUpdateException)
                {
                    // Concurrent call already inserted it — detach and re-query.
                    dbContext.Entry(candidateEvent).State = EntityState.Detached;
                    fixtureEvent = await dbContext.Events
                        .AsNoTracking()
                        .FirstOrDefaultAsync(e => e.Hashtag == FixtureHashtag, ct);
                    if (fixtureEvent is null) throw;
                }
            }

            fixtureEventId = fixtureEvent.Id;
            fixtureEventTitle = fixtureEvent.Title;

            // ── 2. Load existing members + identities for deduplication ──────────
            var subjectList = personas.Select(p => p.Subject).ToList();
            var existingMembers = await dbContext.CommunityMembers
                .AsNoTracking()
                .Where(m => subjectList.Contains(m.UserId))
                .ToListAsync(ct);
            var existingMemberByUserId = existingMembers
                .ToDictionary(m => m.UserId, StringComparer.Ordinal);

            var existingIdentities = await dbContext.ExternalIdentities
                .AsNoTracking()
                .Where(i => i.Provider == IdentityProviderKind.Platform && subjectList.Contains(i.Subject))
                .ToListAsync(ct);
            var existingIdentitySubjects = existingIdentities
                .Select(i => i.Subject)
                .ToHashSet(StringComparer.Ordinal);

            // ── 3. Find-or-create members and their platform identities ──────────
            var memberByPersonaKey = new Dictionary<string, CommunityMember>(StringComparer.Ordinal);
            var pendingMembersByPersonaKey = new Dictionary<string, CommunityMember>(StringComparer.Ordinal);

            foreach (var persona in personas)
            {
                CommunityMember member;
                if (existingMemberByUserId.TryGetValue(persona.Subject, out var existingMember))
                {
                    member = existingMember;
                    membersAlreadyExisted++;
                }
                else
                {
                    member = new CommunityMember
                    {
                        UserId = persona.Subject,
                        DisplayName = persona.DisplayName,
                        Email = persona.Email
                    };
                    dbContext.CommunityMembers.Add(member);
                    pendingMembersByPersonaKey[persona.Key] = member;
                    membersCreated++;
                }

                memberByPersonaKey[persona.Key] = member;

                if (!existingIdentitySubjects.Contains(persona.Subject))
                {
                    var identity = new ExternalIdentity
                    {
                        CommunityMemberId = member.Id,
                        Provider = IdentityProviderKind.Platform,
                        Subject = persona.Subject,
                        Username = persona.Key,
                        IsVerified = true
                    };
                    dbContext.ExternalIdentities.Add(identity);
                    externalIdentitiesCreated++;
                }
            }

            if (pendingMembersByPersonaKey.Count > 0)
            {
                try
                {
                    await dbContext.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Concurrent call already inserted some members — detach and re-query.
                    foreach (var entry in dbContext.ChangeTracker.Entries<CommunityMember>().ToList())
                        entry.State = EntityState.Detached;
                    foreach (var entry in dbContext.ChangeTracker.Entries<ExternalIdentity>().ToList())
                        entry.State = EntityState.Detached;

                    var savedMembers = await dbContext.CommunityMembers
                        .AsNoTracking()
                        .Where(m => subjectList.Contains(m.UserId))
                        .ToListAsync(ct);

                    // All members now exist — adjust counts and rebuild the lookup map.
                    membersCreated = 0;
                    membersAlreadyExisted = personas.Count;
                    externalIdentitiesCreated = 0;
                    foreach (var persona in personas)
                    {
                        var saved = savedMembers.Single(m => m.UserId == persona.Subject);
                        memberByPersonaKey[persona.Key] = saved;
                    }

                    // Verify ExternalIdentity coverage — the concurrent winner may not have saved identities yet.
                    var savedSubjects = subjectList;
                    var fallbackIdentitySubjects = await dbContext.ExternalIdentities
                        .AsNoTracking()
                        .Where(i => i.Provider == IdentityProviderKind.Platform && savedSubjects.Contains(i.Subject))
                        .Select(i => i.Subject)
                        .ToHashSetAsync(ct);
                    foreach (var persona in personas)
                    {
                        if (fallbackIdentitySubjects.Contains(persona.Subject))
                            continue;
                        var member = memberByPersonaKey[persona.Key];
                        var identity = new ExternalIdentity
                        {
                            CommunityMemberId = member.Id,
                            Provider = IdentityProviderKind.Platform,
                            Subject = persona.Subject,
                            Username = persona.Key,
                            IsVerified = true
                        };
                        dbContext.ExternalIdentities.Add(identity);
                        externalIdentitiesCreated++;
                    }
                    if (externalIdentitiesCreated > 0)
                        await dbContext.SaveChangesAsync(ct);
                }
            }

            // ── 4. Find-or-create ParticipationLedgerEntry rows ─────────────────
            var allProvenanceKeys = personas
                .SelectMany(p => GetLedgerEntrySpecs(p.Key)
                    .Select(spec => $"community-simulation-seed:{p.Key.ToLowerInvariant()}:{spec.Ordinal}"))
                .ToList();

            var existingLedgerProvenanceKeys = await dbContext.ParticipationLedgerEntries
                .AsNoTracking()
                .Where(e => allProvenanceKeys.Contains(e.ProvenanceKey))
                .Select(e => new { e.CommunityMemberId, e.Connector, e.ProvenanceKey })
                .ToListAsync(ct);
            var existingLedgerKeys = existingLedgerProvenanceKeys
                .Select(e => (e.CommunityMemberId, e.Connector, e.ProvenanceKey))
                .ToHashSet();

            var pendingLedgerEntries = new List<ParticipationLedgerEntry>();
            foreach (var persona in personas)
            {
                var member = memberByPersonaKey[persona.Key];
                foreach (var spec in GetLedgerEntrySpecs(persona.Key))
                {
                    var provenanceKey = $"community-simulation-seed:{persona.Key.ToLowerInvariant()}:{spec.Ordinal}";
                    var dedupeKey = (member.Id, spec.Connector, provenanceKey);
                    if (existingLedgerKeys.Contains(dedupeKey))
                    {
                        ledgerEntriesAlreadyExisted++;
                        continue;
                    }

                    pendingLedgerEntries.Add(new ParticipationLedgerEntry
                    {
                        CommunityMemberId = member.Id,
                        Connector = spec.Connector,
                        ExternalMemberKey = persona.Key.ToLowerInvariant(),
                        Activity = spec.Activity,
                        Evidence = spec.Evidence,
                        ProvenanceKey = provenanceKey,
                        OccurredAt = now.AddDays(-(3 + spec.Ordinal * 7))
                    });
                    existingLedgerKeys.Add(dedupeKey);
                }
            }

            if (pendingLedgerEntries.Count > 0)
            {
                try
                {
                    dbContext.ParticipationLedgerEntries.AddRange(pendingLedgerEntries);
                    await dbContext.SaveChangesAsync(ct);
                    ledgerEntriesCreated += pendingLedgerEntries.Count;
                }
                catch (DbUpdateException)
                {
                    foreach (var e in pendingLedgerEntries)
                        dbContext.Entry(e).State = EntityState.Detached;

                    foreach (var e in pendingLedgerEntries)
                    {
                        dbContext.ParticipationLedgerEntries.Add(e);
                        try
                        {
                            await dbContext.SaveChangesAsync(ct);
                            ledgerEntriesCreated++;
                        }
                        catch (DbUpdateException)
                        {
                            dbContext.Entry(e).State = EntityState.Detached;
                            var keyExists = await dbContext.ParticipationLedgerEntries
                                .AsNoTracking()
                                .AnyAsync(x =>
                                    x.CommunityMemberId == e.CommunityMemberId
                                    && x.Connector == e.Connector
                                    && x.ProvenanceKey == e.ProvenanceKey, ct);
                            if (!keyExists) throw;
                            ledgerEntriesAlreadyExisted++;
                        }
                    }
                }
            }

            // ── 5. Find-or-create Registrations (one per persona per fixture event) ─
            var personaEmails = personas.Select(p => p.Email).ToList();
            var existingRegistrationEmails = await dbContext.Registrations
                .AsNoTracking()
                .Where(r => r.EventId == fixtureEventId && personaEmails.Contains(r.Email))
                .Select(r => r.Email)
                .ToListAsync(ct);
            var existingRegistrationEmailSet = existingRegistrationEmails
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var persona in personas)
            {
                if (existingRegistrationEmailSet.Contains(persona.Email))
                {
                    registrationsAlreadyExisted++;
                    continue;
                }

                var status = persona.Key switch
                {
                    "Vikram" or "Priya" => RegistrationStatus.CheckedIn,
                    "Anish" or "Rohan" => RegistrationStatus.Accepted,
                    _ => RegistrationStatus.Pending
                };

                dbContext.Registrations.Add(new Registration
                {
                    EventId = fixtureEventId,
                    FullName = persona.DisplayName,
                    Email = persona.Email,
                    Status = status,
                    RegisteredAt = now.AddDays(-20),
                    UpdatedAt = now.AddDays(-3)
                });
                registrationsCreated++;
                existingRegistrationEmailSet.Add(persona.Email);
            }

            if (registrationsCreated > 0)
            {
                try
                {
                    await dbContext.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Concurrent call already inserted some registrations — treat as existing.
                    foreach (var entry in dbContext.ChangeTracker.Entries<Registration>().ToList())
                        entry.State = EntityState.Detached;
                    registrationsAlreadyExisted += registrationsCreated;
                    registrationsCreated = 0;
                }
            }

            await transaction.CommitAsync(ct);
        });

        LogSeedCompleted(logger, fixtureEventId, membersCreated, membersAlreadyExisted,
            ledgerEntriesCreated, registrationsCreated);

        return new CommunitySimulationSeedResult(
            EventId: fixtureEventId,
            EventTitle: fixtureEventTitle,
            PersonasProvisioned: personas.Count,
            MembersCreated: membersCreated,
            MembersAlreadyExisted: membersAlreadyExisted,
            ExternalIdentitiesCreated: externalIdentitiesCreated,
            LedgerEntriesCreated: ledgerEntriesCreated,
            LedgerEntriesAlreadyExisted: ledgerEntriesAlreadyExisted,
            RegistrationsCreated: registrationsCreated,
            RegistrationsAlreadyExisted: registrationsAlreadyExisted,
            PersonaKeys: personas.Select(p => p.Key).ToList().AsReadOnly());
    }

    /// <summary>
    /// Returns the deterministic set of ledger entry specs for a given persona key.
    /// Ordinals are 0-based and must be stable across runs (they form part of the ProvenanceKey).
    /// </summary>
    private static IReadOnlyList<LedgerEntrySpec> GetLedgerEntrySpecs(string personaKey)
        => personaKey switch
        {
            "Anish" =>
            [
                new(0, ParticipationConnectorKind.Forms,   ParticipationActivityKind.JoinedCommunity, "Joined via community sign-up form"),
                new(1, ParticipationConnectorKind.Meetup,  ParticipationActivityKind.Attended,        "Attended community meetup event"),
            ],
            "Priya" =>
            [
                new(0, ParticipationConnectorKind.Forms,   ParticipationActivityKind.JoinedCommunity, "Joined via community sign-up form"),
                new(1, ParticipationConnectorKind.Meetup,  ParticipationActivityKind.Attended,        "Attended community meetup event"),
                new(2, ParticipationConnectorKind.Meetup,  ParticipationActivityKind.Volunteered,     "Volunteered as curation reviewer"),
            ],
            "Rohan" =>
            [
                new(0, ParticipationConnectorKind.Forms,   ParticipationActivityKind.JoinedCommunity, "Joined via community sign-up form"),
                new(1, ParticipationConnectorKind.Discord, ParticipationActivityKind.MessageEngaged,  "Active in community Discord channel"),
            ],
            "Maya" =>
            [
                new(0, ParticipationConnectorKind.Forms,   ParticipationActivityKind.JoinedCommunity, "Joined via community sign-up form"),
                new(1, ParticipationConnectorKind.Luma,    ParticipationActivityKind.Registered,      "Registered for community event via Luma"),
            ],
            "Farah" =>
            [
                new(0, ParticipationConnectorKind.Forms,   ParticipationActivityKind.JoinedCommunity, "Joined via community sign-up form"),
                new(1, ParticipationConnectorKind.GitHub,  ParticipationActivityKind.SubmittedSession,"Submitted session proposal via GitHub"),
            ],
            "Vikram" =>
            [
                new(0, ParticipationConnectorKind.Forms,   ParticipationActivityKind.JoinedCommunity, "Joined via community sign-up form"),
                new(1, ParticipationConnectorKind.Meetup,  ParticipationActivityKind.Attended,        "Attended community meetup event"),
                new(2, ParticipationConnectorKind.Meetup,  ParticipationActivityKind.Volunteered,     "Volunteered as event organizer"),
            ],
            _ =>
            [
                new(0, ParticipationConnectorKind.Forms, ParticipationActivityKind.JoinedCommunity, "Joined via community sign-up form"),
            ],
        };

    private sealed record LedgerEntrySpec(
        int Ordinal,
        ParticipationConnectorKind Connector,
        ParticipationActivityKind Activity,
        string Evidence);

    [LoggerMessage(
        EventId = 2402,
        Level = LogLevel.Information,
        Message = "Community simulation seeded fixture event {EventId}: {MembersCreated} members created, {MembersAlreadyExisted} already existed, {LedgerEntriesCreated} ledger entries created, {RegistrationsCreated} registrations created.")]
    private static partial void LogSeedCompleted(
        ILogger logger,
        Guid eventId,
        int membersCreated,
        int membersAlreadyExisted,
        int ledgerEntriesCreated,
        int registrationsCreated);
}

public sealed record CommunitySimulationSeedResult(
    Guid EventId,
    string EventTitle,
    int PersonasProvisioned,
    int MembersCreated,
    int MembersAlreadyExisted,
    int ExternalIdentitiesCreated,
    int LedgerEntriesCreated,
    int LedgerEntriesAlreadyExisted,
    int RegistrationsCreated,
    int RegistrationsAlreadyExisted,
    IReadOnlyList<string> PersonaKeys);
