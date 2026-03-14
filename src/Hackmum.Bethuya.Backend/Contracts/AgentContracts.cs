namespace Hackmum.Bethuya.Backend.Contracts;

public sealed record InvokePlannerRequest(
    string? Constraints = null,
    string? PriorEventsContext = null,
    string? RequestedBy = null);

public sealed record InvokeCuratorRequest(
    Dictionary<string, double> DiversityTargets,
    List<string> EquityPrompts,
    string? RequestedBy = null);

public sealed record InvokeFacilitatorRequest(
    string? CurrentSessionTitle = null,
    string? RequestedBy = null);

public sealed record InvokeReporterRequest(
    string? SessionNotes = null,
    string? RequestedBy = null);
