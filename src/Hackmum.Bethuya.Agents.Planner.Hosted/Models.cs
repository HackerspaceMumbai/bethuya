using System.Text.Json.Serialization;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Agents.Planner.Hosted;

public sealed record PlannerResponsesRequest(
    [property: JsonPropertyName("conversation")] string? Conversation,
    [property: JsonPropertyName("work_item_id")] string WorkItemId,
    [property: JsonPropertyName("input")] PlannerHostedInput Input,
    [property: JsonPropertyName("model")] string? Model);

public sealed record PlannerHostedInput(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("timezone")] string Timezone,
    [property: JsonPropertyName("location")] string? Location,
    [property: JsonPropertyName("capacity")] int Capacity,
    [property: JsonPropertyName("constraints")] string? Constraints,
    [property: JsonPropertyName("priorEventsContext")] string? PriorEventsContext,
    [property: JsonPropertyName("humanEditedMarkdown")] string? HumanEditedMarkdown);

public sealed record PlannerResponsesSuccess(
    [property: JsonPropertyName("id")] string ResponseId,
    [property: JsonPropertyName("conversation")] string ConversationId,
    [property: JsonPropertyName("markdown_agenda")] string MarkdownAgenda,
    [property: JsonPropertyName("agenda_json")] PlanningAgendaJson AgendaJson,
    [property: JsonPropertyName("agent_name")] string AgentName,
    [property: JsonPropertyName("agent_version")] string AgentVersion);

public sealed record PlannerHybridResponse(
    string MarkdownAgenda,
    PlanningAgendaJson AgendaJson);

