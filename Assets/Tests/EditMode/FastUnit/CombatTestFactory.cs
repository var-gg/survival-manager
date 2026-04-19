using System.Collections.Generic;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

public static class CombatTestFactory
{
    public static BattleUnitLoadout CreateUnit(
        string id,
        string race = "human",
        string classId = "vanguard",
        DeploymentAnchorId anchor = DeploymentAnchorId.FrontCenter,
        float hp = 20f,
        float attack = 5f,
        float defense = 2f,
        float speed = 3f,
        float healPower = 0f,
        float moveSpeed = 1.9f,
        float attackRange = 1.2f,
        float aggroRadius = 8f,
        float attackWindup = 0.1f,
        float attackCooldown = 0.6f,
        float leashDistance = 6f,
        float targetSwitchDelay = 0.3f,
        float preferredDistance = 0f,
        IReadOnlyList<TacticRule>? tactics = null,
        IReadOnlyList<BattleSkillSpec>? skills = null,
        FootprintProfile? footprint = null,
        BehaviorProfile? behavior = null,
        MobilityActionProfile? mobility = null)
    {
        return new BattleUnitLoadout(
            id,
            id,
            race,
            classId,
            anchor,
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = hp,
                [StatKey.Attack] = attack,
                [StatKey.Defense] = defense,
                [StatKey.Speed] = speed,
                [StatKey.HealPower] = healPower,
                [StatKey.MoveSpeed] = moveSpeed,
                [StatKey.AttackRange] = attackRange,
                [StatKey.AggroRadius] = aggroRadius,
                [StatKey.AttackWindup] = attackWindup,
                [StatKey.AttackCooldown] = attackCooldown,
                [StatKey.LeashDistance] = leashDistance,
                [StatKey.TargetSwitchDelay] = targetSwitchDelay,
            },
            new[]
            {
                new UnitRuleChain(
                    $"rules:{id}",
                    tactics ?? new[]
                    {
                        new TacticRule(0, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.LowestHpEnemy),
                        new TacticRule(1, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
                    })
            },
            skills ?? new BattleSkillSpec[0],
            Footprint: footprint,
            Behavior: behavior,
            Mobility: mobility,
            PreferredDistance: preferredDistance);
    }

    public static BattleState CreateBattleState(
        IReadOnlyList<BattleUnitLoadout> allies,
        IReadOnlyList<BattleUnitLoadout> enemies,
        TeamPostureType allyPosture = TeamPostureType.StandardAdvance,
        TeamPostureType enemyPosture = TeamPostureType.StandardAdvance,
        int seed = 7)
    {
        return BattleFactory.Create(allies, enemies, allyPosture, enemyPosture, BattleSimulator.DefaultFixedStepSeconds, seed);
    }

    public static BattleUnitLoadout CreateLoopAUnit(
        string id,
        string race = "human",
        string classId = "vanguard",
        DeploymentAnchorId anchor = DeploymentAnchorId.FrontCenter,
        float hp = 20f,
        float physPower = 5f,
        float armor = 2f,
        float attackSpeed = 2.5f,
        float moveSpeed = 1.9f,
        float attackRange = 1.2f,
        BattleSkillSpec? signatureActive = null,
        BattleSkillSpec? flexActive = null,
        BattleMobilitySpec? mobilityReaction = null,
        BehaviorProfile? behavior = null,
        FootprintProfile? footprint = null,
        TargetRule? basicAttackTargetRule = null,
        CombatEntityKind entityKind = CombatEntityKind.RosterUnit,
        OwnershipLink? ownership = null,
        SummonProfile? summonProfile = null,
        EnergyProfile? energy = null)
    {
        var signature = signatureActive ?? new BattleSkillSpec(
            $"{id}:signature",
            "Signature",
            SkillKind.Strike,
            6f,
            attackRange,
            ResolvedSlotKind: ActionSlotKind.SignatureActive,
            ActivationModel: ActivationModel.Energy,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.HardCommit,
            TargetRuleData: new TargetRule());
        var flex = flexActive ?? new BattleSkillSpec(
            $"{id}:flex",
            "Flex",
            SkillKind.Utility,
            2f,
            attackRange,
            SlotKind: CompiledSkillSlots.UtilityActive,
            ResolvedSlotKind: ActionSlotKind.FlexActive,
            ActivationModel: ActivationModel.Cooldown,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.HardCommit,
            BaseCooldownSeconds: 1.5f,
            TargetRuleData: new TargetRule
            {
                Domain = TargetDomain.EnemyUnit,
                PrimarySelector = TargetSelector.LowestCurrentHpEnemy,
                FallbackPolicy = TargetFallbackPolicy.NearestReachableEnemy,
                Filters = TargetFilterFlags.ExcludeUntargetable,
            });
        return new BattleUnitLoadout(
            id,
            id,
            race,
            classId,
            anchor,
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = hp,
                [StatKey.PhysPower] = physPower,
                [StatKey.Armor] = armor,
                [StatKey.AttackSpeed] = attackSpeed,
                [StatKey.MoveSpeed] = moveSpeed,
                [StatKey.AttackRange] = attackRange,
                [StatKey.AggroRadius] = 8f,
                [StatKey.AttackWindup] = 0.1f,
                [StatKey.AttackCooldown] = 0.7f,
                [StatKey.LeashDistance] = 6f,
                [StatKey.TargetSwitchDelay] = 0.75f,
            },
            new[]
            {
                new UnitRuleChain(
                    $"rules:{id}",
                    new[]
                    {
                        new TacticRule(0, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
                    })
            },
            new[] { signature, flex },
            Footprint: footprint,
            Behavior: behavior ?? new BehaviorProfile(0.25f, 0.1f, 0.1f, 0.1f, 0.5f, 0.5f, 0f, 0f, 0f, 0.5f, 1f, FormationLine.Frontline, RangeDiscipline.HoldBand, 0.8f, attackRange, 0.4f, 0.25f, 6f, 0f),
            Mobility: mobilityReaction?.Profile,
            BasicAttack: new BattleBasicAttackSpec($"{id}:basic", "Basic", DamageType.Physical, basicAttackTargetRule ?? new TargetRule()),
            SignatureActive: signature,
            FlexActive: flex,
            SignaturePassive: new BattlePassiveSpec($"{id}:signature_passive", "Signature Passive"),
            FlexPassive: new BattlePassiveSpec($"{id}:flex_passive", "Flex Passive", ActionSlotKind.FlexPassive),
            MobilityReaction: mobilityReaction,
            Energy: energy ?? new EnergyProfile(100f, 10f),
            EntityKind: entityKind,
            Ownership: ownership,
            SummonProfile: summonProfile);
    }
}
