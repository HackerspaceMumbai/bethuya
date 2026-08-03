using System.Diagnostics;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Planning;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Backend.Agents;

/// <summary>
/// Planner invoker implementation using Foundry-compatible Responses protocol.
/// </summary>
public sealed class FoundryResponsesInvoker(
    IPlannerResponsesApi responsesApi,
    IOptions<PlannerInvokerOptions> options) : IAgentInvoker
{
    private readonly PlannerInvokerOptions _options = options.Value;

    public async Task<PlannerInvocationResult> InvokePlannerAsync(
        PlannerInvocationInput input,
        string conversationId,
        string workItemId,
        string? traceParent,
        string? correlationId,
        CancellationToken ct = default)
    {
        var activity = Activity.Current;
        activity?.SetTag("gen_ai.system", "foundry");
        activity?.SetTag("gen_ai.request.model", _options.Model ?? "planner-chat");
        activity?.SetTag("gen_ai.operation.name", "planner.schedule_draft");
        activity?.SetTag("mcp.server.identity", "planner-hosted");
        activity?.SetTag("mcp.tool.name", "planner.responses");

        var apiResponse = await responsesApi.CreateResponseAsync(
            new PlannerResponsesApiRequest(
                ConversationId: conversationId,
                WorkItemId: workItemId,
                Input: input,
                Model: _options.Model),
            traceParent,
            correlationId,
            ct);

        activity?.SetTag("gen_ai.response.id", apiResponse.ResponseId);
        activity?.SetTag("bethuya.agent.name", apiResponse.AgentName);
        activity?.SetTag("bethuya.agent.version", apiResponse.AgentVersion ?? "unknown");

        var schemaErrors = PlanningAgendaValidator.Validate(apiResponse.AgendaJson);
        if (schemaErrors.Count > 0)
        {
            throw new InvalidOperationException($"Planner agenda_json failed validation: {string.Join("; ", schemaErrors)}");
        }

        var markdownErrors = PlanningAgendaValidator.ValidateMarkdownConsistency(apiResponse.AgendaJson, apiResponse.MarkdownAgenda);
        if (markdownErrors.Count > 0)
        {
            throw new InvalidOperationException($"Planner markdown_agenda is inconsistent with agenda_json: {string.Join("; ", markdownErrors)}");
        }

        return new PlannerInvocationResult(
            MarkdownAgenda: apiResponse.MarkdownAgenda,
            AgendaJson: apiResponse.AgendaJson,
            ResponseId: apiResponse.ResponseId,
            AgentName: apiResponse.AgentName,
            AgentVersionTag: apiResponse.AgentVersion);
    }
}
