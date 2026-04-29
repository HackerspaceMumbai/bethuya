using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Refit;
using Bethuya.Hybrid.Shared.Models;

namespace Bethuya.Hybrid.Shared.Services;

/// <summary>Refit-generated typed HTTP client for the Bethuya Events API.</summary>
public interface IEventApi
{
    [Get("/api/events")]
    Task<List<EventDto>> GetAllAsync(CancellationToken ct = default);

    [Get("/api/events/{id}")]
    Task<EventDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    [Get("/api/events/slug/{hashtag}")]
    Task<EventDto> GetByHashtagAsync(string hashtag, CancellationToken ct = default);

    [Post("/api/events")]
    Task<EventDto> PlanAsync([Body] PlanEventDto request, CancellationToken ct = default);

    [Put("/api/events/{id}")]
    Task<EventDto> UpdateAsync(Guid id, [Body] UpdateEventDto request, CancellationToken ct = default);

    [Multipart]
    [Post("/api/images/upload")]
    Task<ImageUploadResponse> UploadImageAsync([AliasAs("file")] StreamPart file, CancellationToken ct = default);

    [Post("/api/agents/recommend-dates")]
    Task<RecommendDatesResponse> RecommendDatesAsync([Body] RecommendDatesRequest request, CancellationToken ct = default);

    [Get("/api/events/{id}/agenda-draft")]
    Task<AgendaDraftDto?> GetAgendaDraftAsync(Guid id, CancellationToken ct = default);

    [Post("/api/approvals/agenda")]
    Task ApproveAgendaAsync([Body] ApprovalRequestDto approval, CancellationToken ct = default);

    [Post("/api/approvals/agenda/reject")]
    Task RejectAgendaAsync([Body] RejectionRequestDto rejection, CancellationToken ct = default);

    [Post("/api/events/{id}/orchestrate/plan")]
    Task<OrchestrationResponseDto> TriggerPlannerAsync(Guid id, [Body] TriggerPlannerRequestDto request, CancellationToken ct = default);

    [Get("/api/events/{id}/curation-proposal")]
    Task<CurationProposalDto?> GetCurationProposalAsync(Guid id, CancellationToken ct = default);

    [Post("/api/approvals/curation")]
    Task ApproveCurationAsync([Body] CurationApprovalRequest request, CancellationToken ct = default);

    [Post("/api/approvals/curation/reject")]
    Task RejectCurationAsync([Body] CurationRejectionRequest request, CancellationToken ct = default);

    [Get("/api/audit-log/curation")]
    Task<CurationAuditLogDto?> GetCurationAuditLogAsync(Guid eventId, CancellationToken ct = default);
}

/// <summary>Event data returned from the API.</summary>
public sealed record EventDto(
    Guid Id,
    string Title,
    string? Description,
    string Type,
    string Status,
    int Capacity,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Location,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? Hashtag,
    string? CoverImageUrl);

/// <summary>Payload sent to plan a new event.</summary>
public sealed record PlanEventDto(
    string Title,
    string? Description,
    string Type,
    int Capacity,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Location,
    string CreatedBy,
    string? Hashtag,
    string? CoverImageUrl,
    string Status = "Draft");

/// <summary>Payload sent to update an existing event.</summary>
public sealed record UpdateEventDto(
    string Title,
    string? Description,
    string Type,
    int Capacity,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Location,
    string Status,
    string? CoverImageUrl);

/// <summary>Response from the image upload endpoint.</summary>
public sealed record ImageUploadResponse([property: JsonPropertyName("url")] string Url);

/// <summary>Request to recommend optimal event dates via AI.</summary>
public sealed record RecommendDatesRequest(
    string? Title = null,
    string? Type = null,
    string? Description = null,
    string? Location = null,
    int? Capacity = null);

/// <summary>AI-recommended date and time for an event.</summary>
public sealed record RecommendDatesResponse(
    string StartDate,
    string StartTime,
    string EndDate,
    string EndTime,
    string? Reasoning = null);

/// <summary>Agenda draft from the Planner agent.</summary>
public sealed record AgendaDraftDto(
    Guid EventId,
    string Title,
    string? Description,
    string? Theme,
    string? ThemeRationale,
    List<SessionDraftDto> Sessions,
    DateTime CreatedAt,
    string? AgentReasoning);

/// <summary>Session draft in an agenda.</summary>
public sealed record SessionDraftDto(
    string Id,
    string Title,
    string? Speaker,
    DateTime? StartTime,
    DateTime? EndTime,
    string? Description);

/// <summary>Request to approve an agenda draft.</summary>
public sealed record ApprovalRequestDto(
    Guid EventId,
    string ApprovedBy,
    string EditsJson,
    string Status = "Approved");

/// <summary>Request to reject an agenda draft.</summary>
public sealed record RejectionRequestDto(
    Guid EventId,
    string RejectedBy,
    string Reason,
    string Status = "Rejected");

/// <summary>Request to trigger the Planner agent.</summary>
public sealed record TriggerPlannerRequestDto(
    string OrganizerEmail);

/// <summary>Response from triggering an orchestration workflow.</summary>
public sealed record OrchestrationResponseDto(
    string WorkflowId,
    string Status,
    string Message);
