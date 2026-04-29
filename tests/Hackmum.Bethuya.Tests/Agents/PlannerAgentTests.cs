using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.Agents.Implementations;
using Hackmum.Bethuya.AI.Routing;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Tests.Agents.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;

namespace Hackmum.Bethuya.Tests.Agents;

/// <summary>
/// Phase 2: Planner Agent acceptance criteria tests.
/// Tests Planner Agent for drafting event agendas from historical data and Scout context.
/// </summary>
public sealed class PlannerAgentTests
{
    // [Before(HookType.Test)]
    // public async Task Setup() { }


    [Test]
    [Skip("Phase 2: IAIRouter integration pending — Tank will implement")]
    public async Task DraftAgenda_WithValidEvent_ReturnsAgendaDraft()
    {
        // Arrange
        var @event = EventDataBuilder.CreateEvent()
            .WithTitle("Community Meetup 2024")
            .WithTheme("Open Source")
            .WithCapacity(150)
            .Build();

        var request = new PlannerRequest(
            Event: @event,
            Constraints: "2-hour format",
            PriorEventsContext: "3 similar events held; avg 120 attendees, 4 sessions",
            RequestedBy: "organizer@hackerspacemumbai.com");

        // Act
        // var response = await _planner.DraftAsync(request);

        // Assert
        // await Assert.That(response).IsNotNull();
        // await Assert.That(response.DraftAgenda).IsNotNull();
        // await Assert.That(response.DraftAgenda.EventId).IsEqualTo(@event.Id);
        // await Assert.That(response.DraftAgenda.Status).IsEqualTo(AgendaStatus.ProposedByAgent);
        // await Assert.That(response.DraftAgenda.CreatedByAgent).IsEqualTo("Planner");
        // await Assert.That(response.RequiresHumanApproval).IsTrue();
    }

    [Test]
    [Skip("Phase 2: IAIRouter integration pending — Tank will implement")]
    public async Task DraftAgenda_IncludesMultipleSessions_WithValidTimings()
    {
        // Arrange
        var @event = EventDataBuilder.CreateEvent()
            .WithTitle("Tech Workshop")
            .WithCapacity(100)
            .Build();

        var request = new PlannerRequest(
            Event: @event,
            PriorEventsContext: "Past workshops had 5-6 sessions");

        // Act
        // var response = await _planner.DraftAsync(request);

        // Assert
        // await Assert.That(response.DraftAgenda.Sessions).IsNotEmpty();
        // foreach (var session in response.DraftAgenda.Sessions)
        // {
        //     await Assert.That(session.Title).IsNotNullOrEmpty();
        //     await Assert.That(session.StartTime).IsNotEqualTo(TimeOnly.MinValue);
        //     await Assert.That(session.EndTime).IsNotEqualTo(TimeOnly.MinValue);
        //     await Assert.That(session.EndTime).IsGreaterThan(session.StartTime);
        // }
    }

    [Test]
    [Skip("Phase 2: IAIRouter integration pending — Tank will implement")]
    public async Task DraftAgenda_WithConstraints_RespectsFormat()
    {
        // Arrange
        var @event = EventDataBuilder.CreateEvent()
            .WithTitle("Meetup")
            .Build();

        var request = new PlannerRequest(
            Event: @event,
            Constraints: "90-minute format, 3 sessions max, 15-min breaks",
            PriorEventsContext: null,
            RequestedBy: "org@example.com");

        // Act
        // var response = await _planner.DraftAsync(request);

        // Assert
        // await Assert.That(response.DraftAgenda).IsNotNull();
        // await Assert.That(response.DraftAgenda.Sessions.Count).IsLessThanOrEqualTo(3);
    }

    [Test]
    [Skip("Phase 2: IAIRouter integration pending — Tank will implement")]
    public async Task DraftAgenda_WithPriorContext_UsesHistoricalData()
    {
        // Arrange
        var @event = EventDataBuilder.CreateEvent()
            .WithTitle("Community Event")
            .Build();

        var historicalContext = """
            Previous events: 5 held, avg 100 attendees, themes: GenAI, Open Source, Community Building
            Most popular session types: Keynotes (30min), Workshops (60min), Panel discussions (45min)
            Best attendance windows: 2-5 PM
            """;

        var request = new PlannerRequest(
            Event: @event,
            PriorEventsContext: historicalContext);

        // Act
        // var response = await _planner.DraftAsync(request);

        // Assert
        // await Assert.That(response.DraftAgenda.Sessions).IsNotEmpty();
        // await Assert.That(response.AgentReasoning).IsNotNullOrEmpty();
    }

    [Test]
    [Skip("Phase 2: IAIRouter integration pending — Tank will implement")]
    public async Task DraftAgenda_ReturnsStructuredResponse_WithReasoning()
    {
        // Arrange
        var @event = EventDataBuilder.CreateEvent()
            .WithTitle("Meetup")
            .Build();

        var request = new PlannerRequest(@event);

        // Act
        // var response = await _planner.DraftAsync(request);

        // Assert
        // await Assert.That(response.AgentReasoning).IsNotNullOrEmpty();
        // await Assert.That(response.RequiresHumanApproval).IsTrue();
    }

    [Test]
    [Skip("Phase 2: IAIRouter integration pending — Tank will implement")]
    public async Task DraftAgenda_WithLargeCapacity_AdjustsSessions()
    {
        // Arrange
        var largeEvent = EventDataBuilder.CreateEvent()
            .WithCapacity(500)
            .WithTitle("Large Conference")
            .Build();

        var request = new PlannerRequest(
            Event: largeEvent,
            PriorEventsContext: "Capacity 500+ requires parallel tracks");

        // Act
        // var response = await _planner.DraftAsync(request);

        // Assert
        // await Assert.That(response.DraftAgenda).IsNotNull();
        // await Assert.That(response.DraftAgenda.Sessions).IsNotEmpty();
    }

    [Test]
    public async Task PlannerRequest_HasCorrectAgentMetadata()
    {
        // Arrange
        var @event = EventDataBuilder.CreateEvent().Build();
        var request = new PlannerRequest(@event);

        // Act & Assert
        await Assert.That(request.AgentName).IsEqualTo("Planner");
        await Assert.That(request.Sensitivity).IsEqualTo(Core.Enums.DataSensitivity.NonSensitive);
    }

    private static IAIRouter CreateMockRouter()
    {
        // Stub implementation — Tank will replace with full Foundry/OpenAI integration
        throw new NotImplementedException("Phase 2: IAIRouter integration pending");
    }
}
