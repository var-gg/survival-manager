using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.SealedLlmBridge;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class SealedLlmCodecFastTests
{
    [Test]
    public void PolicyObservation_CanonicalBytesAreStableDeeplySensitiveAndSetOrderIndependent()
    {
        var baseline = CreatePolicyObservation();
        var repeated = CreatePolicyObservation();
        var reordered = CreatePolicyObservation(reverseOrderInsensitiveCollections: true);
        var deeplyChanged = CreatePolicyObservation(deepAffixMagnitude: 0.375f);

        Assert.That(SealedLlmObservationCodec.CanonicalBytes(repeated), Is.EqualTo(
            SealedLlmObservationCodec.CanonicalBytes(baseline)));
        Assert.That(SealedLlmObservationCodec.CanonicalBytes(reordered), Is.EqualTo(
            SealedLlmObservationCodec.CanonicalBytes(baseline)));
        Assert.That(SealedLlmObservationCodec.CanonicalBytes(deeplyChanged), Is.Not.EqualTo(
            SealedLlmObservationCodec.CanonicalBytes(baseline)));
        Assert.That(SealedLlmObservationCodec.Hash(repeated), Is.EqualTo(SealedLlmObservationCodec.Hash(baseline)));
    }

    [Test]
    public void RosterObservation_CanonicalBytesAreStableDeeplySensitiveAndSetOrderIndependent()
    {
        var baseline = CreateRosterObservation();
        var repeated = CreateRosterObservation();
        var reordered = CreateRosterObservation(reverseOrderInsensitiveCollections: true);
        var deeplyChanged = CreateRosterObservation(deepNodeMagnitude: 2.5f);

        Assert.That(SealedLlmObservationCodec.CanonicalBytes(repeated), Is.EqualTo(
            SealedLlmObservationCodec.CanonicalBytes(baseline)));
        Assert.That(SealedLlmObservationCodec.CanonicalBytes(reordered), Is.EqualTo(
            SealedLlmObservationCodec.CanonicalBytes(baseline)));
        Assert.That(SealedLlmObservationCodec.CanonicalBytes(deeplyChanged), Is.Not.EqualTo(
            SealedLlmObservationCodec.CanonicalBytes(baseline)));
    }

    [Test]
    public void ObservationCanonicalWriter_ReferencesEveryObservationContractProperty()
    {
        var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
            "Assets", "_Game", "Scripts", "Editor", "SealedLlmBridge",
            "SealedLlmObservationCanonicalWriter.cs"));
        var observationTypes = new[]
        {
            typeof(HeadlessPolicyObservation),
            typeof(HeadlessRosterPolicyObservation),
            typeof(HeadlessHeroObservation),
            typeof(HeadlessSkillObservation),
            typeof(HeadlessStatusApplicationObservation),
            typeof(HeadlessStatModifierObservation),
            typeof(HeadlessRuleModifierObservation),
            typeof(HeadlessTriggeredEffectObservation),
            typeof(HeadlessAffixMechanicsObservation),
            typeof(HeadlessItemMechanicsObservation),
            typeof(HeadlessAugmentMechanicsObservation),
            typeof(HeadlessWalletObservation),
            typeof(HeadlessSynergyCountObservation),
            typeof(HeadlessSynergyObservation),
            typeof(HeadlessSynergyTierObservation),
            typeof(HeadlessEnemyPreview),
            typeof(HeadlessEnemyUnitPreview),
            typeof(HeadlessRewardOption),
            typeof(HeadlessRewardMechanicsObservation),
            typeof(HeadlessRecruitOfferObservation),
            typeof(HeadlessPassiveHeroObservation),
            typeof(HeadlessPassiveBoardObservation),
            typeof(HeadlessPassiveNodeObservation),
            typeof(HeadlessRefitItemObservation),
            typeof(HeadlessRefitSlotObservation),
            typeof(HeadlessOwnedItemObservation),
        };

        foreach (var type in observationTypes)
        {
            foreach (var property in type.GetProperties().Where(property =>
                         property.DeclaringType == type && property.GetMethod?.IsStatic == false))
            {
                Assert.That(
                    source,
                    Does.Contain($"value.{property.Name}"),
                    $"Canonical observation writer omitted {type.Name}.{property.Name}.");
            }
        }
    }

    [Test]
    public void LegalActionSetHash_IsDeterministicOrderIndependentAndChangesWithOfferedOptions()
    {
        var baseline = CreatePolicyObservation();
        var repeated = CreatePolicyObservation(reverseOrderInsensitiveCollections: true);
        var changed = CreatePolicyObservation(secondRewardIndex: 7);

        var firstHash = SealedLlmLegalActionSet.RewardLegalActionSetHash(baseline);
        Assert.That(SealedLlmLegalActionSet.RewardLegalActionSetHash(repeated), Is.EqualTo(firstHash));
        Assert.That(SealedLlmLegalActionSet.RewardLegalActionSetHash(changed), Is.Not.EqualTo(firstHash));
        Assert.That(
            SealedLlmLegalActionSet.DeploymentLegalActionSetHash(repeated),
            Is.EqualTo(SealedLlmLegalActionSet.DeploymentLegalActionSetHash(baseline)));
    }

    [Test]
    public void SixSeams_EncodeDecodeRoundTripChoiceAndPassProductionGuards()
    {
        var policyObservation = CreatePolicyObservation();
        var rosterObservation = CreateRosterObservation();

        var deployment = new HeadlessDeploymentDecision(
            new[]
            {
                new HeadlessPlacement(DeploymentAnchorId.FrontTop, "hero-b"),
                new HeadlessPlacement(DeploymentAnchorId.BackTop, "hero-a"),
            },
            "reference",
            1d,
            new[] { "fact" });
        var decodedDeployment = SealedLlmActionCodec.DecodeDeployment(
            policyObservation,
            SealedLlmActionCodec.EncodeDeployment(deployment));
        Assert.That(PlacementSignature(decodedDeployment), Is.EqualTo(PlacementSignature(deployment)));
        Assert.DoesNotThrow(() => HeadlessPolicyGuard.ValidateDeploymentDecision(policyObservation, decodedDeployment));

        var prep = new HeadlessPrepDecision(
            new[]
            {
                new HeadlessPlacement(DeploymentAnchorId.BackTop, "hero-b"),
                new HeadlessPlacement(DeploymentAnchorId.FrontTop, "hero-a"),
            },
            new[] { new HeadlessEquipmentAssignment("instance-hero-a", "hero-b") },
            "reference",
            1d,
            new[] { "fact" });
        var decodedPrep = SealedLlmActionCodec.DecodePrep(
            policyObservation,
            SealedLlmActionCodec.EncodePrep(prep));
        Assert.That(
            string.Join(";", decodedPrep.Placements.OrderBy(value => value.Anchor).Select(value => $"{value.Anchor}={value.HeroId}")),
            Is.EqualTo(string.Join(";", prep.Placements.OrderBy(value => value.Anchor).Select(value => $"{value.Anchor}={value.HeroId}"))));
        Assert.That(decodedPrep.EquipmentAssignments.Single().ItemInstanceId, Is.EqualTo("instance-hero-a"));
        Assert.DoesNotThrow(() => HeadlessPrepPolicyGuard.ValidateDecision(policyObservation, decodedPrep));

        var reward = new HeadlessRewardDecision(1, "reference", 1d, new[] { "fact" });
        var decodedReward = SealedLlmActionCodec.DecodeReward(
            policyObservation,
            SealedLlmActionCodec.EncodeReward(reward));
        Assert.That(decodedReward.OptionIndex, Is.EqualTo(reward.OptionIndex));
        Assert.DoesNotThrow(() => HeadlessPolicyGuard.ValidateRewardDecision(policyObservation, decodedReward));

        var recruit = new HeadlessRecruitDecision(0, "reference", 1d, new[] { "fact" });
        var decodedRecruit = SealedLlmActionCodec.DecodeRecruit(
            rosterObservation,
            SealedLlmActionCodec.EncodeRecruit(recruit));
        Assert.That(decodedRecruit.OfferIndex, Is.EqualTo(recruit.OfferIndex));
        Assert.DoesNotThrow(() => HeadlessRosterPolicyGuard.ValidateRecruitDecision(rosterObservation, decodedRecruit));

        var passive = new HeadlessPassiveDecision(
            "hero-a", "board-main", "node-root", "reference", 1d, new[] { "fact" });
        var decodedPassive = SealedLlmActionCodec.DecodePassive(
            rosterObservation,
            SealedLlmActionCodec.EncodePassive(passive));
        Assert.That(
            new[] { decodedPassive.HeroId, decodedPassive.BoardId, decodedPassive.NodeId },
            Is.EqualTo(new[] { passive.HeroId, passive.BoardId, passive.NodeId }));
        Assert.DoesNotThrow(() => HeadlessRosterPolicyGuard.ValidatePassiveDecision(rosterObservation, decodedPassive));

        var refit = new HeadlessRefitDecision("item-instance-a", 0, "reference", 1d, new[] { "fact" });
        var decodedRefit = SealedLlmActionCodec.DecodeRefit(
            rosterObservation,
            SealedLlmActionCodec.EncodeRefit(refit));
        Assert.That(decodedRefit.ItemInstanceId, Is.EqualTo(refit.ItemInstanceId));
        Assert.That(decodedRefit.AffixSlotIndex, Is.EqualTo(refit.AffixSlotIndex));
        Assert.DoesNotThrow(() => HeadlessRosterPolicyGuard.ValidateRefitDecision(rosterObservation, decodedRefit));
    }

    [Test]
    public void SixSeams_RejectMalformedAndOffMenuActionsWithTypedException()
    {
        var policyObservation = CreatePolicyObservation();
        var rosterObservation = CreateRosterObservation();

        AssertTypedFailure(() => SealedLlmActionCodec.DecodeDeployment(
            policyObservation,
            "BackTop=hero-a;;FrontTop=hero-b"));
        AssertTypedFailure(() => SealedLlmActionCodec.DecodeDeployment(
            policyObservation,
            "BackTop=missing;FrontTop=hero-b"));
        AssertTypedFailure(() => SealedLlmActionCodec.DecodePrep(
            policyObservation,
            "formation#BackTop=hero-a;FrontTop=hero-b|equipment#missing=hero-a"));
        AssertTypedFailure(() => SealedLlmActionCodec.DecodePrep(
            policyObservation,
            "formation#BackTop=hero-b;FrontTop=hero-a|equipment#skip|extra"));
        AssertTypedFailure(() => SealedLlmActionCodec.DecodeReward(policyObservation, "01"));
        AssertTypedFailure(() => SealedLlmActionCodec.DecodeReward(policyObservation, "99"));
        AssertTypedFailure(() => SealedLlmActionCodec.DecodeRecruit(rosterObservation, " 0"));
        AssertTypedFailure(() => SealedLlmActionCodec.DecodeRecruit(rosterObservation, "99"));
        AssertTypedFailure(() => SealedLlmActionCodec.DecodePassive(rosterObservation, "hero-a:board-main"));
        AssertTypedFailure(() => SealedLlmActionCodec.DecodePassive(rosterObservation, "hero-a:board-main:missing"));
        AssertTypedFailure(() => SealedLlmActionCodec.DecodeRefit(rosterObservation, "item-instance-a:01"));
        AssertTypedFailure(() => SealedLlmActionCodec.DecodeRefit(rosterObservation, "item-instance-a:9"));
    }

    [Test]
    public void LegalMenus_ExcludeUnaffordableBlockedAndUnavailableRosterChoices()
    {
        var observation = CreateRosterObservation(gold: 0, echo: 0, canRefit: false);

        AssertTypedFailure(() => SealedLlmActionCodec.DecodeRecruit(observation, "0"));
        AssertTypedFailure(() => SealedLlmActionCodec.DecodePassive(
            observation,
            "hero-a:board-main:node-target"));
        AssertTypedFailure(() => SealedLlmActionCodec.DecodeRefit(observation, "item-instance-a:0"));
        Assert.DoesNotThrow(() => SealedLlmActionCodec.DecodeRecruit(observation, "skip"));
        Assert.DoesNotThrow(() => SealedLlmActionCodec.DecodePassive(observation, "skip"));
        Assert.DoesNotThrow(() => SealedLlmActionCodec.DecodeRefit(observation, "skip"));
    }

    [Test]
    public void RequestCanonicalBytes_AreDeterministicFunctionsOfManifestObservationAndHistory()
    {
        var manifest = Manifest("template-a");
        var observationBytes = SealedLlmObservationCodec.CanonicalBytes(CreatePolicyObservation());
        var baseline = SealedLlmRequestCodec.RequestCanonicalBytes(manifest, observationBytes, "history-a");

        Assert.That(
            SealedLlmRequestCodec.RequestCanonicalBytes(manifest, observationBytes, "history-a"),
            Is.EqualTo(baseline));
        Assert.That(
            SealedLlmRequestCodec.RequestCanonicalBytes(Manifest("template-b"), observationBytes, "history-a"),
            Is.Not.EqualTo(baseline));
        Assert.That(
            SealedLlmRequestCodec.RequestCanonicalBytes(
                manifest,
                SealedLlmObservationCodec.CanonicalBytes(CreatePolicyObservation(deepAffixMagnitude: 0.375f)),
                "history-a"),
            Is.Not.EqualTo(baseline));
        Assert.That(
            SealedLlmRequestCodec.RequestCanonicalBytes(manifest, observationBytes, "history-b"),
            Is.Not.EqualTo(baseline));
        Assert.That(
            SealedLlmRequestCodec.Hash(manifest, observationBytes, "history-a"),
            Is.EqualTo(SealedLlmRequestCodec.Hash(manifest, observationBytes, "history-a")));
    }

    private static HeadlessPolicyObservation CreatePolicyObservation(
        float deepAffixMagnitude = 0.25f,
        bool reverseOrderInsensitiveCollections = false,
        int secondRewardIndex = 1)
    {
        var tags = reverseOrderInsensitiveCollections
            ? new[] { "physical", "weapon" }
            : new[] { "weapon", "physical" };
        var heroes = new[]
        {
            Hero("hero-a", DeploymentAnchorId.BackTop, tags, deepAffixMagnitude),
            Hero("hero-b", DeploymentAnchorId.FrontTop, tags, deepAffixMagnitude),
        };
        var rewards = new[]
        {
            new HeadlessRewardOption(0, HeadlessRewardKind.Gold, "gold", 10, 0, 0),
            new HeadlessRewardOption(
                secondRewardIndex,
                HeadlessRewardKind.Item,
                "item-a",
                0,
                0,
                0,
                new HeadlessRewardMechanicsObservation(
                    Item("item-a", "reward-instance", tags, deepAffixMagnitude),
                    null)),
        };
        if (reverseOrderInsensitiveCollections)
        {
            Array.Reverse(heroes);
            Array.Reverse(rewards);
        }

        var observation = new HeadlessPolicyObservation(
            1701,
            2,
            "chapter-a",
            "site-a",
            heroes,
            reverseOrderInsensitiveCollections
                ? new[] { DeploymentAnchorId.FrontTop, DeploymentAnchorId.BackTop }
                : new[] { DeploymentAnchorId.BackTop, DeploymentAnchorId.FrontTop },
            new HeadlessEnemyPreview(
                true,
                "encounter-a",
                "faction-a",
                "normal",
                2,
                new[]
                {
                    new HeadlessEnemyUnitPreview(
                        "enemy-a", "undead", "ranger", "carry", DeploymentAnchorId.BackTop),
                },
                "aura-a",
                "utility-a",
                reverseOrderInsensitiveCollections ? new[] { "rare", "item" } : new[] { "item", "rare" }),
            rewards,
            new HeadlessWalletObservation(20, 20),
            new[]
            {
                new HeadlessAugmentMechanicsObservation(
                    "augment-a",
                    "utility",
                    "family-a",
                    1,
                    tags,
                    new[] { "frontline" },
                    new[] { new HeadlessStatModifierObservation("MaxHp", "Add", 4f, string.Empty) },
                    new[] { new HeadlessRuleModifierObservation("Target", "Nearest", 1f) },
                    new[]
                    {
                        new HeadlessTriggeredEffectObservation(
                            "BattleStart", "ApplyStatus", "Team", 1f, 0.5f, "guarded", 3f, 1),
                    }),
            },
            new[] { new HeadlessSynergyCountObservation("human", 2) },
            new[]
            {
                new HeadlessSynergyObservation(
                    "synergy-human",
                    "human",
                    new[]
                    {
                        new HeadlessSynergyTierObservation(
                            2,
                            new[] { new HeadlessStatModifierObservation("MaxHp", "Add", 8f, string.Empty) },
                            "team-rule-human"),
                    }),
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["signal-b"] = "fact-b",
                ["signal-a"] = "fact-a",
            },
            new[]
            {
                new HeadlessPlacement(DeploymentAnchorId.BackTop, "hero-a"),
                new HeadlessPlacement(DeploymentAnchorId.FrontTop, "hero-b"),
            },
            new[]
            {
                new HeadlessOwnedItemObservation(
                    Item("item-hero-a", "instance-hero-a", tags, deepAffixMagnitude),
                    "hero-a"),
            });
        HeadlessPolicyGuard.ValidateObservation(observation);
        return observation;
    }

    private static HeadlessRosterPolicyObservation CreateRosterObservation(
        float deepNodeMagnitude = 1.5f,
        bool reverseOrderInsensitiveCollections = false,
        int gold = 20,
        int echo = 20,
        bool canRefit = true)
    {
        var policy = CreatePolicyObservation(reverseOrderInsensitiveCollections: reverseOrderInsensitiveCollections);
        var nodes = new[]
        {
            new HeadlessPassiveNodeObservation(
                "node-root",
                0,
                "Small",
                Array.Empty<string>(),
                new[] { "path-a" },
                "skill-root",
                reverseOrderInsensitiveCollections ? new[] { "defense", "guard" } : new[] { "guard", "defense" },
                new[] { new HeadlessStatModifierObservation("MaxHp", "Add", 5f, string.Empty) },
                new[] { new HeadlessRuleModifierObservation("Barrier", "Self", deepNodeMagnitude) }),
            new HeadlessPassiveNodeObservation(
                "node-target",
                1,
                "Keystone",
                new[] { "node-root" },
                new[] { "path-b" },
                "skill-target",
                new[] { "payoff" },
                Array.Empty<HeadlessStatModifierObservation>(),
                Array.Empty<HeadlessRuleModifierObservation>()),
        };
        if (reverseOrderInsensitiveCollections) Array.Reverse(nodes);
        var board = new HeadlessPassiveBoardObservation("board-main", nodes);
        var passiveHeroes = policy.Roster.Select(hero => new HeadlessPassiveHeroObservation(
            hero.HeroId,
            hero.Level,
            "board-main",
            Array.Empty<string>(),
            3,
            1,
            new[] { board })).ToArray();
        var offers = new[]
        {
            new HeadlessRecruitOfferObservation(
                0, "recruit-a", "human", "mystic", "support", "active-a", "passive-a", 5,
                "Common", "OnPlan", false),
            new HeadlessRecruitOfferObservation(
                1, "recruit-b", "undead", "ranger", "carry", "active-b", "passive-b", 50,
                "Rare", "OffPlan", false),
        };
        if (reverseOrderInsensitiveCollections)
        {
            Array.Reverse(passiveHeroes);
            Array.Reverse(offers);
        }

        var observation = new HeadlessRosterPolicyObservation(
            1701,
            "chapter-a",
            "site-a",
            4,
            policy.Roster,
            new HeadlessWalletObservation(gold, echo),
            offers,
            passiveHeroes,
            new[]
            {
                new HeadlessRefitItemObservation(
                    "item-a",
                    "item-instance-a",
                    "hero-a",
                    new[] { "weapon", "physical" },
                    "weapon-sword",
                    5,
                    new[]
                    {
                        new HeadlessRefitSlotObservation(
                            0,
                            Affix("affix-current", deepNodeMagnitude),
                            canRefit),
                    }),
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["roster-b"] = "fact-roster-b",
                ["roster-a"] = "fact-roster-a",
            });
        HeadlessRosterPolicyGuard.ValidateObservation(observation);
        return observation;
    }

    private static HeadlessHeroObservation Hero(
        string heroId,
        DeploymentAnchorId anchor,
        IReadOnlyList<string> itemTags,
        float deepAffixMagnitude)
        => new(
            heroId,
            $"archetype-{heroId}",
            "human",
            "vanguard",
            "anchor",
            2,
            90,
            100,
            1,
            true,
            anchor,
            new[] { Skill($"skill-{heroId}") },
            $"active-{heroId}",
            $"passive-{heroId}",
            new[] { Item($"item-{heroId}", $"instance-{heroId}", itemTags, deepAffixMagnitude) },
            new[] { "node-owned-b", "node-owned-a" });

    private static HeadlessSkillObservation Skill(string skillId)
        => new(
            skillId,
            SkillKind.Strike,
            "CoreActive",
            12f,
            2.5f,
            DamageType.Physical,
            1f,
            0.75f,
            0.25f,
            0.1f,
            0.2f,
            5f,
            3f,
            0.25f,
            true,
            SkillDelivery.Melee,
            SkillTargetRule.NearestEnemy,
            new[] { new HeadlessStatusApplicationObservation("apply-a", "marked", 2f, 1.25f, 2) });

    private static HeadlessItemMechanicsObservation Item(
        string itemId,
        string instanceId,
        IReadOnlyList<string> tags,
        float deepAffixMagnitude)
        => new(
            itemId,
            instanceId,
            tags,
            "weapon-sword",
            new[] { new HeadlessStatModifierObservation("PhysicalPower", "Add", 2f, "weapon") },
            new[] { Affix("affix-a", deepAffixMagnitude) },
            new[] { Skill($"granted-{itemId}") });

    private static HeadlessAffixMechanicsObservation Affix(string affixId, float magnitude)
        => new(
            affixId,
            new[] { "offense", "physical" },
            new[] { "weapon" },
            new[] { "magic" },
            new[] { new HeadlessStatModifierObservation("AttackSpeed", "Add", magnitude, "weapon") },
            new[] { new HeadlessRuleModifierObservation("Delivery", "Melee", magnitude) });

    private static string PlacementSignature(HeadlessDeploymentDecision decision)
        => string.Join(";", decision.Placements
            .OrderBy(placement => placement.Anchor.ToString(), StringComparer.Ordinal)
            .Select(placement => $"{placement.Anchor}={placement.HeroId}"));

    private static LlmPromptManifestV1 Manifest(string templateId)
        => new(templateId, "prompt", "briefing", "model-snapshot", "temperature=0");

    private static void AssertTypedFailure(TestDelegate action)
        => Assert.Throws<SealedLlmActionDecodeException>(action);
}
