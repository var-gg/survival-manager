using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Editor.Validation;
using UnityEditor;
using UnityEngine;
using EntityHandle = SM.Core.Ids.EntityId;

namespace SM.Tests.EditMode;

public sealed class LoopAContractClosureTests
{
    private const string TempRoot = "Assets/Resources/_Game/Content/Definitions/__LoopAContractTemp";

    [SetUp]
    public void SetUp()
    {
        DeleteTempRoot();
    }

    [TearDown]
    public void TearDown()
    {
        DeleteTempRoot();
    }

    [Test]
    public void ContentDefinitionValidator_FlagsLoopAAuthorityAndSummonChainViolations()
    {
        EnsureFolder(TempRoot);
        var assets = new List<ScriptableObject>();

        assets.Add(CreateTempAsset<AffixDefinition>("affix_teamwide.asset", asset =>
        {
            asset.Id = "affix_loop_a_teamwide";
            asset.NameKey = "content.affix.loop_a_teamwide.name";
            asset.DescriptionKey = "content.affix.loop_a_teamwide.desc";
            asset.Category = AffixCategoryValue.OffenseFlat;
            asset.AffixFamily = AffixFamilyValue.ConditionalTagged;
            asset.EffectType = AffixEffectTypeValue.StatModifier;
            asset.Effects.Add(new EffectDescriptor
            {
                Layer = AuthorityLayer.Affix,
                Scope = EffectScope.AlliedRosterUnits,
                Capabilities = EffectCapability.ModifyStats,
            });
        }));

        var invalidSynergyTier = CreateTempAsset<SynergyTierDefinition>("synergy_tier_loop_a.asset", asset =>
        {
            asset.Id = "synergy_tier_loop_a";
            asset.NameKey = "content.synergy.loop_a_tier.name";
            asset.DescriptionKey = "content.synergy.loop_a_tier.desc";
            asset.Threshold = 2;
            asset.Effects.Add(new EffectDescriptor
            {
                Layer = AuthorityLayer.Synergy,
                Scope = EffectScope.ShopPhase,
                Capabilities = EffectCapability.ModifyEconomyRule,
            });
        });
        assets.Add(invalidSynergyTier);

        assets.Add(CreateTempAsset<SynergyDefinition>("synergy_loop_a.asset", asset =>
        {
            asset.Id = "synergy_loop_a";
            asset.NameKey = "content.synergy.loop_a.name";
            asset.DescriptionKey = "content.synergy.loop_a.desc";
            asset.CountedTagId = "human";
            asset.Tiers = new List<SynergyTierDefinition> { invalidSynergyTier };
        }));

        assets.Add(CreateTempAsset<SkillDefinitionAsset>("skill_reward_rule.asset", asset =>
        {
            asset.Id = "skill_loop_a_reward_rule";
            asset.NameKey = "content.skill.loop_a_reward_rule.name";
            asset.DescriptionKey = "content.skill.loop_a_reward_rule.desc";
            asset.Kind = SkillKindValue.Strike;
            asset.SlotKind = SkillSlotKindValue.CoreActive;
            asset.DamageType = DamageTypeValue.Physical;
            asset.Delivery = SkillDeliveryValue.Melee;
            asset.TargetRule = SkillTargetRuleValue.NearestEnemy;
            asset.Effects.Add(new EffectDescriptor
            {
                Layer = AuthorityLayer.Skill,
                Scope = EffectScope.RewardPhase,
                Capabilities = EffectCapability.ModifyOfferWeights,
            });
        }));

        assets.Add(CreateTempAsset<AugmentDefinition>("augment_slot_delta.asset", asset =>
        {
            asset.Id = "augment_loop_a_slot_delta";
            asset.NameKey = "content.augment.loop_a_slot_delta.name";
            asset.DescriptionKey = "content.augment.loop_a_slot_delta.desc";
            asset.FamilyId = "augment_loop_a_slot_delta";
            asset.RiskRewardClass = AugmentRiskRewardClassValue.Neutral;
            asset.OfferBucket = AugmentOfferBucketValue.NeutralCombat;
            asset.RosterSlotDelta = 1;
        }));

        assets.Add(CreateTempAsset<SkillDefinitionAsset>("skill_summon_chain.asset", asset =>
        {
            asset.Id = "skill_loop_a_summon_chain";
            asset.NameKey = "content.skill.loop_a_summon_chain.name";
            asset.DescriptionKey = "content.skill.loop_a_summon_chain.desc";
            asset.Kind = SkillKindValue.Utility;
            asset.SlotKind = SkillSlotKindValue.UtilityActive;
            asset.DamageType = DamageTypeValue.Physical;
            asset.Delivery = SkillDeliveryValue.Trap;
            asset.TargetRule = SkillTargetRuleValue.NearestEnemy;
            asset.SummonProfile = new SummonProfile();
            asset.Effects.Add(new EffectDescriptor
            {
                Layer = AuthorityLayer.Skill,
                Scope = EffectScope.GroundArea,
                Capabilities = EffectCapability.SpawnSummon,
                AllowsPersistentSummonChain = true,
            });
        }));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        var report = ContentDefinitionValidator.BuildValidationReport(assets);
        var codes = report.Issues.Select(issue => issue.Code).ToHashSet(StringComparer.Ordinal);

        Assert.That(codes, Contains.Item("loop_a.effect.scope"));
        Assert.That(codes, Contains.Item("loop_a.effect.capability"));
        Assert.That(codes, Contains.Item("loop_a.augment.loadout_delta"));
        Assert.That(codes, Contains.Item("loop_a.summon_chain"));
    }

