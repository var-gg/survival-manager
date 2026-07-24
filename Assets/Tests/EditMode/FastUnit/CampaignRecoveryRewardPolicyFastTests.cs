using System.Linq;
using NUnit.Framework;
using SM.Core.Content;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

[TestFixture]
[Category("FastUnit")]
public sealed class CampaignRecoveryRewardPolicyFastTests
{
    [Test]
    public void RewardedRevisitCurve_UsesFourThreeTwoOneThenZero()
    {
        Assert.That(
            Enumerable.Range(1, 6)
                .Select(CampaignRecoveryRewardPolicy.GetItemRollCount)
                .ToArray(),
            Is.EqualTo(new[] { 4, 3, 2, 1, 0, 0 }));
        Assert.That(
            Enumerable.Range(1, 5)
                .Select(index => CampaignRecoveryRewardPolicy.GetRevisitEcho(40, index))
                .ToArray(),
            Is.EqualTo(new[] { 40, 26, 17, 11, 0 }));
        Assert.That(
            Enumerable.Range(1, 5)
                .Select(CampaignRecoveryRewardPolicy.GetItemRollCountBefore)
                .ToArray(),
            Is.EqualTo(new[] { 0, 4, 7, 9, 10 }));
        Assert.That(
            Enumerable.Range(1, 5)
                .Select(CampaignRecoveryRewardPolicy.GetMinimumItemGrade)
                .ToArray(),
            Is.EqualTo(new[]
            {
                ItemRarityTierValue.Epic,
                ItemRarityTierValue.Legendary,
                ItemRarityTierValue.Legendary,
                ItemRarityTierValue.Legendary,
                ItemRarityTierValue.Common,
            }));
    }

    [Test]
    public void RevisitGold_UsesThirtyPercentOfMedianRecruitAcrossWholeChapterBudget()
    {
        var medianRecruit = CampaignRecoveryRewardPolicy.GetMedianRecruitGoldCost();
        var perRevisit = Enumerable.Range(1, 5)
            .Select(CampaignRecoveryRewardPolicy.GetRevisitGold)
            .ToArray();

        Assert.That(medianRecruit, Is.EqualTo(7));
        Assert.That(perRevisit, Is.EqualTo(new[] { 1, 1, 0, 0, 0 }));
        Assert.That(perRevisit.Sum(), Is.EqualTo(2));
        Assert.That(
            perRevisit.Sum() / (double)medianRecruit,
            Is.InRange(0.25d, 0.35d));
    }

    [Test]
    public void DefeatConsolation_PaysQuarterThenEighthAndStops()
    {
        var amounts = Enumerable.Range(1, 4)
            .Select(index => CampaignRecoveryRewardPolicy.GetDefeatConsolationEcho(40, index))
            .ToArray();

        Assert.That(amounts, Is.EqualTo(new[] { 10, 5, 0, 0 }));
        Assert.That(amounts.Sum(), Is.EqualTo(15));
    }
}
