using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class PreviewGroundedConceptPolicyFastTests
{
    [Test]
    public void ThreatParser_TagsManyRangedSustainAndFrontWallFromVisibleFields()
    {
        var profile = EnemyThreatProfileParser.Parse(EnemyThreatObservation.FromVisiblePreview(
            Preview("visible-a")));

        Assert.That(profile.Tags, Does.Contain(EnemyThreatTag.BacklineFirepower));
        Assert.That(profile.Tags, Does.Contain(EnemyThreatTag.SustainEngine));
        Assert.That(profile.Tags, Does.Contain(EnemyThreatTag.FrontlineWall));
    }

    [Test]
    public void LexicographicSelection_PreservesIdentityBeforeMaximizingCounterConnections()
    {
        var observation = IdentityDominanceObservation();
        var policy = ConceptCommitPolicy.CreatePreviewGrounded(GuardedIntent());

        var decision = policy.DecideDeployment(observation);

        Assert.That(decision.Placements.Select(value => value.HeroId), Does.Contain("identity-anchor"));
        Assert.That(policy.LastPreviewDecision.IdentityPreservingCandidateAvailable, Is.True);
        Assert.That(policy.LastPreviewDecision.CoreIdentityPreserved, Is.True);
        Assert.That(policy.LastPreviewDecision.Reason, Is.EqualTo(IntentDecisionReason.CounterAdapt));
    }

    [Test]
    public void CounterAdapt_RejectsUnsupportedClaim()
    {
        Assert.Throws<HeadlessPolicyEvidenceException>(() =>
            PreviewGroundedEvidenceGuard.RequireSupported(
                IntentDecisionReason.CounterAdapt,
                Array.Empty<PreviewCounterConnection>()));
    }

    [Test]
    public void SameVisibleObservation_ProducesByteIdenticalDecisionAndTrace()
    {
        var first = Run(CanonicalObservation("site-a", "visible-a"));
        var repeated = Run(CanonicalObservation("site-a", "visible-a"));

        Assert.That(repeated, Is.EqualTo(first));
    }

    [Test]
    public void ThreatDtoHasNoSiteOrEncounterIdentity_AndIdentityChangesDoNotAffectDecision()
    {
        var propertyNames = typeof(EnemyThreatObservation).GetProperties()
            .Concat(typeof(EnemyThreatUnitObservation).GetProperties())
            .Select(value => value.Name)
            .ToArray();
        Assert.That(propertyNames.Any(value =>
            value.Contains("EncounterId", StringComparison.OrdinalIgnoreCase)
            || value.Contains("SiteId", StringComparison.OrdinalIgnoreCase)), Is.False);

        var first = Run(CanonicalObservation("site-a", "visible-a"));
        var renamed = Run(CanonicalObservation("site-completely-different", "visible-completely-different"));
        Assert.That(renamed, Is.EqualTo(first));
    }

    [Test]
    public void FactoryRegistersPreviewPolicyWithoutChangingExistingCohorts()
    {
        Assert.That(HeadlessPolicyFactory.ProductionPolicyIds.Count, Is.EqualTo(6));
        Assert.That(HeadlessPolicyFactory.AllPolicyIds.Count, Is.EqualTo(7));
        Assert.That(HeadlessPolicyFactory.RegisteredPolicyIds, Does.Contain(HeadlessPolicyFactory.PreviewGroundedConceptId));
        Assert.That(
            HeadlessPolicyFactory.Create(HeadlessPolicyFactory.PreviewGroundedConceptId).Id,
            Is.EqualTo(ConceptCommitPolicy.PreviewGroundedPolicyId));

        var legacy = new ConceptCommitPolicy(GuardedIntent());
        legacy.DecideDeployment(CanonicalObservation("site-a", "visible-a"));
        Assert.That(legacy.Id, Is.EqualTo(ConceptCommitPolicy.PolicyId));
        Assert.That(legacy.LastPreviewDecision, Is.Null);
    }

    [Test]
    public void Prep_UsesOneOwnedPenetrationItemToCounterVisibleEnemyArmor()
    {
        var enemyArmor = new HeadlessItemMechanicsObservation(
            "item_enemy_layered_armor",
            string.Empty,
            new[] { "armor" },
            string.Empty,
            new[] { new HeadlessStatModifierObservation("armor", "Flat", 2f, string.Empty) },
            Array.Empty<HeadlessAffixMechanicsObservation>(),
            Array.Empty<HeadlessSkillObservation>());
        var preview = new HeadlessEnemyPreview(
            true,
            "encounter-armored",
            "faction-visible",
            "elite",
            2,
            new[]
            {
                new HeadlessEnemyUnitPreview(
                    "bulwark",
                    "undead",
                    "vanguard",
                    "anchor",
                    DeploymentAnchorId.FrontCenter,
                    new[] { enemyArmor }),
            },
            string.Empty,
            string.Empty,
            Array.Empty<string>());
        var roster = new[]
        {
            Hero("hero-wall", "warden", "vanguard", "anchor", DeploymentAnchorId.FrontCenter, true,
                Skill("wall-guard", SkillKind.Buff, SkillDelivery.Aura, SkillTargetRule.Self, DamageType.Physical, 0f, "guarded")),
            Hero("hero-ranger", "marksman", "ranger", "carry", DeploymentAnchorId.BackTop, true,
                Skill("ranger-shot", SkillKind.Strike, SkillDelivery.Projectile, SkillTargetRule.MostExposedEnemy, DamageType.Physical, 5f)),
        };
        var evidence = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HeadlessPolicyEvidence.DeploymentSurfaceSignal] = "fact-deployment",
            [HeadlessPolicyEvidence.RosterSurfaceSignal] = "fact-roster",
            [HeadlessPolicyEvidence.EnemyPreviewSignal] = "fact-preview",
            [HeadlessPolicyEvidence.EnemyUnitSignal(0)] = "fact-enemy-0",
            [HeadlessPolicyEvidence.HeroSignal("hero-wall")] = "fact-hero-wall",
            [HeadlessPolicyEvidence.HeroSignal("hero-ranger")] = "fact-hero-ranger",
            [HeadlessPolicyEvidence.HeroSkillSignal("hero-wall", "wall-guard")] = "fact-wall-guard",
            [HeadlessPolicyEvidence.HeroSkillSignal("hero-ranger", "ranger-shot")] = "fact-ranger-shot",
        };
        var piercingBow = new HeadlessItemMechanicsObservation(
            "item_counter_bow",
            "owned-piercing-bow",
            new[] { "weapon", "bow" },
            "bow",
            new[] { new HeadlessStatModifierObservation("phys_pen", "Flat", 0.7f, string.Empty) },
            Array.Empty<HeadlessAffixMechanicsObservation>(),
            Array.Empty<HeadlessSkillObservation>());
        var observation = new HeadlessPolicyObservation(
            1701,
            2,
            "chapter-visible",
            "site-visible",
            roster,
            new[] { DeploymentAnchorId.FrontCenter, DeploymentAnchorId.BackTop },
            preview,
            Array.Empty<HeadlessRewardOption>(),
            evidenceFactIdsBySignal: evidence,
            currentPlacements: new[]
            {
                new HeadlessPlacement(DeploymentAnchorId.FrontCenter, "hero-wall"),
                new HeadlessPlacement(DeploymentAnchorId.BackTop, "hero-ranger"),
            },
            ownedItems: new[] { new HeadlessOwnedItemObservation(piercingBow, string.Empty) });

        var policy = ConceptCommitPolicy.CreatePreviewGrounded(GuardedIntent());
        var decision = policy.DecidePrep(observation);

        Assert.That(HeadlessPrepPolicyGuard.MaximumEquipmentAssignments, Is.EqualTo(1));
        Assert.That(decision.EquipmentAssignments.Count, Is.EqualTo(1));
        Assert.That(decision.EquipmentAssignments[0].ItemInstanceId, Is.EqualTo("owned-piercing-bow"));
        Assert.That(decision.EquipmentAssignments[0].HeroId, Is.EqualTo("hero-ranger"));
        Assert.That(decision.Rationale, Does.Contain("counter=phys_pen"));
        Assert.DoesNotThrow(() => HeadlessPrepPolicyGuard.ValidateDecision(observation, decision));
    }

    [Test]
    public void Prep_BacklineDivePreview_MovesProtectionAnchorBesideBackline()
    {
        var roster = new[]
        {
            Hero("hero-warden", "warden", "vanguard", "anchor", DeploymentAnchorId.FrontCenter, true,
                Skill("warden-guard", SkillKind.Shield, SkillDelivery.Aura, SkillTargetRule.Self, DamageType.Physical, 0f, "guarded")),
            Hero("hero-raider", "raider", "duelist", "bruiser", DeploymentAnchorId.FrontTop, true,
                Skill("raider-strike", SkillKind.Strike, SkillDelivery.Melee, SkillTargetRule.MostExposedEnemy, DamageType.Physical, 1.3f)),
            Hero("hero-marksman", "marksman", "ranger", "carry", DeploymentAnchorId.BackTop, true,
                Skill("marksman-shot", SkillKind.Strike, SkillDelivery.Projectile, SkillTargetRule.MostExposedEnemy, DamageType.Physical, 5f)),
            Hero("hero-hunter", "hunter", "ranger", "carry", DeploymentAnchorId.BackBottom, true,
                Skill("hunter-shot", SkillKind.Strike, SkillDelivery.Projectile, SkillTargetRule.MostExposedEnemy, DamageType.Physical, 5f)),
        };
        var preview = new HeadlessEnemyPreview(
            true,
            "site_wolfpine_trail_boss_1",
            "faction_wolfpine_pack",
            "site_boss",
            3,
            new[]
            {
                new HeadlessEnemyUnitPreview("warden", "human", "vanguard", "boss_captain", DeploymentAnchorId.FrontCenter),
                new HeadlessEnemyUnitPreview("reaver", "undead", "duelist", "escort", DeploymentAnchorId.FrontTop),
                new HeadlessEnemyUnitPreview("shaman", "beastkin", "mystic", "escort", DeploymentAnchorId.BackBottom),
            },
            "boss_aura_sustain_guard",
            "boss_utility_backline_dive",
            Array.Empty<string>());
        var observation = Observation(
            "site_wolfpine_trail",
            "site_wolfpine_trail_boss_1",
            4,
            roster,
            preview,
            new[]
            {
                new HeadlessOwnedItemObservation(
                    new HeadlessItemMechanicsObservation(
                        "item-durable-bait",
                        "owned-durable-bait",
                        new[] { "ranger" },
                        string.Empty,
                        new[] { new HeadlessStatModifierObservation("max_health", "Add", 8f, string.Empty) },
                        Array.Empty<HeadlessAffixMechanicsObservation>(),
                        Array.Empty<HeadlessSkillObservation>()),
                    "hero-warden"),
                new HeadlessOwnedItemObservation(
                    new HeadlessItemMechanicsObservation(
                        "item-vanguard-only",
                        "owned-vanguard-only",
                        new[] { "vanguard" },
                        string.Empty,
                        new[] { new HeadlessStatModifierObservation("max_health", "Add", 20f, string.Empty) },
                        Array.Empty<HeadlessAffixMechanicsObservation>(),
                        Array.Empty<HeadlessSkillObservation>()),
                    "hero-warden"),
            });
        var profile = EnemyThreatProfileParser.Parse(EnemyThreatObservation.FromVisiblePreview(preview));
        var policy = ConceptCommitPolicy.CreatePreviewGrounded(GuardedIntent());

        var decision = policy.DecidePrep(observation);

        Assert.That(profile.Tags, Does.Contain(EnemyThreatTag.BacklineDive));
        Assert.That(decision.Rationale, Does.Contain("backline_dive_screen"));
        Assert.That(decision.Placements.Any(guard =>
            guard.Anchor.IsFrontRow()
            && guard.HeroId == "hero-warden"
            && decision.Placements.Any(backliner =>
                backliner.Anchor.IsBackRow()
                && backliner.Anchor.LaneIndex() == guard.Anchor.LaneIndex()
                && backliner.HeroId is "hero-hunter" or "hero-marksman")), Is.True,
            $"placements={string.Join(",", decision.Placements.Select(value => $"{value.Anchor}:{value.HeroId}"))}; rationale={decision.Rationale}");
        Assert.That(observation.CurrentPlacements.Any(guard =>
            guard.Anchor.IsFrontRow()
            && guard.HeroId == "hero-warden"
            && observation.CurrentPlacements.Any(backliner =>
                backliner.Anchor.IsBackRow()
                && backliner.Anchor.LaneIndex() == guard.Anchor.LaneIndex()
                && backliner.HeroId is "hero-hunter" or "hero-marksman")), Is.False);
        Assert.That(decision.EquipmentAssignments.Count, Is.EqualTo(1));
        Assert.That(decision.EquipmentAssignments[0].ItemInstanceId, Is.EqualTo("owned-durable-bait"));
        Assert.That(decision.EquipmentAssignments[0].HeroId, Is.EqualTo("hero-hunter").Or.EqualTo("hero-marksman"));
        Assert.That(decision.Rationale, Does.Contain("counter=durable_bait"));
        Assert.DoesNotThrow(() => HeadlessPrepPolicyGuard.ValidateDecision(observation, decision));
    }

    private static string Run(HeadlessPolicyObservation observation)
    {
        var policy = ConceptCommitPolicy.CreatePreviewGrounded(GuardedIntent());
        var decision = policy.DecideDeployment(observation);
        HeadlessPolicyGuard.ValidateDeploymentDecision(observation, decision);
        return HeadlessMetricJson.Serialize(new
        {
            decision.Placements,
            decision.Rationale,
            decision.EvidenceFactIds,
            Trace = policy.LastPreviewDecision,
        });
    }

    private static HeadlessConceptIntent GuardedIntent()
        => new(
            "preview-guarded-line",
            "coverage",
            new[] { "build.contains_status:guarded" },
            new[] { "deploy.status:guarded", "activate:status:guarded" },
            "status:guarded",
            new[] { "skill:guarded" },
            new[] { "formation:any_legal" },
            new[] { "visible_counter_connection" },
            "core",
            new[] { "visible_guard_source_unavailable" });

    private static HeadlessPolicyObservation CanonicalObservation(string siteId, string encounterId)
    {
        var roster = new[]
        {
            Hero("hero-1", "warden", "vanguard", "anchor", DeploymentAnchorId.FrontCenter, true,
                Skill("warden-guard", SkillKind.Buff, SkillDelivery.Aura, SkillTargetRule.Self, DamageType.Physical, 0f, "guarded")),
            Hero("hero-2", "guardian", "vanguard", "anchor", DeploymentAnchorId.FrontTop, false,
                Skill("guardian-sunder", SkillKind.Strike, SkillDelivery.Melee, SkillTargetRule.MostExposedEnemy, DamageType.Physical, 1.5f, "sunder"),
                Skill("guardian-guard", SkillKind.Buff, SkillDelivery.Aura, SkillTargetRule.Self, DamageType.Physical, 0f, "guarded")),
            Hero("hero-3", "slayer", "duelist", "bruiser", DeploymentAnchorId.FrontBottom, true,
                Skill("slayer-bleed", SkillKind.Strike, SkillDelivery.Melee, SkillTargetRule.LowestHpEnemy, DamageType.Physical, 1.35f, "bleed")),
            Hero("hero-4", "raider", "duelist", "bruiser", DeploymentAnchorId.FrontTop, false,
                Skill("raider-mark", SkillKind.Strike, SkillDelivery.Melee, SkillTargetRule.MostExposedEnemy, DamageType.Physical, 1.3f, "marked")),
            Hero("hero-5", "hunter", "ranger", "carry", DeploymentAnchorId.BackTop, true,
                Skill("hunter-shot", SkillKind.Strike, SkillDelivery.Projectile, SkillTargetRule.MostExposedEnemy, DamageType.Physical, 5.6f)),
            Hero("hero-6", "scout", "ranger", "carry", DeploymentAnchorId.BackBottom, false,
                Skill("scout-shot", SkillKind.Strike, SkillDelivery.Projectile, SkillTargetRule.MostExposedEnemy, DamageType.Physical, 5.4f)),
            Hero("hero-7", "priest", "mystic", "support", DeploymentAnchorId.BackCenter, true,
                Skill("priest-heal", SkillKind.Heal, SkillDelivery.Melee, SkillTargetRule.LowestHpAlly, DamageType.Healing, 2f)),
            Hero("hero-8", "hexer", "mystic", "controller", DeploymentAnchorId.BackCenter, false,
                Skill("hexer-silence", SkillKind.Strike, SkillDelivery.Ranged, SkillTargetRule.LowestHpEnemy, DamageType.Magical, 2.9f, "silence")),
        };
        return Observation(siteId, encounterId, 4, roster, Preview(encounterId));
    }

    private static HeadlessPolicyObservation IdentityDominanceObservation()
    {
        var roster = new[]
        {
            Hero("identity-anchor", "warden", "vanguard", "anchor", DeploymentAnchorId.FrontCenter, true,
                Skill("identity-guard", SkillKind.Buff, SkillDelivery.Aura, SkillTargetRule.Self, DamageType.Physical, 0f, "guarded")),
            Hero("counter-one", "slayer", "duelist", "bruiser", DeploymentAnchorId.FrontTop, true,
                Skill("counter-one-skill", SkillKind.Strike, SkillDelivery.Ranged, SkillTargetRule.LowestHpEnemy, DamageType.Magical, 3f, "silence")),
            Hero("counter-two", "guardian", "vanguard", "anchor", DeploymentAnchorId.FrontBottom, false,
                Skill("counter-two-skill", SkillKind.Strike, SkillDelivery.Melee, SkillTargetRule.MostExposedEnemy, DamageType.Physical, 1.5f, "sunder")),
        };
        return Observation("site-a", "visible-a", 2, roster, Preview("visible-a"));
    }

    private static HeadlessPolicyObservation Observation(
        string siteId,
        string encounterId,
        int capacity,
        IReadOnlyList<HeadlessHeroObservation> roster,
        HeadlessEnemyPreview preview,
        IReadOnlyList<HeadlessOwnedItemObservation> ownedItems = null)
    {
        var evidence = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HeadlessPolicyEvidence.DeploymentSurfaceSignal] = "fact-deployment",
            [HeadlessPolicyEvidence.RosterSurfaceSignal] = "fact-roster",
            [HeadlessPolicyEvidence.EnemyPreviewSignal] = "fact-preview",
            [HeadlessPolicyEvidence.RewardSurfaceSignal] = "fact-reward",
        };
        for (var index = 0; index < preview.Units.Count; index++)
        {
            evidence[HeadlessPolicyEvidence.EnemyUnitSignal(index)] = $"fact-enemy-{index}";
        }

        foreach (var hero in roster)
        {
            evidence[HeadlessPolicyEvidence.HeroSignal(hero.HeroId)] = $"fact-{hero.HeroId}";
            foreach (var skill in hero.SkillCards)
            {
                evidence[HeadlessPolicyEvidence.HeroSkillSignal(hero.HeroId, skill.SkillId)] =
                    $"fact-{hero.HeroId}-{skill.SkillId}";
            }
        }

        return new HeadlessPolicyObservation(
            1701,
            capacity,
            "chapter-visible",
            siteId,
            roster,
            new[]
            {
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontBottom,
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackCenter,
                DeploymentAnchorId.BackBottom,
            },
            new HeadlessEnemyPreview(
                preview.IsAvailable,
                encounterId,
                preview.FactionId,
                preview.DifficultyBand,
                preview.ThreatSkulls,
                preview.Units,
                preview.BossAuraTag,
                preview.BossUtilityTag,
                preview.RewardDropTags),
            Array.Empty<HeadlessRewardOption>(),
            evidenceFactIdsBySignal: evidence,
            currentPlacements: roster
                .Where(hero => hero.IsDeployed)
                .Take(capacity)
                .Select(hero => new HeadlessPlacement(hero.PreferredAnchor, hero.HeroId))
                .ToArray(),
            ownedItems: ownedItems);
    }

    private static HeadlessEnemyPreview Preview(string encounterId)
        => new(
            true,
            encounterId,
            "visible-faction",
            "chapter-entry",
            1,
            new[]
            {
                new HeadlessEnemyUnitPreview("marksman", "undead", "ranger", "carry", DeploymentAnchorId.BackTop),
                new HeadlessEnemyUnitPreview("priest", "human", "mystic", "support", DeploymentAnchorId.BackCenter),
                new HeadlessEnemyUnitPreview("bastion", "undead", "vanguard", "anchor", DeploymentAnchorId.FrontCenter),
                new HeadlessEnemyUnitPreview("raider", "beastkin", "duelist", "bruiser", DeploymentAnchorId.FrontBottom),
            },
            string.Empty,
            string.Empty,
            Array.Empty<string>());

    private static HeadlessHeroObservation Hero(
        string heroId,
        string archetypeId,
        string classId,
        string roleTag,
        DeploymentAnchorId anchor,
        bool isDeployed,
        params HeadlessSkillObservation[] skills)
        => new(
            heroId,
            archetypeId,
            "human",
            classId,
            roleTag,
            1,
            100,
            100,
            0,
            isDeployed,
            anchor,
            skills);

    private static HeadlessSkillObservation Skill(
        string skillId,
        SkillKind kind,
        SkillDelivery delivery,
        SkillTargetRule targetRule,
        DamageType damageType,
        float range,
        params string[] statuses)
        => new(
            skillId,
            kind,
            "active",
            1f,
            range,
            damageType,
            1f,
            1f,
            0f,
            0f,
            0f,
            0f,
            1f,
            0f,
            true,
            delivery,
            targetRule,
            statuses.Select(value => new HeadlessStatusApplicationObservation(value, value, 2f, 1f, 1)).ToArray());
}
