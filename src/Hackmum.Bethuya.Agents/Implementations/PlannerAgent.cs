using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.AI.Routing;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Hackmum.Bethuya.Agents.Implementations;

/// <summary>
/// Planner Agent — drafts agendas, timings, and speaker suggestions.
/// Uses Azure OpenAI (non-sensitive content).
/// </summary>
public sealed class PlannerAgent(IAIRouter router, ILogger<PlannerAgent> logger)
    : AgentBase<PlannerRequest, PlannerResponse>(router, logger)
{
    public override string Name => "Planner";

    protected override IList<ChatMessage> BuildPrompt(PlannerRequest request)
    {
        var systemPrompt = """
            You are the Planner Agent for Bethuya, a community event platform.
            Your job is to draft an event agenda with sessions, timings, and speaker suggestions.
            
            Guidelines:
            - Create a balanced agenda with varied session types
            - Suggest realistic time slots (30-60 min per session)
            - Include breaks and networking time
            - Consider the event type and capacity
            
            Respond with a structured agenda in JSON format:
            {
                "sessions": [
                    { "title": "...", "speaker": "...", "startTime": "HH:mm", "endTime": "HH:mm", "description": "..." }
                ],
                "reasoning": "..."
            }
            """;

        var userPrompt = $"""
            Event: {request.Event.Title}
            Type: {request.Event.Type}
            Capacity: {request.Event.Capacity}
            Start: {request.Event.StartDate:g}
            End: {request.Event.EndDate:g}
            Description: {request.Event.Description ?? "N/A"}
            Constraints: {request.Constraints ?? "None"}
            Prior events context: {request.PriorEventsContext ?? "None"}
            """;

        return
        [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ];
    }

    protected override PlannerResponse ParseResponse(ChatResponse response, PlannerRequest request)
    {
        var content = response.Text ?? "";

        var agenda = new Agenda
        {
            EventId = request.Event.Id,
            Status = AgendaStatus.ProposedByAgent,
            CreatedByAgent = Name,
            Sessions =
            [
                new AgendaSession
                {
                    Title = "Opening & Welcome",
                    StartTime = TimeOnly.FromDateTime(request.Event.StartDate.DateTime),
                    EndTime = TimeOnly.FromDateTime(request.Event.StartDate.AddMinutes(30).DateTime),
                    Description = "Welcome and introductions",
                    Order = 1
                }
            ]
        };

        return new PlannerResponse(
            DraftAgenda: agenda,
            RequiresHumanApproval: true,
            AgentReasoning: content);
    }
}
