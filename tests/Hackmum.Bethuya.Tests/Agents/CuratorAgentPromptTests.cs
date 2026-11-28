using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.Agents.Implementations;
using Hackmum.Bethuya.AI.Routing;
using Hackmum.Bethuya.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Hackmum.Bethuya.Tests.Agents;

public class CuratorAgentPromptTests
{
    [Test]
    [Arguments(0, 0)]
    [Arguments(0, 3)]
    public async Task DraftAsync_ZeroCapacity_PromptHasFiniteOversubscriptionRatio(int capacity, int registrationCount)
    {
        List<ChatMessage> sentMessages = [];
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                sentMessages.AddRange(callInfo.Arg<IEnumerable<ChatMessage>>());
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"));
            });

        var router = Substitute.For<IAIRouter>();
        router.GetChatClient(DataSensitivity.Sensitive).Returns(chatClient);
        router.GetProviderName(DataSensitivity.Sensitive).Returns("FoundryLocal");

        var evt = new Event { Title = "Test", CreatedBy = "test@test.com", Capacity = capacity };
        var registrations = Enumerable.Range(1, registrationCount)
            .Select(i => new Registration { EventId = evt.Id, FullName = $"Registrant {i}", Email = $"r{i}@example.com" })
            .ToList();
        var agent = new CuratorAgent(router, NullLogger<CuratorAgent>.Instance);

        await agent.DraftAsync(new CuratorRequest(evt, registrations, new FairnessBudget { EventId = evt.Id }));

        var userPrompt = sentMessages.Single(m => m.Role == ChatRole.User).Text;
        await Assert.That(userPrompt).Contains("Oversubscription Ratio:");
        await Assert.That(userPrompt).DoesNotContain("Infinity");
        await Assert.That(userPrompt).DoesNotContain("∞");
        await Assert.That(userPrompt).DoesNotContain("NaN");
    }
}
