namespace Hackmum.Bethuya.Backend.Contracts;

public sealed record CreateRegistrationRequest(
    Guid EventId,
    string FullName,
    string Email,
    string? Bio,
    List<string> Interests);
