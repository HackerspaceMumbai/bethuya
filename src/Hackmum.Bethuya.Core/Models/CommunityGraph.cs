using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>A bounded, privacy-filtered graph projected from verified participation.</summary>
public sealed record CommunityGraphSnapshot(
    IReadOnlyList<CommunityGraphNode> Nodes,
    IReadOnlyList<CommunityGraphRelationship> Relationships,
    IReadOnlyList<CommunityGraphOpportunity> Opportunities,
    IReadOnlyList<CommunityGraphHealth> Health,
    DateTimeOffset AsOf,
    bool IsTruncated,
    IReadOnlyList<CommunityGraphEvidence> Evidence);

/// <summary>A navigable entity; all details come from participation records.</summary>
public sealed record CommunityGraphNode(GraphNodeId Id, string Kind, string Label, string Description);

/// <summary>An explainable relationship with its complete supporting records in this snapshot.</summary>
public sealed record CommunityGraphRelationship(GraphNodeId Source, GraphNodeId Target, string Kind,
    IReadOnlyList<ParticipationLedgerEntryId> EvidenceIds);

/// <summary>Ledger proof, excluding private connector identity and correlation fields.</summary>
public sealed record CommunityGraphEvidence(ParticipationLedgerEntryId EntryId, string Summary,
    string Activity, string Connector, DateTimeOffset OccurredAt, GraphNodeId MemberId);

/// <summary>A suggested pathway, not a published vacancy or an automatic selection.</summary>
public sealed record CommunityGraphOpportunity(GraphNodeId Id, GraphNodeId MemberId, string Title,
    string Reason, IReadOnlyList<ParticipationLedgerEntryId> EvidenceIds);

/// <summary>An in-graph health annotation with a measurable basis.</summary>
public sealed record CommunityGraphHealth(string Title, string Detail);
