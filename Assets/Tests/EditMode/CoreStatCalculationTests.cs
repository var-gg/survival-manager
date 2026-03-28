using System.Collections.Generic;
using NUnit.Framework;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

public class CoreStatCalculationTests
{
    [Test]
    public void StatCalculation_Follows_BaseFlatAdditiveMultiplicativeClamp_Order()
    {
        var stats = new StatBlock(
            new Dictionary<StatKey, float>
            {
                [StatKey.Attack] = 10f,
            },
            new[]
            {
                new StatModifier(StatKey.Attack, ModifierOp.Flat, 5f, ModifierSource.Item, "item.sword"),
                new StatModifier(StatKey.Attack, ModifierOp.AdditivePercent, 0.2f, ModifierSource.Trait, "trait.brave"),
                new StatModifier(StatKey.Attack, ModifierOp.MultiplicativePercent, 0.5f, ModifierSource.Augment, "augment.fury"),
                new StatModifier(StatKey.Attack, ModifierOp.ClampMax, 25f, ModifierSource.Other, "cap"),
            });

        var actual = stats.Get(StatKey.Attack);
        Assert.That(actual, Is.EqualTo(25f).Within(0.001f));
    }
}
