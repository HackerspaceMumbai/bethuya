namespace ServiceDefaults.Auth.Observability;

/// <summary>
/// Canonical <c>resource.type</c> values for authorization audit records and metric dimensions. These
/// are coarse resource categories (not entity ids), so they carry no PII and stay low-cardinality for
/// metrics. Centralised here to avoid magic strings at the audit call sites.
/// </summary>
public static class BethuyaAuditResourceTypes
{
    /// <summary>An attendee registration (owned by the registering attendee).</summary>
    public const string Registration = "registration";

    /// <summary>An event (organizer-owned provenance via <c>CreatedBy</c>).</summary>
    public const string Event = "event";

    /// <summary>A curation decision applied to a registrant.</summary>
    public const string CurationDecision = "curation-decision";

    /// <summary>An approval/rejection decision in the approval workflow.</summary>
    public const string Approval = "approval";

    /// <summary>A planning cycle (draft/approve/publish lifecycle).</summary>
    public const string PlanningCycle = "planning-cycle";
}
