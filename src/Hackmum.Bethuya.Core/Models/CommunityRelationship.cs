using System.Text.Json;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Evidence-backed relationship edge used by the member-facing passport.
/// </summary>
public sealed class CommunityRelationship
{
    public CommunityRelationshipId Id { get; init; } = CommunityRelationshipId.From(Guid.CreateVersion7());
    public CommunityMemberId SourceMemberId { get; init; }
    public CommunityMemberId TargetMemberId { get; init; }
    public CommunityRelationshipKind Kind { get; init; }
    public required string Context { get; set; }
    public string EvidenceEntryIdsJson { get; set; } = "[]";
    public DateTimeOffset EstablishedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<Guid> EvidenceEntryIds
    {
        get
        {
            try
            {
                return JsonSerializer.Deserialize<List<Guid>>(EvidenceEntryIdsJson) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
        set => EvidenceEntryIdsJson = JsonSerializer.Serialize(value);
    }
}
