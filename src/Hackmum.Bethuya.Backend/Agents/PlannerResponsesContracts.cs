using System.Text.Json.Serialization;
using Hackmum.Bethuya.Core.Models;
using Refit;

namespace Hackmum.Bethuya.Backend.Agents;

/// <summary>
/// Refit client for planner hosted-agent Responses protocol endpoint.
/// </summary>
public interface IPlannerResponsesApi
{
    [Post("/responses")]
    Task<PlannerResponsesApiResponse> CreateResponseAsync(
        [Body] PlannerResponsesApiRequest request,
        [Header("traceparent")] string? traceParent = null,
        [Header("x-correlation-id")] string? correlationId = null,
        CancellationToken ct = default);
}

public sealed record PlannerResponsesApiRequest(
    [property: JsonPropertyName("conversation")] string ConversationId,
    [property: JsonPropertyName("work_item_id")] string WorkItemId,
    [property: JsonPropertyName("input")] PlannerInvocationInput Input,
    [property: JsonPropertyName("model")] string? Model = null);

public sealed record PlannerResponsesApiResponse(
    [property: JsonPropertyName("id")] string? ResponseId,
    [property: JsonPropertyName("markdown_agenda")] string MarkdownAgenda,
    [property: JsonPropertyName("agenda_json")] PlanningAgendaJson AgendaJson,
    [property: JsonPropertyName("agent_name")] string AgentName,
    [property: JsonPropertyName("agent_version")] string? AgentVersion);

