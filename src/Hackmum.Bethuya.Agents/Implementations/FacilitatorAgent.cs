using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.AI.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Hackmum.Bethuya.Agents.Implementations;

/// <summary>
/// Facilitator Agent — live assistance with prompts, Q&amp;A suggestions, and note capture.
/// Organizer-controlled: all outputs require explicit publish action.
/// </summary>
public sealed class FacilitatorAgent(IAIRouter router, ILogger<FacilitatorAgent> logger)
    : AgentBase<FacilitatorRequest, FacilitatorResponse>(router, logger)
{
    public override string Name => "Facilitator";

    protected override IList<ChatMessage> BuildPrompt(FacilitatorRequest request)
    {
        var systemPrompt = """
            You are the Facilitator Agent for Bethuya, a community event platform.
            You assist organizers during live events with:
            - Discussion prompts relevant to the current session
            - Q&A suggestions to engage the audience
            - Note-taking suggestions to capture key points
            
            All suggestions are opt-in. The organizer controls what is shared.
            """;

        var currentSession = request.CurrentSessionTitle ?? "General";
        var userPrompt = $"""
            Event: {request.Event.Title}
            Current Session: {currentSession}
            Agenda Sessions: {string.Join(", ", request.Agenda.Sessions.Select(s => s.Title))}
            
            Generate 3 discussion prompts, 3 Q&A suggestions, and a note-taking template.
            """;

        return
        [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ];
    }

    protected override FacilitatorResponse ParseResponse(ChatResponse response, FacilitatorRequest request)
    {
        var content = response.Text ?? "";

        return new FacilitatorResponse(
            SuggestedPrompts:
            [
                "What's your experience with this topic?",
                "How would you apply this in your projects?",
                "What challenges have you faced in this area?"
            ],
            QASuggestions:
            [
                "What tools or frameworks do you recommend?",
                "How does this compare to alternative approaches?",
                "What are the key takeaways from this session?"
            ],
            SessionNotes: content,
            RequiresHumanApproval: true,
            AgentReasoning: content);
    }
}
