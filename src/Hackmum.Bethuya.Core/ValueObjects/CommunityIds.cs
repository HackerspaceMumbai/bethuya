using Vogen;

namespace Hackmum.Bethuya.Core.ValueObjects;

[ValueObject<Guid>]
public readonly partial struct CommunityMemberId
{
    private static Validation Validate(Guid value)
        => value != Guid.Empty
            ? Validation.Ok
            : Validation.Invalid("Community member id cannot be empty.");
}

[ValueObject<Guid>]
public readonly partial struct ExternalIdentityId
{
    private static Validation Validate(Guid value)
        => value != Guid.Empty
            ? Validation.Ok
            : Validation.Invalid("External identity id cannot be empty.");
}