    [Test]
    public void LoopAEnergyCadence_PrimesSignatureWithinExpectedWindow()
    {
        var signature = new BattleSkillSpec(
            "signature_strike",
            "Signature Strike",
            SkillKind.Strike,
            7f,
            1.2f,
            ResolvedSlotKind: ActionSlotKind.SignatureActive,
            ActivationModel: ActivationModel.Energy,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.HardCommit,
            TargetRuleData: new TargetRule());
        var disabledFlex = new BattleSkillSpec(
            "flex_disabled",
            "Flex Disabled",
            SkillKind.Utility,
            0f,
            0f,
            SlotKind: CompiledSkillSlots.UtilityActive,
            ResolvedSlotKind: ActionSlotKind.FlexActive,
            ActivationModel: ActivationModel.Cooldown,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.HardCommit,
            BaseCooldownSeconds: 2f,
            TargetRuleData: new TargetRule
            {
                Domain = TargetDomain.None,
                FallbackPolicy = TargetFallbackPolicy.Abort,
            });
        var allyDefinition = CombatTestFactory.CreateLoopAUnit("loop_a_ally", signatureActive: signature, flexActive: disabledFlex, attackSpeed: 2.4f, attackRange: 1.1f, hp: 80f);
        var enemyDefinition = CombatTestFactory.CreateLoopAUnit("loop_a_enemy", signatureActive: signature with { Id = "enemy_signature" }, flexActive: disabledFlex with { Id = "enemy_flex" }, attackSpeed: 2.2f, attackRange: 1.1f, hp: 80f);
        var actor = new UnitSnapshot(
            new EntityHandle("ally_loop_a"),
            TeamSide.Ally,
            allyDefinition,
            BattleFactory.ResolveAnchorPosition(TeamSide.Ally, DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveSpawnPosition(TeamSide.Ally, DeploymentAnchorId.FrontCenter));
        var enemy = new UnitSnapshot(
            new EntityHandle("enemy_loop_a"),
            TeamSide.Enemy,
            enemyDefinition,
            BattleFactory.ResolveAnchorPosition(TeamSide.Enemy, DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveSpawnPosition(TeamSide.Enemy, DeploymentAnchorId.FrontCenter));
        var state = new BattleState(new[] { actor }, new[] { enemy }, TeamPostureType.StandardAdvance, TeamPostureType.StandardAdvance, BattleSimulator.DefaultFixedStepSeconds, 7);
        actor.SetPosition(new CombatVector2(-0.6f, 0f));
        enemy.SetPosition(new CombatVector2(0.6f, 0f));

        var elapsed = 0f;
        for (var cycle = 0; cycle < 7; cycle++)
        {
            actor.GainEnergyFromBasicAttackResolved();
            actor.AdvanceTime(0.8f);
            elapsed += 0.8f;
        }
        actor.GainEnergyFromDirectHitTaken();
        elapsed += 0.35f;
        actor.AdvanceTime(0.35f);
        var evaluated = TacticEvaluator.Evaluate(state, actor);

        Assert.That(elapsed, Is.InRange(5f, 9f));
        Assert.That(actor.CanSpendSignatureCastEnergy(), Is.True);
        Assert.That(evaluated.ActionType, Is.EqualTo(BattleActionType.ActiveSkill));
        Assert.That(evaluated.Skill?.Id, Is.EqualTo("signature_strike"));
    }

    [Test]
    public void TargetScoringService_BacklineExposedSelectorFallsBackThenSwapsAfterFrontlineDies()
    {
        var assassin = new UnitSnapshot(
            new EntityHandle("ally_assassin"),
            TeamSide.Ally,
            CombatTestFactory.CreateLoopAUnit("assassin", classId: "duelist", anchor: DeploymentAnchorId.FrontCenter, attackRange: 1.4f),
            BattleFactory.ResolveAnchorPosition(TeamSide.Ally, DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveSpawnPosition(TeamSide.Ally, DeploymentAnchorId.FrontCenter));
        var frontline = new UnitSnapshot(
            new EntityHandle("enemy_frontline"),
            TeamSide.Enemy,
            CombatTestFactory.CreateLoopAUnit("frontline", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter, behavior: new BehaviorProfile(0.25f, 0.1f, 0.1f, 0.1f, 0.5f, 0.5f, 0f, 0f, 0f, 0.5f, 1f, FormationLine.Frontline, RangeDiscipline.HoldBand, 0.8f, 1.2f, 0.4f, 0.25f, 6f, 0f)),
            BattleFactory.ResolveAnchorPosition(TeamSide.Enemy, DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveSpawnPosition(TeamSide.Enemy, DeploymentAnchorId.FrontCenter));
        var backline = new UnitSnapshot(
            new EntityHandle("enemy_backline"),
            TeamSide.Enemy,
            CombatTestFactory.CreateLoopAUnit("backline", classId: "mystic", anchor: DeploymentAnchorId.BackCenter, behavior: new BehaviorProfile(0.25f, 0.1f, 0.1f, 0.1f, 0.5f, 0.5f, 0f, 0f, 0f, 0.5f, 1f, FormationLine.Backline, RangeDiscipline.HoldBand, 2f, 3f, 0.4f, 0.25f, 6f, 0f)),
            BattleFactory.ResolveAnchorPosition(TeamSide.Enemy, DeploymentAnchorId.BackCenter),
            BattleFactory.ResolveSpawnPosition(TeamSide.Enemy, DeploymentAnchorId.BackCenter));
        var state = new BattleState(new[] { assassin }, new[] { frontline, backline }, TeamPostureType.StandardAdvance, TeamPostureType.StandardAdvance, BattleSimulator.DefaultFixedStepSeconds, 7);
        var selectorRule = new TargetRule
        {
            Domain = TargetDomain.EnemyUnit,
            PrimarySelector = TargetSelector.BacklineExposedEnemy,
            FallbackPolicy = TargetFallbackPolicy.NearestReachableEnemy,
            Filters = TargetFilterFlags.ExcludeUntargetable,
        };

        var before = TargetScoringService.SelectTarget(state, assassin, selectorRule);
        frontline.TakeDamage(999f);
        var after = TargetScoringService.SelectTarget(state, assassin, selectorRule);

        Assert.That(before?.Id.Value, Is.EqualTo("enemy_frontline"));
        Assert.That(after?.Id.Value, Is.EqualTo("enemy_backline"));
    }

    [Test]
    public void LoopATargetLock_KeepsCurrentTargetUntilMinimumCommitExpires()
    {
        var basicRule = new TargetRule
        {
            Domain = TargetDomain.EnemyUnit,
            PrimarySelector = TargetSelector.LowestCurrentHpEnemy,
            FallbackPolicy = TargetFallbackPolicy.NearestReachableEnemy,
            Filters = TargetFilterFlags.ExcludeUntargetable,
            MinimumCommitSeconds = 0.75f,
            MaxAcquireRange = 3f,
        };
        var actor = new UnitSnapshot(
            new EntityHandle("ally_lock_actor"),
            TeamSide.Ally,
            CombatTestFactory.CreateLoopAUnit("lock_actor", attackRange: 3f, basicAttackTargetRule: basicRule),
            BattleFactory.ResolveAnchorPosition(TeamSide.Ally, DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveSpawnPosition(TeamSide.Ally, DeploymentAnchorId.FrontCenter));
        var healthy = new UnitSnapshot(
            new EntityHandle("enemy_healthy"),
            TeamSide.Enemy,
            CombatTestFactory.CreateLoopAUnit("enemy_healthy", hp: 20f, anchor: DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveAnchorPosition(TeamSide.Enemy, DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveSpawnPosition(TeamSide.Enemy, DeploymentAnchorId.FrontCenter));
        var wounded = new UnitSnapshot(
            new EntityHandle("enemy_wounded"),
            TeamSide.Enemy,
            CombatTestFactory.CreateLoopAUnit("enemy_wounded", hp: 5f, anchor: DeploymentAnchorId.BackCenter),
            BattleFactory.ResolveAnchorPosition(TeamSide.Enemy, DeploymentAnchorId.BackCenter),
            BattleFactory.ResolveSpawnPosition(TeamSide.Enemy, DeploymentAnchorId.BackCenter));
        var state = new BattleState(new[] { actor }, new[] { healthy, wounded }, TeamPostureType.StandardAdvance, TeamPostureType.StandardAdvance, BattleSimulator.DefaultFixedStepSeconds, 7);

        actor.SetCurrentTarget(healthy.Id);
        actor.StartRetargetLock(0.75f);
        actor.RequestReevaluation(ReevaluationReason.Cadence);
        var locked = TacticEvaluator.Evaluate(state, actor);

        actor.AdvanceTime(1f);
        actor.RequestReevaluation(ReevaluationReason.Cadence);
        var unlocked = TacticEvaluator.Evaluate(state, actor);

        Assert.That(locked.Target?.Id.Value, Is.EqualTo("enemy_healthy"));
        Assert.That(unlocked.Target?.Id.Value, Is.EqualTo("enemy_wounded"));
    }

    [Test]
    public void SummonKill_MirrorsOwnerWithoutOwnerEnergy_AndDirectAllyTargetingExcludesSummon()
    {
        var ownerId = new EntityHandle("ally_owner");
        var owner = new UnitSnapshot(
            ownerId,
            TeamSide.Ally,
            CombatTestFactory.CreateLoopAUnit("owner", anchor: DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveAnchorPosition(TeamSide.Ally, DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveSpawnPosition(TeamSide.Ally, DeploymentAnchorId.FrontCenter));
        var summonProfile = new SummonProfile();
        var summon = new UnitSnapshot(
            new EntityHandle("ally_summon"),
            TeamSide.Ally,
            CombatTestFactory.CreateLoopAUnit(
                "summon",
                anchor: DeploymentAnchorId.BackCenter,
                hp: 8f,
                physPower: 8f,
                entityKind: CombatEntityKind.OwnedSummon,
                ownership: new OwnershipLink
                {
                    OwnerEntity = ownerId,
                    SourceEntity = ownerId,
                    SourceDefinitionId = "skill_summon",
                    SummonGeneration = 0,
                },
                summonProfile: summonProfile,
                behavior: new BehaviorProfile(0.25f, 0.1f, 0.1f, 0.1f, 0.5f, 0.5f, 0f, 0f, 0f, 0.5f, 1f, FormationLine.Midline, RangeDiscipline.HoldBand, 0.8f, 1.2f, 0.4f, 0.25f, 6f, 0f)),
            BattleFactory.ResolveAnchorPosition(TeamSide.Ally, DeploymentAnchorId.BackCenter),
            BattleFactory.ResolveSpawnPosition(TeamSide.Ally, DeploymentAnchorId.BackCenter));
        summon.TakeDamage(2f);
        var enemy = new UnitSnapshot(
            new EntityHandle("enemy_target"),
            TeamSide.Enemy,
            CombatTestFactory.CreateLoopAUnit("enemy_target", hp: 4f, anchor: DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveAnchorPosition(TeamSide.Enemy, DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveSpawnPosition(TeamSide.Enemy, DeploymentAnchorId.FrontCenter));
        var state = new BattleState(new[] { owner, summon }, new[] { enemy }, TeamPostureType.StandardAdvance, TeamPostureType.StandardAdvance, BattleSimulator.DefaultFixedStepSeconds, 7);
        var synergyPackages = SynergyService.BuildForTeam(new[] { owner.Definition, summon.Definition });

        summon.BeginWindup(BattleActionType.BasicAttack, enemy.Id, null);
        summon.FinishWindup();
        var events = CombatActionResolver.Resolve(state, summon);
        var killEvent = events.Single(evt => evt.EventKind == BattleEventKind.Kill);
        var allyTargetRule = new TargetRule
        {
            Domain = TargetDomain.AlliedUnit,
            PrimarySelector = TargetSelector.LowestHpPercentAlly,
            FallbackPolicy = TargetFallbackPolicy.Self,
            Filters = TargetFilterFlags.ExcludeSummons,
        };
        var allyTarget = TargetScoringService.SelectTarget(state, owner, allyTargetRule);

        Assert.That(killEvent.KillPayload, Is.Not.Null);
        Assert.That(killEvent.KillPayload!.IsMirroredFromOwnedSummon, Is.True);
        Assert.That(killEvent.KillPayload.MirroredOwner, Is.EqualTo(ownerId));
        Assert.That(killEvent.KillPayload.GrantsOwnerEnergy, Is.False);
        Assert.That(owner.CurrentEnergy, Is.EqualTo(owner.MaxEnergy > 0f ? owner.Definition.EffectiveEnergy.Starting : 0f));
        Assert.That(allyTarget?.Id, Is.EqualTo(ownerId));
        Assert.That(state.GetTeam(TeamSide.Ally).Select(unit => unit.Id.Value), Does.Contain("ally_summon"));
        Assert.That(synergyPackages, Is.Empty);

        owner.TakeDamage(999f);
        state.ScheduleOwnedEntityDespawnIfOwnerDead();
        for (var index = 0; index < 10; index++)
        {
            state.AdvanceOwnedEntityDespawns();
        }

        Assert.That(summon.IsAlive, Is.False);
    }

    private static T CreateTempAsset<T>(string fileName, Action<T> configure) where T : ScriptableObject
    {
        var path = $"{TempRoot}/{fileName}";
        var asset = ScriptableObject.CreateInstance<T>();
        configure(asset);
        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static void DeleteTempRoot()
    {
        if (!AssetDatabase.IsValidFolder(TempRoot))
        {
            return;
        }

        AssetDatabase.DeleteAsset(TempRoot);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }
}
