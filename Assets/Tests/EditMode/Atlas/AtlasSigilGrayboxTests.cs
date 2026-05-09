using System;
using System.Linq;
using NUnit.Framework;
using SM.Atlas.Model;
using SM.Atlas.Services;

namespace SM.Tests.EditMode.Atlas;

[Category("BatchOnly")]
public sealed class AtlasSigilGrayboxTests
{
    [Test]
    public void GrayboxRegion_HasExpectedNineteenHexComposition()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();

        Assert.That(region.Nodes.Count, Is.EqualTo(19));
        Assert.That(region.Nodes.Count(node => node.Kind is AtlasNodeKind.Skirmish or AtlasNodeKind.Elite or AtlasNodeKind.Boss or AtlasNodeKind.Extract), Is.EqualTo(5));
        Assert.That(region.Nodes.Count(node => node.Kind is AtlasNodeKind.Reward or AtlasNodeKind.Event), Is.EqualTo(2));
        Assert.That(region.Nodes.Count(node => node.Kind == AtlasNodeKind.SigilAnchor), Is.EqualTo(3));
        Assert.That(region.Nodes.Count(node => node.Kind == AtlasNodeKind.Normal), Is.EqualTo(9));
        Assert.That(region.SigilPool.Count, Is.EqualTo(6));
        Assert.That(region.Roster.Count, Is.EqualTo(6));
    }

    [Test]
    public void SigilPropagation_QuantizesFalloffAndCapsSameCategory()
    {
        Assert.That(SigilPropagationService.ResolveFalloffPercent(0), Is.EqualTo(100));
        Assert.That(SigilPropagationService.ResolveFalloffPercent(1), Is.EqualTo(70));
        Assert.That(SigilPropagationService.ResolveFalloffPercent(2), Is.EqualTo(40));
        Assert.That(SigilPropagationService.ResolveFalloffPercent(3), Is.EqualTo(0));

        var region = CreateSingleNodeRegion(
            new AtlasSigilDefinition(
                "sigil_alpha",
                "Alpha",
                2,
                "reward",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "Alpha reward", 70) }),
            new AtlasSigilDefinition(
                "sigil_beta",
                "Beta",
                2,
                "reward",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "Beta reward", 60) }));

        var resolution = SigilPropagationService.Resolve(region, new[]
        {
            new AtlasPlacedSigil("sigil_beta", new AtlasHexCoordinate(0, 0)),
            new AtlasPlacedSigil("sigil_alpha", new AtlasHexCoordinate(0, 0)),
        });

        var stack = resolution.FindNode("node");
        Assert.That(stack, Is.Not.Null);
        var reward = stack!.ResolvedModifiers.Single(modifier => modifier.Category == AtlasModifierCategory.RewardBias);
        Assert.That(reward.Percent, Is.EqualTo(SigilPropagationService.RewardBiasHardCapPercent));
        Assert.That(reward.SameCategoryCapped, Is.True);
        Assert.That(reward.HardCapped, Is.True);
    }

    [Test]
    public void SigilPropagation_EnforcesThreatHardCapAndThreeCategoryStack()
    {
        var region = CreateSingleNodeRegion(
            new AtlasSigilDefinition(
                "sigil_reward",
                "Reward",
                2,
                "reward",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "Reward", 25) }),
            new AtlasSigilDefinition(
                "sigil_threat",
                "Threat",
                2,
                "threat",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.ThreatPressure, "Threat", 70) }),
            new AtlasSigilDefinition(
                "sigil_affinity",
                "Affinity",
                2,
                "affinity",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.AffinityBoost, "Affinity", 18) }));

        var resolution = SigilPropagationService.Resolve(region, new[]
        {
            new AtlasPlacedSigil("sigil_reward", new AtlasHexCoordinate(0, 0)),
            new AtlasPlacedSigil("sigil_threat", new AtlasHexCoordinate(0, 0)),
            new AtlasPlacedSigil("sigil_affinity", new AtlasHexCoordinate(0, 0)),
        });

        var stack = resolution.FindNode("node")!;
        Assert.That(stack.ResolvedModifiers.Count, Is.LessThanOrEqualTo(3));
        Assert.That(stack.ThreatPressurePercent, Is.EqualTo(SigilPropagationService.ThreatPressureHardCapPercent));
    }

    [Test]
    public void AtlasContextHasher_SortsSigilsLexicallyAndKeepsBattleInputOrderStable()
    {
        var node = new AtlasRegionNode(
            "node",
            new AtlasHexCoordinate(0, 0),
            AtlasNodeKind.Skirmish,
            "Node",
            "enemy",
            "reward",
            "answer",
            2);
        var a = Influence("sigil_a", AtlasModifierCategory.RewardBias, 10);
        var b = Influence("sigil_b", AtlasModifierCategory.ThreatPressure, 10);

        var hash1 = AtlasContextHasher.BuildNodeOverlayHash("region", node, "route", "salt", new[] { b, a });
        var hash2 = AtlasContextHasher.BuildNodeOverlayHash("region", node, "route", "salt", new[] { a, b });
        Assert.That(hash1, Is.EqualTo(hash2));
        Assert.That(AtlasContextHasher.SortedSigilIds(new[] { b, a }), Is.EqualTo(new[] { "sigil_a", "sigil_b" }));

        var battle1 = AtlasContextHasher.BuildBattleContextHash("run", "chapter", "site", 2, "encounter", hash1, "squad");
        var battle2 = AtlasContextHasher.BuildBattleContextHash("run", "chapter", "site", 2, "encounter", hash2, "squad");
        var battle3 = AtlasContextHasher.BuildBattleContextHash("run", "chapter", "site", 2, "encounter", hash2, "other_squad");
        Assert.That(battle1, Is.EqualTo(battle2));
        Assert.That(battle1, Is.Not.EqualTo(battle3));
    }

    private static AtlasRegionDefinition CreateSingleNodeRegion(params AtlasSigilDefinition[] sigils)
    {
        return new AtlasRegionDefinition(
            "region",
            "Region",
            new[] { new AtlasRegionNode("node", new AtlasHexCoordinate(0, 0), AtlasNodeKind.Skirmish, "Node", "enemy", "reward", "answer", 0) },
            new[] { new AtlasHexCoordinate(0, 0) },
            sigils,
            Array.Empty<AtlasCharacterPreview>(),
            new[] { new AtlasRouteCandidate("route", "Route", new[] { "node" }) });
    }

    private static AtlasSigilInfluence Influence(string sigilId, AtlasModifierCategory category, int effectivePercent)
    {
        return new AtlasSigilInfluence(
            sigilId,
            sigilId,
            new AtlasHexCoordinate(0, 0),
            0,
            0,
            category,
            category.ToString(),
            effectivePercent,
            effectivePercent);
    }
}
