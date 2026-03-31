using System.Collections.Generic;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

internal static class CombatTestFactory
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
}
