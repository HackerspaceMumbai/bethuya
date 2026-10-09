using System.Text.Json;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Durable member opportunity with an append-only lifecycle history.
/// </summary>
public sealed class MemberOpportunity
{
    public MemberOpportunityId Id { get; init; } = MemberOpportunityId.From(Guid.CreateVersion7());
    public CommunityMemberId CommunityMemberId { get; init; }
    public MemberOpportunityKind Kind { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public MemberOpportunityStatus CurrentStatus { get; set; } = MemberOpportunityStatus.Offered;
    public string? Outcome { get; set; }
    public string LifecycleJson { get; set; } = "[]";
    public DateTimeOffset OfferedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CommunityMember? CommunityMember { get; init; }

    public IReadOnlyList<MemberOpportunityLifecycleEvent> Lifecycle
    {
        get
        {
            try
            {
                return JsonSerializer.Deserialize<List<MemberOpportunityLifecycleEvent>>(LifecycleJson) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
        set => LifecycleJson = JsonSerializer.Serialize(value);
    }
}

/// <summary>
/// One immutable opportunity lifecycle transition.
/// </summary>
public sealed record MemberOpportunityLifecycleEvent(
    MemberOpportunityStatus Status,
    DateTimeOffset OccurredAt,
    string Explanation);
