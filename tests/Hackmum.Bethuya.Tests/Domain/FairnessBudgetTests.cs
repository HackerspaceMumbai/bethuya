using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Tests.Domain;

public class FairnessBudgetTests
{
    [Test]
    public async Task FairnessBudget_DiversityTargets_InitializedEmpty()
    {
        var budget = new FairnessBudget();
        await Assert.That(budget.DiversityTargets).IsNotNull();
        await Assert.That(budget.DiversityTargets.Count).IsEqualTo(0);
    }

    [Test]
    public async Task FairnessBudget_CanSetTargets()
    {
        var budget = new FairnessBudget
        {
            DiversityTargets = new Dictionary<string, double>
            {
                ["first-time-attendees"] = 0.3,
                ["students"] = 0.2
            },
            EquityPrompts = ["Consider geographic diversity", "Balance experience levels"]
        };

        await Assert.That(budget.DiversityTargets.Count).IsEqualTo(2);
        await Assert.That(budget.EquityPrompts.Count).IsEqualTo(2);
    }

    [Test]
    public async Task FairnessBudget_ActualMetrics_TrackProgress()
    {
        var budget = new FairnessBudget
        {
            DiversityTargets = new Dictionary<string, double> { ["students"] = 0.2 },
            ActualMetrics = new Dictionary<string, double> { ["students"] = 0.15 }
        };

        var target = budget.DiversityTargets["students"];
        var actual = budget.ActualMetrics["students"];

        await Assert.That(actual).IsLessThan(target);
    }
}
