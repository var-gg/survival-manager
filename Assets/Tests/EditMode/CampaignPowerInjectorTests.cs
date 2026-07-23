using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Editor.Validation;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class CampaignPowerInjectorTests
{
    [Test]
    public void Apply_MultipliesHpAndBothDamagePowersByExpHalfX()
    {
        const double logPower = 0.4d;
        var unit = CombatTestFactory.CreateLoopAUnit(
            "ally",
            hp: 100f,
            physPower: 20f);
        unit = unit with
        {
            BaseStats = new Dictionary<StatKey, float>(unit.BaseStats)
            {
                [StatKey.MagPower] = 30f,
            },
        };
        var source = new BattleLoadoutSnapshot(
            "snapshot",
            "compile",
            "hash",
            new TeamTacticProfile("tactic", "Tactic", TeamPostureType.StandardAdvance),
            new[] { unit },
            new[] { unit.Id },
            Array.Empty<string>());

        var injected = CampaignPowerInjector.Apply(source, logPower);
        var package = injected.Allies.Single().Packages!
            .Single(value => value.SourceId == CampaignPowerInjector.SourceId);
        var stats = new StatBlock(
            new Dictionary<StatKey, float>(unit.BaseStats),
            package.Modifiers);
        var factor = Math.Exp(logPower / 2d);

        Assert.That(stats.Get(StatKey.MaxHealth), Is.EqualTo(100d * factor).Within(0.001d));
        Assert.That(stats.Get(StatKey.PhysPower), Is.EqualTo(20d * factor).Within(0.001d));
        Assert.That(stats.Get(StatKey.MagPower), Is.EqualTo(30d * factor).Within(0.001d));
        Assert.That(source.Allies.Single().Packages ?? Array.Empty<CombatModifierPackage>(), Is.Empty);
    }
}
