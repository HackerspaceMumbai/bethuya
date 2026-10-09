using System.Text.Json;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// A member-authored portfolio item describing work they are proud of.
/// </summary>
public sealed class CommunityPortfolioEntry
{
    public CommunityPortfolioEntryId Id { get; init; } = CommunityPortfolioEntryId.From(Guid.CreateVersion7());
    public CommunityMemberId CommunityMemberId { get; init; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public bool IsFeatured { get; set; }
    public int DisplayOrder { get; set; }
    public string LinksJson { get; set; } = "[]";
    public string EvidenceEntryIdsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CommunityMember? CommunityMember { get; init; }

    public IReadOnlyList<PortfolioLink> Links
    {
        get => Deserialize<List<PortfolioLink>>(LinksJson);
        set => LinksJson = JsonSerializer.Serialize(value);
    }

    public IReadOnlyList<Guid> EvidenceEntryIds
    {
        get => Deserialize<List<Guid>>(EvidenceEntryIdsJson);
        set => EvidenceEntryIdsJson = JsonSerializer.Serialize(value);
    }

    private static T Deserialize<T>(string json) where T : new()
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }
}

/// <summary>
/// One typed external reference attached to a portfolio item.
/// </summary>
public sealed record PortfolioLink(PortfolioLinkKind Kind, string Url, string? Label);
