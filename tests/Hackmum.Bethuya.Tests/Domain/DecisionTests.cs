using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Tests.Domain;

public class DecisionTests
{
    [Test]
    public async Task Decision_DefaultStatus_IsPending()
    {
        var decision = new Decision { EntityType = "Agenda", DecidedBy = "admin@hackmum.org" };
        await Assert.That(decision.Status).IsEqualTo(DecisionStatus.Pending);
    }

    [Test]
    public async Task Decision_CanStoreDiff()
    {
        var diff = """
            - Session: "Intro to AI" (30 min)
            + Session: "Intro to AI" (45 min)
            """;

        var decision = new Decision
        {
            EntityType = "AgendaSession",
            DecidedBy = "admin@hackmum.org",
            Diff = diff
        };

        await Assert.That(decision.Diff).IsNotNull();
        await Assert.That(decision.Diff).Contains("45 min");
    }
}
