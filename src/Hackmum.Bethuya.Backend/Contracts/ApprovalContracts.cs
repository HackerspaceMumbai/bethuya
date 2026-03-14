namespace Hackmum.Bethuya.Backend.Contracts;

public sealed record ApproveRequest(string? Reason = null);
public sealed record RejectRequest(string Reason);
