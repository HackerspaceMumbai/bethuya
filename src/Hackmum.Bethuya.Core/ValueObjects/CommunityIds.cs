using Vogen;

namespace Hackmum.Bethuya.Core.ValueObjects;

/// <summary>
/// Stable identifier for a community member profile.
/// </summary>
[ValueObject<Guid>]
public readonly partial struct CommunityMemberId
{
    private static Validation Validate(Guid value)
        => value != Guid.Empty
            ? Validation.Ok
            : Validation.Invalid("Community member id cannot be empty.");
}

/// <summary>
/// Stable identifier for a linked external identity entry.
/// </summary>
[ValueObject<Guid>]
public readonly partial struct ExternalIdentityId
{
    private static Validation Validate(Guid value)
        => value != Guid.Empty
            ? Validation.Ok
            : Validation.Invalid("External identity id cannot be empty.");
}

/// <summary>
/// Stable identifier for an event referenced by passport timeline entries.
/// </summary>
[ValueObject<Guid>]
public readonly partial struct EventId
{
    private static Validation Validate(Guid value)
        => value != Guid.Empty
            ? Validation.Ok
            : Validation.Invalid("Event id cannot be empty.");
}
