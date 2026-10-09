using System.Text.Json;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Auditable organizer-awarded community recognition.
/// </summary>
public sealed class CommunitySignalAward
{
    public CommunitySignalAwardId Id { get; init; } = CommunitySignalAwardId.From(Guid.CreateVersion7());
    public CommunityMemberId CommunityMemberId { get; init; }
    public CommunitySignalKind Kind { get; init; } = CommunitySignalKind.Champion;
    public required string Rationale { get; init; }
    public string EvidenceEntryIdsJson { get; init; } = "[]";
    public required string AwardedBy { get; init; }
    public DateTimeOffset AwardedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? RevokedBy { get; set; }
    public string? RevocationReason { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public CommunityMember? CommunityMember { get; init; }

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
        init => EvidenceEntryIdsJson = JsonSerializer.Serialize(value);
    }
}
