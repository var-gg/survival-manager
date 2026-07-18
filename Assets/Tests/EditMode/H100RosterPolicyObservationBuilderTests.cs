using System;
using System.Linq;
using NUnit.Framework;
using SM.Core.Stats;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using SM.Meta.Services;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class H100RosterPolicyObservationBuilderTests
{
    [Test]
    public void Build_FreshReserveUsesEffectiveFullHealth_WhileDeployedHealthIsUnchanged()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100RosterPolicyObservationBuilderTests));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        Assert.That(lookup.TryGetCombatSnapshot(out _, out var error), Is.True, error);

        var session = H100SessionDriver.CreateSession(lookup, "h100-roster-health-regression");
        Assert.That(session.Profile.Heroes.Count, Is.GreaterThanOrEqualTo(2));
        var deployedRecord = session.Profile.Heroes[0];
        var reserveRecord = session.Profile.Heroes[1];
        deployedRecord.CurrentHp = 41;
        deployedRecord.MaxHp = 73;
        reserveRecord.CurrentHp = 0;
        reserveRecord.MaxHp = 0;

        foreach (var anchor in session.DeploymentAnchors)
        {
            Assert.That(session.AssignHeroToAnchor(anchor, null), Is.True);
        }

        Assert.That(
            session.AssignHeroToAnchor(session.DeploymentAnchors[0], deployedRecord.HeroId),
            Is.True);

        var reservePreview = session.TryBuildHeroStatPreview(reserveRecord.HeroId);
        Assert.That(reservePreview, Is.Not.Null);
        var effectiveMaxHealth = HeroEffectiveStatPreview.Resolve(
                reservePreview,
                new[] { StatKey.MaxHealth })
            .Single()
            .EffectiveValue;
        var expectedReserveHp = (int)Math.Max(1, Math.Round(effectiveMaxHealth));

        var observation = H100RosterPolicyObservationBuilder.Build(session, lookup, decisionSeed: 1701);
        var deployed = observation.Roster.Single(hero => hero.HeroId == deployedRecord.HeroId);
        var reserve = observation.Roster.Single(hero => hero.HeroId == reserveRecord.HeroId);

        Assert.That(expectedReserveHp, Is.GreaterThan(0));
        Assert.That(reserve.IsDeployed, Is.False);
        Assert.That(reserve.MaxHp, Is.EqualTo(expectedReserveHp));
        Assert.That(reserve.CurrentHp, Is.EqualTo(expectedReserveHp));
        Assert.That(deployed.IsDeployed, Is.True);
        Assert.That(deployed.CurrentHp, Is.EqualTo(41));
        Assert.That(deployed.MaxHp, Is.EqualTo(73));
    }
}
