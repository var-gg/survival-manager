using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Stats;
using SM.Meta.Services;
using SM.Unity.UI.Town.Preview;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class AffixMagnitudeRollTests
{
    [Test]
    public void Roll_IsDeterministicWithinRange_AndSeedSensitive()
    {
        var first = AffixMagnitudeRoller.Roll(42, "affix_bracing", 0, 2f, 4f);
        var repeated = AffixMagnitudeRoller.Roll(42, "affix_bracing", 0, 2f, 4f);
        var changed = AffixMagnitudeRoller.Roll(43, "affix_bracing", 0, 2f, 4f);

        Assert.That(first, Is.EqualTo(repeated));
        Assert.That(first, Is.InRange(2f, 4f));
        Assert.That(changed, Is.InRange(2f, 4f));
        Assert.That(changed, Is.Not.EqualTo(first));
    }

    [Test]
    public void UniformRoll_MeasuredMeanStaysAtCenteredLegacyMagnitude()
    {
        const int samples = 100_000;
        var mean = Enumerable.Range(0, samples)
            .Average(seed => AffixMagnitudeRoller.Roll(seed, "affix_test", 0, 0f, 2f));

        Assert.That(AffixMagnitudeRoller.ExpectedMagnitude(0f, 2f), Is.EqualTo(1d));
        Assert.That(mean, Is.EqualTo(1d).Within(0.01d));
    }

    [Test]
    public void PackageResolver_MaterializesInstanceValueWithoutMutatingSharedPackage()
    {
        var shared = new CombatModifierPackage(
            "affix_test",
            ModifierSource.Item,
            new[]
            {
                new StatModifier(StatKey.PhysPower, ModifierOp.Flat, 2f, ModifierSource.Item, "affix_test"),
                new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 4f, ModifierSource.Item, "affix_test"),
            });

        var resolved = AffixMagnitudePackageResolver.Resolve(shared, 3f);

        Assert.That(resolved, Is.Not.SameAs(shared));
        Assert.That(resolved.Modifiers.Select(value => value.Value), Is.EqualTo(new[] { 3f, 6f }));
        Assert.That(shared.Modifiers.Select(value => value.Value), Is.EqualTo(new[] { 2f, 4f }));
        Assert.That(AffixMagnitudePackageResolver.Resolve(shared, null), Is.SameAs(shared));
    }

    [Test]
    public void PackageResolver_RollsEveryModifier_AndKeepsDrawbacksNegativeAtRangeEdges()
    {
        var shared = new CombatModifierPackage(
            "affix_tradeoff",
            ModifierSource.Item,
            new[]
            {
                new StatModifier(StatKey.PhysPower, ModifierOp.Flat, 2f, ModifierSource.Item, "affix_tradeoff"),
                new StatModifier(StatKey.Armor, ModifierOp.Flat, -1f, ModifierSource.Item, "affix_tradeoff"),
                new StatModifier(StatKey.MaxHealth, ModifierOp.Increased, -0.04f, ModifierSource.Item, "affix_tradeoff"),
            });

        var low = AffixMagnitudePackageResolver.Resolve(shared, 1.5f);
        var high = AffixMagnitudePackageResolver.Resolve(shared, 2.5f);

        Assert.That(low.Modifiers.Select(value => value.Value), Is.EqualTo(new[] { 1.5f, -0.75f, -0.03f }).Within(0.000001f));
        Assert.That(high.Modifiers.Select(value => value.Value), Is.EqualTo(new[] { 2.5f, -1.25f, -0.05f }).Within(0.000001f));
        Assert.That(low.Modifiers.Skip(1).All(value => value.Value < 0f), Is.True);
        Assert.That(high.Modifiers.Skip(1).All(value => value.Value < 0f), Is.True);
        Assert.That(shared.Modifiers.Select(value => value.Value), Is.EqualTo(new[] { 2f, -1f, -0.04f }));
    }

    [Test]
    public void Presentation_ShowsActualMagnitudePercentileAndRange()
    {
        Assert.That(
            AffixMagnitudePresentation.Format(2.5f, 2f, 4f),
            Is.EqualTo("2.5 · 25% [2 ~ 4]"));
    }
}
