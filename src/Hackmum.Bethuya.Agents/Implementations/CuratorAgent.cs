using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.AI.Routing;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Hackmum.Bethuya.Agents.Implementations;

/// <summary>
/// Curator Agent — responsible attendee curation for oversubscribed events.
/// ALWAYS uses DataSensitivity.Sensitive (Foundry Local) for PII protection.
/// NEVER auto-accepts or auto-rejects — outputs are recommendations only.
/// All curation insights are explainable and transparent.
/// 
/// CRITICAL: This agent MUST run on Foundry Local only.
/// Attribute [RequiresLocalProvider] enforces this constraint at runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiresLocalProviderAttribute : Attribute
{
}

[RequiresLocalProvider]
public sealed class CuratorAgent(IAIRouter router, ILogger<CuratorAgent> logger)
    : AgentBase<CuratorRequest, CuratorResponse>(router, logger)
{
    public override string Name => "Curator";

    protected override IList<ChatMessage> BuildPrompt(CuratorRequest request)
    {
        var systemPrompt = """
            You are the Curator Agent for Bethuya, a community event platform.
            Your role is to ASSIST humans in building a fair, diverse, inclusive, and theme-aligned attendee list.
            
            CRITICAL RULES:
            - You NEVER auto-accept or auto-reject anyone
            - You provide RECOMMENDATIONS only — humans decide
            - All reasoning must be EXPLAINABLE and TRANSPARENT
            - You only use SELF-REPORTED, CONSENTED data
            - You NEVER infer sensitive traits
            - You NEVER use opaque scoring
            
            Provide:
            1. Theme alignment signals (based on self-reported interests)
            2. Community continuity signals
            3. DEI nudges (based on consented fields only)
            4. Equity prompts
            5. First-come signals (when applicable)
            6. Over-representation alerts
            
            Output a ranked recommendation with clear reasoning for each attendee.
            IMPORTANT: Do NOT include attendee names or email addresses in your response.
            Use only registrant indices (Registrant #1, Registrant #2, etc.).
            """;

        var registrationSummary = string.Join("\n", request.Registrations.Select((r, i) =>
            $"  Registrant #{i + 1} — Interests: [{string.Join(", ", r.Interests)}] — Registered: {r.RegisteredAt:g}"));

        var userPrompt = $"""
            Event: {request.Event.Title} (Capacity: {request.Event.Capacity})
            Total Registrations: {request.Registrations.Count}
            Oversubscription Ratio: {(double)request.Registrations.Count / request.Event.Capacity:F1}x
            
            Diversity Targets:
            {string.Join("\n", request.Budget.DiversityTargets.Select(kv => $"  {kv.Key}: {kv.Value:P0}"))}
            
            Equity Prompts:
            {string.Join("\n", request.Budget.EquityPrompts.Select(p => $"  - {p}"))}
            
            Registrations (anonymized):
            {registrationSummary}
            """;

        return
        [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ];
    }

    protected override CuratorResponse ParseResponse(ChatResponse response, CuratorRequest request)
    {
        var content = response.Text ?? "";
        var capacity = request.Event.Capacity;
        var registrations = request.Registrations;

        // Use FairnessBudget to rank registrants
        var ranked = request.Budget.RankForSelection(registrations.ToList());

        var proposedIds = ranked
            .Take(capacity)
            .Select(r => r.Item1.Id)
            .ToList();

        var waitlistedIds = ranked
            .Skip(capacity)
            .Select(r => r.Item1.Id)
            .ToList();

        var insights = new CurationInsights
        {
            ThemeAlignmentScores = ranked.ToDictionary(
                r => r.Item1.Id,
                r => (double)(r.Item2 / 100m)), // Normalize to 0-1
            DEINudges = request.Budget.EquityPrompts,
            OverRepresentationAlerts = [],
            CommunitySignals = ["Fairness-based selection applied"],
            FirstComeSignals = ["Tiebreakers resolved randomly"]
        };

        var proposal = new AttendanceProposal
        {
            EventId = request.Event.Id,
            ProposedAttendeeIds = proposedIds,
            Insights = insights,
            Budget = request.Budget,
            Status = ProposalStatus.PendingReview
        };

        var waitlist = new WaitlistProposal
        {
            EventId = request.Event.Id,
            WaitlistedRegistrationIds = waitlistedIds,
            Reason = "Capacity exceeded — waitlisted based on fairness-driven curation recommendations",
            Status = ProposalStatus.PendingReview
        };

        return new CuratorResponse(
            Proposal: proposal,
            Waitlist: waitlist,
            Insights: insights,
            RequiresHumanApproval: true,
            AgentReasoning: content);
    }
}
