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
        Assert.That(region.StageCandidates.Count, Is.EqualTo(8));
        Assert.That(region.StageCandidates.Count(candidate => candidate.SiteStageIndex == 1), Is.EqualTo(2));
        Assert.That(region.StageCandidates.Count(candidate => candidate.SiteStageIndex == 2), Is.EqualTo(2));
        Assert.That(region.StageCandidates.Count(candidate => candidate.SiteStageIndex == 3), Is.EqualTo(2));
        Assert.That(region.StageCandidates.Count(candidate => candidate.SiteStageIndex == 4), Is.EqualTo(1));
        Assert.That(region.StageCandidates.Count(candidate => candidate.SiteStageIndex == 5), Is.EqualTo(1));
        Assert.That(region.Nodes.Count(node => node.Kind == AtlasNodeKind.SigilAnchor), Is.EqualTo(3));
        Assert.That(region.Nodes.Count(node => node.Kind is AtlasNodeKind.Cache or AtlasNodeKind.ScoutVantage or AtlasNodeKind.Echo), Is.EqualTo(8));
        Assert.That(region.SigilAnchorSlots.Count, Is.EqualTo(3));
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
                new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "Alpha reward", 70) },
                AtlasModifierCategory.RewardBias),
            new AtlasSigilDefinition(
                "sigil_beta",
                "Beta",
                2,
                "reward",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "Beta reward", 60) },
                AtlasModifierCategory.RewardBias));

        var resolution = SigilPropagationService.Resolve(region, new[]
        {
            new AtlasPlacedSigil("sigil_beta", new AtlasHexCoordinate(0, 0), "anchor"),
            new AtlasPlacedSigil("sigil_alpha", new AtlasHexCoordinate(0, 0), "anchor"),
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
        var region = CreateLaneTargetRegion(
            new AtlasSigilDefinition(
                "sigil_reward",
                "Reward",
                2,
                "reward",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "Reward", 25) },
                AtlasModifierCategory.RewardBias),
            new AtlasSigilDefinition(
                "sigil_threat",
                "Threat",
                2,
                "threat",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.ThreatPressure, "Threat", 70) },
                AtlasModifierCategory.ThreatPressure),
            new AtlasSigilDefinition(
                "sigil_affinity",
                "Affinity",
                2,
                "affinity",
                new[] { new AtlasSigilModifier(AtlasModifierCategory.AffinityBoost, "Affinity", 18) },
                AtlasModifierCategory.AffinityBoost));

        var resolution = SigilPropagationService.Resolve(region, new[]
        {
            new AtlasPlacedSigil("sigil_reward", new AtlasHexCoordinate(0, 0), "anchor"),
            new AtlasPlacedSigil("sigil_threat", new AtlasHexCoordinate(0, 0), "anchor"),
            new AtlasPlacedSigil("sigil_affinity", new AtlasHexCoordinate(0, 0), "anchor"),
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

        var hash1 = AtlasContextHasher.BuildNodeOverlayHash("region", node, "salt", new[] { b, a });
        var hash2 = AtlasContextHasher.BuildNodeOverlayHash("region", node, "salt", new[] { a, b });
        Assert.That(hash1, Is.EqualTo(hash2));
        Assert.That(AtlasContextHasher.SortedSigilIds(new[] { b, a }), Is.EqualTo(new[] { "sigil_a", "sigil_b" }));

        var pathHash = AtlasContextHasher.BuildStageCandidatePathHash(new[] { "hex_a", "hex_b" });
        var battle1 = AtlasContextHasher.BuildBattleContextHash("run", "chapter", "site", 2, "encounter", pathHash, hash1, "squad");
        var battle2 = AtlasContextHasher.BuildBattleContextHash("run", "chapter", "site", 2, "encounter", pathHash, hash2, "squad");
        var battle3 = AtlasContextHasher.BuildBattleContextHash("run", "chapter", "site", 2, "encounter", pathHash, hash2, "other_squad");
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
            new[] { new AtlasStageCandidate(1, "1A", "node", Array.Empty<string>()) },
            new[] { new SigilAnchorSlot("anchor", "node", "Outer", "stage_1_2", "Approach", AtlasHexDirection.East, new[] { "node" }, new[] { "CampaignFirstClear" }) });
    }

    private static AtlasRegionDefinition CreateLaneTargetRegion(params AtlasSigilDefinition[] sigils)
    {
        return new AtlasRegionDefinition(
            "region",
            "Region",
            new[]
            {
                new AtlasRegionNode("anchor_node", new AtlasHexCoordinate(0, 0), AtlasNodeKind.SigilAnchor, "Anchor", "anchor", "sigil", "answer"),
                new AtlasRegionNode("node", new AtlasHexCoordinate(1, 0), AtlasNodeKind.Skirmish, "Node", "enemy", "reward", "answer", 0),
            },
            new[] { new AtlasHexCoordinate(0, 0) },
            sigils,
            Array.Empty<AtlasCharacterPreview>(),
            new[] { new AtlasStageCandidate(1, "1A", "node", Array.Empty<string>()) },
            new[] { new SigilAnchorSlot("anchor", "anchor_node", "Outer", "stage_1_2", "Approach", AtlasHexDirection.East, new[] { "node" }, new[] { "CampaignFirstClear" }) });
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
