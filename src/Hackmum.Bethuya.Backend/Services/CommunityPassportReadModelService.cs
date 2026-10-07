using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Backend.Services;

/// <summary>
/// Builds the versioned, permission-aware Community Passport experience.
/// </summary>
public sealed class CommunityPassportReadModelService(
    BethuyaDbContext db,
    CommunityPassportService passportService,
    CommunityPassportAccessPolicy accessPolicy,
    ICommunityStoryGenerator storyGenerator)
{
    private const string SchemaVersion = "1.0";
    private const string CommunityName = "Hackerspace Mumbai";

    public async Task<CommunityPassportExperienceResponse> GetMineAsync(
        CommunitySubjectContext subject,
        CancellationToken ct = default)
    {
        var member = await passportService.EnsureMemberProvisionedAsync(subject, ct);
        return await BuildAsync(member.Id, isOwner: true, isOrganizer: false, ct);
    }

    public async Task<CommunityPassportExperienceResponse?> GetForOrganizerAsync(
        CommunityMemberId memberId,
        CancellationToken ct = default)
    {
        var member = await db.CommunityMembers.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == memberId, ct);

        if (member is null || !accessPolicy.CanOrganizerView(member))
        {
            return null;
        }

        return await BuildAsync(memberId, isOwner: false, isOrganizer: true, ct);
    }

    public async Task<CommunityPassportDirectoryResponse> GetDirectoryAsync(
        string? search,
        bool? participationShared,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var normalizedSearch = search?.Trim();
        var query = db.CommunityMembers.AsNoTracking()
            .Where(member => member.Visibility != ProfileVisibilityScope.Private);

        if (participationShared.HasValue)
        {
            query = query.Where(member =>
                member.ShareParticipationWithOrganizers == participationShared.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            if (db.Database.IsNpgsql())
            {
                var pattern = $"%{EscapeLikePattern(normalizedSearch)}%";
                query = query.Where(member =>
                    EF.Functions.ILike(member.DisplayName, pattern)
                    || member.OccupationStatus != null && EF.Functions.ILike(member.OccupationStatus, pattern)
                    || member.CompanyName != null && EF.Functions.ILike(member.CompanyName, pattern));
            }
            else
            {
                query = query.Where(member =>
                    member.DisplayName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || member.OccupationStatus != null
                        && member.OccupationStatus.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || member.CompanyName != null
                        && member.CompanyName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }
        }

        var totalCount = await query.CountAsync(ct);
        var members = await query
            .OrderBy(member => member.DisplayName)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);
        var memberIds = members.Select(member => member.Id).ToArray();
        var sharedMemberIds = members
            .Where(member => member.ShareParticipationWithOrganizers)
            .Select(member => member.Id)
            .ToArray();
        List<DirectoryRegistrationSignal> directoryRegistrations;
        if (sharedMemberIds.Length == 0)
        {
            directoryRegistrations = [];
        }
        else
        {
            directoryRegistrations = await db.Registrations.AsNoTracking()
                .Where(registration => registration.CommunityMemberId.HasValue
                    && sharedMemberIds.Contains(registration.CommunityMemberId.Value))
                .Select(registration => new DirectoryRegistrationSignal(
                    registration.CommunityMemberId!.Value,
                    registration.Intent,
                    registration.Goals,
                    registration.ContributionPreferences))
                .ToListAsync(ct);
        }
        var registrationsByMember = directoryRegistrations.ToLookup(registration => registration.MemberId);
        var ledgerActivities = memberIds.Length == 0
            ? []
            : await db.ParticipationLedgerEntries.AsNoTracking()
                .Where(entry => memberIds.Contains(entry.CommunityMemberId))
                .Select(entry => new { entry.CommunityMemberId, entry.Activity })
                .Distinct()
                .ToListAsync(ct);
        var activitiesByMember = ledgerActivities.ToLookup(entry => entry.CommunityMemberId, entry => entry.Activity);
        var relationshipCounts = memberIds.Length == 0
            ? []
            : await db.CommunityRelationships.AsNoTracking()
                .Where(relationship => memberIds.Contains(relationship.SourceMemberId))
                .GroupBy(relationship => relationship.SourceMemberId)
                .Select(group => new { MemberId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.MemberId, item => item.Count, ct);
        var activeAwards = memberIds.Length == 0
            ? []
            : await db.CommunitySignalAwards.AsNoTracking()
                .Where(award => memberIds.Contains(award.CommunityMemberId)
                    && award.Kind == CommunitySignalKind.Champion
                    && award.RevokedAt == null)
                .ToListAsync(ct);
        var awardsByMember = activeAwards.ToLookup(award => award.CommunityMemberId);

        var entries = new List<CommunityPassportDirectoryEntryResponse>(members.Count);
        foreach (var member in members)
        {
            var signals = member.ShareParticipationWithOrganizers
                ? BuildDirectorySignalLabels(
                    activitiesByMember[member.Id],
                    relationshipCounts.GetValueOrDefault(member.Id),
                    awardsByMember[member.Id],
                    registrationsByMember[member.Id].Any(HasVolunteerSignal))
                    .Take(4)
                    .ToArray()
                : [];

            entries.Add(new CommunityPassportDirectoryEntryResponse(
                member.Id.Value,
                member.DisplayName,
                CommunityName,
                member.OccupationStatus,
                signals,
                member.CreatedAt,
                member.ShareParticipationWithOrganizers));
        }

        return new CommunityPassportDirectoryResponse(totalCount, entries);
    }

    private static IEnumerable<string> BuildDirectorySignalLabels(
        IEnumerable<ParticipationActivityKind> activities,
        int relationshipCount,
        IEnumerable<CommunitySignalAward> awards,
        bool hasVolunteerIntent)
    {
        var activitySet = activities.ToHashSet();
        if (activitySet.Contains(ParticipationActivityKind.ProjectContributed))
        {
            yield return "Builder";
        }
        if (activitySet.Contains(ParticipationActivityKind.Volunteered))
        {
            yield return "Volunteer";
        }
        else if (hasVolunteerIntent)
        {
            yield return "Volunteer";
        }
        if (activitySet.Contains(ParticipationActivityKind.Spoke)
            || activitySet.Contains(ParticipationActivityKind.SubmittedSession))
        {
            yield return "Speaker";
        }
        if (activitySet.Contains(ParticipationActivityKind.Mentored))
        {
            yield return "Mentor";
        }
        if (activitySet.Contains(ParticipationActivityKind.Organized)
            || activitySet.Contains(ParticipationActivityKind.LedProgram))
        {
            yield return "Organizer";
        }
        if (activitySet.Contains(ParticipationActivityKind.Maintained))
        {
            yield return "Maintainer";
        }
        if (activitySet.Contains(ParticipationActivityKind.ContentCreated)
            || activitySet.Contains(ParticipationActivityKind.Spoke))
        {
            yield return "Knowledge Sharer";
        }
        if (activitySet.Contains(ParticipationActivityKind.Moderated)
            || activitySet.Contains(ParticipationActivityKind.LedProgram))
        {
            yield return "Community Steward";
        }
        if (relationshipCount >= 2)
        {
            yield return "Connector";
        }
        if (awards.Any())
        {
            yield return "Champion";
        }
    }

    public async Task<PassportPrivacyPreferencesResponse> UpdatePrivacyAsync(
        CommunitySubjectContext subject,
        UpdatePassportPrivacyPreferencesRequest request,
        CancellationToken ct = default)
    {
        var member = await passportService.EnsureMemberProvisionedAsync(subject, ct);
        member.Visibility = request.Visibility;
        member.ShareParticipationWithOrganizers = request.ShareParticipationWithOrganizers;
        member.IsDiscoverableToCommunity = request.AppearInMentorshipRecommendations
            || request.AppearInOpportunityRecommendations
            || request.AppearInCollaboratorDiscovery
            || request.AppearInSpeakerRecommendations
            || request.AppearInVolunteerLeadershipRecommendations;
        member.AppearInMentorshipRecommendations = request.AppearInMentorshipRecommendations;
        member.AppearInOpportunityRecommendations = request.AppearInOpportunityRecommendations;
        member.AppearInCollaboratorDiscovery = request.AppearInCollaboratorDiscovery;
        member.AppearInSpeakerRecommendations = request.AppearInSpeakerRecommendations;
        member.AppearInVolunteerLeadershipRecommendations = request.AppearInVolunteerLeadershipRecommendations;
        member.EnableRelationshipInsights = request.EnableRelationshipInsights;
        member.ReceiveOpportunityRecommendations = request.ReceiveOpportunityRecommendations;
        member.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToPrivacy(member);
    }

    private async Task<CommunityPassportExperienceResponse> BuildAsync(
        CommunityMemberId memberId,
        bool isOwner,
        bool isOrganizer,
        CancellationToken ct)
    {
        var member = await db.CommunityMembers
            .Include(candidate => candidate.PortfolioEntries)
            .Include(candidate => candidate.Opportunities)
            .Include(candidate => candidate.SignalAwards)
            .AsNoTracking()
            .AsSplitQuery()
            .SingleAsync(candidate => candidate.Id == memberId, ct);

        var redacted = accessPolicy.ShouldRedactParticipation(member, isOwner);
        List<Registration> registrations = redacted
            ? []
            : await db.Registrations.AsNoTracking()
                .Where(registration => registration.CommunityMemberId == member.Id)
                .ToListAsync(ct);
        List<ParticipationLedgerEntry> ledgerEntries = redacted
            ? []
            : await db.ParticipationLedgerEntries.AsNoTracking()
                .Where(entry => entry.CommunityMemberId == member.Id)
                .OrderByDescending(entry => entry.OccurredAt)
                .ToListAsync(ct);
        var eventsById = await LoadEventsAsync(registrations, ledgerEntries, ct);
        var contributions = redacted ? [] : BuildContributions(registrations, ledgerEntries, eventsById);
        var opportunities = redacted ? [] : BuildOpportunities(member.Opportunities);
        var relationshipCount = redacted
            ? 0
            : await db.CommunityRelationships.AsNoTracking()
                .CountAsync(relationship => relationship.SourceMemberId == member.Id, ct);
        var signals = redacted
            ? []
            : BuildSignals(ledgerEntries, registrations, relationshipCount, member.SignalAwards);
        var story = redacted
            ? new CommunityStoryResponse("Participation details are private for this organizer view.", [])
            : storyGenerator.Generate(new CommunityStoryInput(
                member.DisplayName,
                member.CreatedAt.Year,
                signals,
                contributions,
                opportunities));

        return new CommunityPassportExperienceResponse(
            SchemaVersion,
            new PassportViewerResponse(isOwner, isOrganizer, isOwner, redacted),
            new PassportIdentitySummaryResponse(
                member.Id.Value,
                member.DisplayName,
                isOwner || isOrganizer ? member.Email : string.Empty,
                GetInitials(member.DisplayName),
                CommunityName,
                member.CreatedAt,
                member.OccupationStatus,
                member.CompanyName ?? member.EducationInstitute),
            story,
            signals,
            redacted ? [] : BuildPathways(ledgerEntries, registrations),
            contributions,
            redacted ? [] : BuildActivity(contributions),
            redacted ? new PassportPortfolioResponse([], []) : BuildPortfolio(member, contributions),
            redacted || !member.EnableRelationshipInsights
                ? []
                : await BuildConnectionsAsync(member, registrations, isOrganizer, ct),
            opportunities,
            ToPrivacy(member),
            new PassportResidencyResponse(member.ResidencyRegion, member.ResidencyMode, member.ComplianceProfile));
    }

    private async Task<Dictionary<Guid, ContributionEventSource>> LoadEventsAsync(
        List<Registration> registrations,
        List<ParticipationLedgerEntry> ledgerEntries,
        CancellationToken ct)
    {
        var eventIds = registrations.Select(registration => registration.EventId)
            .Concat(ledgerEntries.Where(entry => entry.EventId.HasValue).Select(entry => entry.EventId!.Value))
            .Distinct()
            .ToArray();
        return eventIds.Length == 0
            ? []
            : await db.Events.AsNoTracking()
                .Where(evt => eventIds.Contains(evt.Id))
                .ToDictionaryAsync(
                    evt => evt.Id,
                    evt => new ContributionEventSource(evt.Title, evt.StartDate),
                    ct);
    }

    private static List<PassportContributionResponse> BuildContributions(
        List<Registration> registrations,
        List<ParticipationLedgerEntry> ledgerEntries,
        Dictionary<Guid, ContributionEventSource> eventsById)
    {
        var contributions = ledgerEntries.Select(entry =>
        {
            var activityLabel = FormatActivity(entry.Activity);
            var title = entry.EventId.HasValue && eventsById.TryGetValue(entry.EventId.Value, out var evt)
                ? $"{activityLabel} · {evt.Title}"
                : activityLabel;
            return new PassportContributionResponse(
                entry.Id.Value,
                activityLabel,
                title,
                entry.Evidence,
                CommunityName,
                ImpactArea(entry.Activity),
                entry.OccurredAt,
                true,
                true,
                entry.EventId);
        }).ToList();

        var ledgerEventActivities = ledgerEntries
            .Where(entry => entry.EventId.HasValue)
            .Select(entry => (entry.EventId!.Value, entry.Activity))
            .ToHashSet();
        foreach (var registration in registrations)
        {
            var activity = registration.Status switch
            {
                RegistrationStatus.CheckedIn => ParticipationActivityKind.Attended,
                RegistrationStatus.Waitlisted => ParticipationActivityKind.Waitlisted,
                _ => ParticipationActivityKind.Registered
            };
            if (ledgerEventActivities.Contains((registration.EventId, activity)))
            {
                continue;
            }

            var eventTitle = eventsById.TryGetValue(registration.EventId, out var evt)
                ? evt.Title
                : "Community event";
            var occurredAt = activity == ParticipationActivityKind.Attended
                && evt is { StartDate: var startDate }
                && startDate != default
                    ? startDate
                    : registration.RegisteredAt;
            var activityLabel = FormatActivity(activity);
            contributions.Add(new PassportContributionResponse(
                registration.Id,
                activityLabel,
                $"{activityLabel} · {eventTitle}",
                $"Verified registration outcome: {registration.Status}.",
                CommunityName,
                "Participation",
                occurredAt,
                true,
                false,
                registration.EventId));
        }

        return contributions.OrderByDescending(contribution => contribution.OccurredAt).ToList();
    }

    private static List<CommunitySignalResponse> BuildSignals(
        IEnumerable<ParticipationLedgerEntry> ledgerEntries,
        IReadOnlyCollection<Registration> registrations,
        int relationshipCount,
        IEnumerable<CommunitySignalAward> signalAwards)
    {
        var materializedLedgerEntries = ledgerEntries as IReadOnlyCollection<ParticipationLedgerEntry>
            ?? ledgerEntries.ToArray();
        var signals = new List<CommunitySignalResponse>();
        AddDerivedSignal(signals, CommunitySignalKind.Builder, "Builder",
            materializedLedgerEntries.Where(entry => entry.Activity == ParticipationActivityKind.ProjectContributed),
            "Builds projects and contributes technical work.");
        AddDerivedSignal(signals, CommunitySignalKind.Volunteer, "Volunteer",
            materializedLedgerEntries.Where(entry => entry.Activity == ParticipationActivityKind.Volunteered),
            "Shows up to help community programs run.");
        AddDerivedSignal(signals, CommunitySignalKind.Speaker, "Speaker",
            materializedLedgerEntries.Where(entry => entry.Activity is ParticipationActivityKind.Spoke or ParticipationActivityKind.SubmittedSession),
            "Shares knowledge through sessions and workshops.");
        AddDerivedSignal(signals, CommunitySignalKind.Mentor, "Mentor",
            materializedLedgerEntries.Where(entry => entry.Activity == ParticipationActivityKind.Mentored),
            "Supports the growth of other community members.");
        AddDerivedSignal(signals, CommunitySignalKind.Organizer, "Organizer",
            materializedLedgerEntries.Where(entry => entry.Activity is ParticipationActivityKind.Organized or ParticipationActivityKind.LedProgram),
            "Coordinates events and community programs.");
        AddDerivedSignal(signals, CommunitySignalKind.Maintainer, "Maintainer",
            materializedLedgerEntries.Where(entry => entry.Activity == ParticipationActivityKind.Maintained),
            "Sustains shared projects and community infrastructure.");
        AddDerivedSignal(signals, CommunitySignalKind.KnowledgeSharer, "Knowledge Sharer",
            materializedLedgerEntries.Where(entry => entry.Activity is ParticipationActivityKind.ContentCreated or ParticipationActivityKind.Spoke),
            "Creates reusable learning resources for the community.");
        AddDerivedSignal(signals, CommunitySignalKind.CommunitySteward, "Community Steward",
            materializedLedgerEntries.Where(entry => entry.Activity is ParticipationActivityKind.Moderated or ParticipationActivityKind.LedProgram),
            "Creates healthy spaces and carries community responsibility.");

        if (relationshipCount >= 2)
        {
            signals.Add(new CommunitySignalResponse(
                CommunitySignalKind.Connector,
                "Connector",
                false,
                "Builds relationships across community activities.",
                [$"{relationshipCount} evidence-backed community relationships"]));
        }

        if (!signals.Any(signal => signal.Kind == CommunitySignalKind.Volunteer)
            && registrations.Any(HasVolunteerSignal))
        {
            signals.Add(new CommunitySignalResponse(
                CommunitySignalKind.Volunteer,
                "Volunteer",
                false,
                "Has offered time to support community activities.",
                ["Volunteer intent recorded during event registration"]));
        }

        signals.AddRange(signalAwards
            .Where(award => award.Kind == CommunitySignalKind.Champion && award.RevokedAt is null)
            .Select(award => new CommunitySignalResponse(
                CommunitySignalKind.Champion,
                "Champion",
                true,
                award.Rationale,
                award.EvidenceEntryIds.Count == 0
                    ? ["Explicit organizer recognition"]
                    : [$"{award.EvidenceEntryIds.Count} verified evidence record(s)"])));

        return signals;
    }

    private static void AddDerivedSignal(
        List<CommunitySignalResponse> signals,
        CommunitySignalKind kind,
        string label,
        IEnumerable<ParticipationLedgerEntry> evidenceEntries,
        string explanation)
    {
        var evidence = evidenceEntries.Take(3).Select(entry => entry.Evidence).ToArray();
        if (evidence.Length > 0)
        {
            signals.Add(new CommunitySignalResponse(kind, label, false, explanation, evidence));
        }
    }

    private static List<GrowthPathwayResponse> BuildPathways(
        IReadOnlyCollection<ParticipationLedgerEntry> ledgerEntries,
        IReadOnlyCollection<Registration> registrations)
    {
        var attendedEventIds = ledgerEntries
            .Where(entry => entry.Activity == ParticipationActivityKind.Attended && entry.EventId.HasValue)
            .Select(entry => entry.EventId!.Value)
            .Concat(registrations
                .Where(registration => registration.Status == RegistrationStatus.CheckedIn)
                .Select(registration => registration.EventId))
            .Distinct()
            .Count();
        var attendedWithoutEvent = ledgerEntries.Count(entry =>
            entry.Activity == ParticipationActivityKind.Attended && !entry.EventId.HasValue);
        var attended = attendedEventIds + attendedWithoutEvent;
        var volunteered = ledgerEntries.Count(entry => entry.Activity == ParticipationActivityKind.Volunteered);
        var spoke = ledgerEntries.Count(entry => entry.Activity is ParticipationActivityKind.Spoke or ParticipationActivityKind.SubmittedSession);
        var mentored = ledgerEntries.Count(entry => entry.Activity == ParticipationActivityKind.Mentored);
        var organized = ledgerEntries.Count(entry => entry.Activity is ParticipationActivityKind.Organized or ParticipationActivityKind.LedProgram);
        var maintained = ledgerEntries.Count(entry => entry.Activity is ParticipationActivityKind.ProjectContributed or ParticipationActivityKind.Maintained);

        return
        [
            BuildPathway("Participation", attended, ("First event", 1), ("Regular participant", 3), ("Community regular", 6)),
            BuildPathway("Volunteer", volunteered, ("Volunteer", 1), ("Lead volunteer", 3), ("Volunteer captain", 6)),
            BuildPathway("Speaker", spoke, ("Speaker", 1), ("Regular speaker", 3), ("Community speaker", 6)),
            BuildPathway("Mentor", mentored, ("Mentor", 1), ("Senior mentor", 3), ("Community mentor", 6)),
            BuildPathway("Organizer", organized, ("Coordinator", 1), ("Organizer", 3), ("Community lead", 6)),
            BuildPathway("Maintainer", maintained, ("Contributor", 1), ("Maintainer", 3), ("Trusted maintainer", 6))
        ];
    }

    private static GrowthPathwayResponse BuildPathway(
        string name,
        int evidenceCount,
        params (string Name, int Threshold)[] definitions)
    {
        var currentIndex = Array.FindLastIndex(definitions, definition => evidenceCount >= definition.Threshold);
        var milestones = definitions.Select((definition, index) => new GrowthMilestoneResponse(
            definition.Name,
            index <= currentIndex ? "Completed" : index == currentIndex + 1 ? "Current" : "Upcoming",
            index <= currentIndex
                ? "Verified by participation evidence."
                : $"Continue contributing through {name.ToLowerInvariant()} activities.")).ToArray();
        var currentStage = currentIndex >= 0 ? definitions[currentIndex].Name : "Getting started";
        return new GrowthPathwayResponse(name, currentStage, milestones);
    }

    private static List<ActivityDayResponse> BuildActivity(IReadOnlyCollection<PassportContributionResponse> contributions)
        => contributions
            .Where(contribution => contribution.OccurredAt >= DateTimeOffset.UtcNow.AddYears(-1))
            .GroupBy(contribution => DateOnly.FromDateTime(contribution.OccurredAt.Date))
            .OrderBy(group => group.Key)
            .Select(group => new ActivityDayResponse(
                group.Key,
                group.Count(),
                group.Select(contribution => contribution.Type).Distinct(StringComparer.Ordinal).ToArray()))
            .ToList();

    private static PassportPortfolioResponse BuildPortfolio(
        CommunityMember member,
        IReadOnlyCollection<PassportContributionResponse> contributions)
    {
        var verified = contributions
            .Where(contribution => contribution.Type is "Volunteered" or "Mentored" or "Spoke" or "Organized"
                or "Project contributed" or "Maintained" or "Content created" or "Led program")
            .Take(8)
            .Select(contribution => new VerifiedPortfolioHighlightResponse(
                contribution.Title,
                contribution.Description,
                $"{contribution.Type} · {contribution.OccurredAt:dd MMM yyyy}"))
            .ToArray();
        var entries = member.PortfolioEntries
            .OrderByDescending(entry => entry.IsFeatured)
            .ThenBy(entry => entry.DisplayOrder)
            .Select(entry => new PortfolioEntryResponse(
                entry.Id.Value,
                entry.Title,
                entry.Description,
                entry.IsFeatured,
                entry.DisplayOrder,
                entry.Links.Select(link => new PortfolioLinkResponse(link.Kind, link.Url, link.Label)).ToArray(),
                entry.EvidenceEntryIds))
            .ToArray();
        return new PassportPortfolioResponse(verified, entries);
    }

    private async Task<List<PassportConnectionResponse>> BuildConnectionsAsync(
        CommunityMember member,
        List<Registration> registrations,
        bool isOrganizer,
        CancellationToken ct)
    {
        var storedRelationships = await db.CommunityRelationships.AsNoTracking()
            .Where(relationship => relationship.SourceMemberId == member.Id)
            .OrderByDescending(relationship => relationship.UpdatedAt)
            .Take(12)
            .ToListAsync(ct);
        var targetIds = storedRelationships.Select(relationship => relationship.TargetMemberId).Distinct().ToArray();
        var targets = targetIds.Length == 0
            ? []
            : await db.CommunityMembers.AsNoTracking()
                .Where(candidate => targetIds.Contains(candidate.Id)
                    && candidate.EnableRelationshipInsights
                    && candidate.AppearInCollaboratorDiscovery
                    && (!isOrganizer || candidate.ShareParticipationWithOrganizers)
                    && candidate.Visibility != ProfileVisibilityScope.Private)
                .ToDictionaryAsync(candidate => candidate.Id, ct);

        var connections = storedRelationships
            .Where(relationship => targets.ContainsKey(relationship.TargetMemberId))
            .Select(relationship => new PassportConnectionResponse(
                relationship.TargetMemberId.Value,
                targets[relationship.TargetMemberId].DisplayName,
                relationship.Kind,
                relationship.Context,
                $"This connection exists because {relationship.Context.TrimEnd('.').ToLowerInvariant()}."))
            .ToList();

        if (connections.Count > 0 || !member.AppearInCollaboratorDiscovery)
        {
            return connections;
        }

        var eventIds = registrations
            .Where(registration => registration.Status == RegistrationStatus.CheckedIn)
            .Select(registration => registration.EventId)
            .Distinct()
            .ToArray();
        if (eventIds.Length == 0)
        {
            return connections;
        }

        var candidates =
            from registration in db.Registrations.AsNoTracking()
            join candidate in db.CommunityMembers.AsNoTracking()
                on registration.CommunityMemberId equals (CommunityMemberId?)candidate.Id
            where eventIds.Contains(registration.EventId)
                && registration.Status == RegistrationStatus.CheckedIn
                && candidate.Id != member.Id
                && candidate.EnableRelationshipInsights
                && candidate.AppearInCollaboratorDiscovery
                && (!isOrganizer || candidate.ShareParticipationWithOrganizers)
                && candidate.Visibility != ProfileVisibilityScope.Private
            group candidate by new { candidate.Id, candidate.DisplayName } into grouped
            orderby grouped.Count() descending, grouped.Key.DisplayName, grouped.Key.Id
            select new CoAttendeeCandidate(grouped.Key.Id, grouped.Key.DisplayName, grouped.Count());
        var coAttendees = await candidates.Take(6).ToListAsync(ct);
        connections.AddRange(coAttendees.Select(candidate => new PassportConnectionResponse(
            candidate.MemberId.Value,
            candidate.DisplayName,
            CommunityRelationshipKind.CoAttendee,
            $"Attended {candidate.SharedEventCount} community event(s) together",
            "This connection is suggested from verified shared event participation.")));
        return connections;
    }

    private sealed record CoAttendeeCandidate(
        CommunityMemberId MemberId,
        string DisplayName,
        int SharedEventCount);

    private sealed record ContributionEventSource(
        string Title,
        DateTimeOffset StartDate);

    private sealed record DirectoryRegistrationSignal(
        CommunityMemberId MemberId,
        string? Intent,
        string? Goals,
        List<string> ContributionPreferences);

    private static List<PassportOpportunityResponse> BuildOpportunities(
        IEnumerable<MemberOpportunity> opportunities)
        => opportunities
            .OrderByDescending(opportunity => opportunity.OfferedAt)
            .Select(opportunity => new PassportOpportunityResponse(
                opportunity.Id.Value,
                opportunity.Kind,
                opportunity.Title,
                opportunity.Description,
                opportunity.CurrentStatus,
                opportunity.Outcome,
                opportunity.OfferedAt,
                opportunity.Lifecycle
                    .Select(item => new OpportunityLifecycleEventResponse(item.Status, item.OccurredAt, item.Explanation))
                    .ToArray()))
            .ToList();

    private static PassportPrivacyPreferencesResponse ToPrivacy(CommunityMember member)
        => new(
            member.Visibility,
            member.ShareParticipationWithOrganizers,
            member.AppearInMentorshipRecommendations,
            member.AppearInOpportunityRecommendations,
            member.AppearInCollaboratorDiscovery,
            member.AppearInSpeakerRecommendations,
            member.AppearInVolunteerLeadershipRecommendations,
            member.EnableRelationshipInsights,
            member.ReceiveOpportunityRecommendations);

    private static bool HasVolunteerSignal(Registration registration)
        => HasVolunteerSignal(
            registration.ContributionPreferences,
            registration.Intent,
            registration.Goals);

    private static bool HasVolunteerSignal(DirectoryRegistrationSignal registration)
        => HasVolunteerSignal(
            registration.ContributionPreferences,
            registration.Intent,
            registration.Goals);

    private static bool HasVolunteerSignal(
        IEnumerable<string> contributionPreferences,
        string? intent,
        string? goals)
        => contributionPreferences.Any(preference =>
               preference.Contains("volunteer", StringComparison.OrdinalIgnoreCase))
           || intent?.Contains("volunteer", StringComparison.OrdinalIgnoreCase) == true
           || goals?.Contains("volunteer", StringComparison.OrdinalIgnoreCase) == true;

    private static string FormatActivity(ParticipationActivityKind activity)
        => activity switch
        {
            ParticipationActivityKind.Registered => "Registered",
            ParticipationActivityKind.Waitlisted => "Waitlisted",
            ParticipationActivityKind.Attended => "Attended",
            ParticipationActivityKind.Volunteered => "Volunteered",
            ParticipationActivityKind.SubmittedSession => "Submitted session",
            ParticipationActivityKind.Mentored => "Mentored",
            ParticipationActivityKind.Spoke => "Spoke",
            ParticipationActivityKind.Organized => "Organized",
            ParticipationActivityKind.ProjectContributed => "Project contributed",
            ParticipationActivityKind.ContentCreated => "Content created",
            ParticipationActivityKind.Maintained => "Maintained",
            ParticipationActivityKind.Moderated => "Moderated",
            ParticipationActivityKind.LedProgram => "Led program",
            _ => activity.ToString()
        };

    private static string ImpactArea(ParticipationActivityKind activity)
        => activity switch
        {
            ParticipationActivityKind.Mentored => "Learning",
            ParticipationActivityKind.Spoke or ParticipationActivityKind.ContentCreated => "Knowledge sharing",
            ParticipationActivityKind.Volunteered or ParticipationActivityKind.Organized
                or ParticipationActivityKind.LedProgram => "Community operations",
            ParticipationActivityKind.ProjectContributed or ParticipationActivityKind.Maintained => "Building",
            _ => "Participation"
        };

    private static string GetInitials(string displayName)
        => string.Concat(displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0])));

    private static string EscapeLikePattern(string value)
        => value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
