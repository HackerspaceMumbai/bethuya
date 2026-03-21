using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.AI.Routing;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Hackmum.Bethuya.Agents.Implementations;

/// <summary>
/// Reporter Agent — drafts post-event summaries, highlights, and action items.
/// Human edits before publish; attribution preserved.
/// </summary>
public sealed class ReporterAgent(IAIRouter router, ILogger<ReporterAgent> logger)
    : AgentBase<ReporterRequest, ReporterResponse>(router, logger)
{
    public override string Name => "Reporter";

    protected override IList<ChatMessage> BuildPrompt(ReporterRequest request)
    {
        var systemPrompt = """
            You are the Reporter Agent for Bethuya, a community event platform.
            Draft a post-event report with:
            1. Executive summary (2-3 paragraphs)
            2. Key highlights (5-7 bullet points)
            3. Action items (3-5 concrete next steps)
            
            The report will be edited by humans before publishing.
            Maintain a professional, community-focused tone.
            Include attribution where appropriate.
            """;

        var userPrompt = $"""
            Event: {request.Event.Title}
            Type: {request.Event.Type}
            Date: {request.Event.StartDate:D} - {request.Event.EndDate:D}
            Capacity: {request.Event.Capacity}
            Session Notes: {request.SessionNotes ?? "No notes captured"}
            """;

        return
        [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ];
    }

    protected override ReporterResponse ParseResponse(ChatResponse response, ReporterRequest request)
    {
        var content = response.Text ?? "";

        var report = new EventReport
        {
            EventId = request.Event.Id,
            Summary = content,
            Highlights = ["Event completed successfully"],
            ActionItems = ["Follow up with attendees", "Publish event materials"],
            Status = ProposalStatus.PendingReview,
            DraftedByAgent = Name
        };

        return new ReporterResponse(
            DraftReport: report,
            RequiresHumanApproval: true,
            AgentReasoning: "Draft report generated from event details and session notes");
    }
}
