using NUnit.Framework;
using SM.Content.Definitions;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

public class MetaRewardPickTests
{
    [Test]
    public void RewardPick_Updates_Currency_And_Expedition_State()
    {
        var currency = new CurrencyState();
        var expedition = new ExpeditionState();
        var options = new[]
        {
            new RewardOption("reward.gold.10", RewardType.Gold, 10, "ui.reward.kind.gold"),
            new RewardOption("augment.silver.guard", RewardType.TemporaryAugment, 1, "ui.reward.kind.temp_augment"),
            new RewardOption("reward.reroll.2", RewardType.TraitRerollCurrency, 2, "ui.reward.kind.reroll"),
        };

        RewardService.ApplyRewardPick(currency, expedition, options, new RewardPick("augment.silver.guard"));
        RewardService.ApplyRewardPick(currency, expedition, options, new RewardPick("reward.gold.10"));

        Assert.That(expedition.TemporaryAugmentIds, Has.Count.EqualTo(1));
        Assert.That(expedition.TemporaryAugmentIds[0], Is.EqualTo("augment.silver.guard"));
        Assert.That(currency.Gold, Is.EqualTo(10));
    }
}
