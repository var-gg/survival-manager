using System;
using System.Linq;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity;

public enum BattleActionSemantic
{
    None = 0,
    BasicAttack = 1,
    DamagingSkill = 2,
    HealSupport = 3,
    DefendHold = 4,
    Reposition = 5,
    Down = 6,
}

public readonly record struct BattleStepFocus(
    string ActorId,
    string ActorName,
    string? TargetId,
    string TargetName,
    BattleActionSemantic Semantic,
    float Progress,
    bool IsWindup);

public static class BattleReadabilityFormatter
{
    public static bool TryResolveStepFocus(BattleSimulationStep step, out BattleStepFocus focus)
    {
        var lastEvent = step.Events.LastOrDefault();
        if (lastEvent != null)
        {
            focus = new BattleStepFocus(
                lastEvent.ActorId.Value,
                lastEvent.ActorName,
                lastEvent.TargetId?.Value,
                NormalizeTarget(lastEvent.TargetName),
                ResolveSemantic(lastEvent),
                1f,
                false);
            return true;
        }

        var windup = step.Units
            .Where(unit => unit.IsAlive && unit.ActionState == CombatActionState.ExecuteAction)
            .OrderByDescending(unit => unit.WindupProgress)
            .ThenBy(unit => unit.Side)
            .ThenBy(unit => unit.Id)
            .FirstOrDefault();
        if (windup != null)
        {
            focus = new BattleStepFocus(
                windup.Id,
                windup.Name,
                windup.TargetId,
                NormalizeTarget(windup.TargetName),
                ResolveSemantic(windup, step),
                windup.WindupProgress,
                true);
            return true;
        }

        var active = step.Units
            .Where(unit => unit.IsAlive)
            .OrderByDescending(unit => GetStatePriority(unit))
            .ThenBy(unit => unit.Side)
            .ThenBy(unit => unit.Id)
            .FirstOrDefault();
        if (active != null)
        {
            focus = new BattleStepFocus(
                active.Id,
                active.Name,
                active.TargetId,
                NormalizeTarget(active.TargetName),
                ResolveSemantic(active, step),
                active.WindupProgress,
                false);
            return true;
        }

        focus = default;
        return false;
    }

    public static BattleActionSemantic ResolveSemantic(BattleEvent eventData)
    {
        return eventData switch
        {
            { EventKind: BattleEventKind.Kill } => BattleActionSemantic.Down,
            { LogCode: BattleLogCode.ActiveSkillHeal } => BattleActionSemantic.HealSupport,
            { LogCode: BattleLogCode.WaitDefend } => BattleActionSemantic.DefendHold,
            { ActionType: BattleActionType.WaitDefend } => BattleActionSemantic.DefendHold,
            { ActionType: BattleActionType.ActiveSkill } => BattleActionSemantic.DamagingSkill,
            { ActionType: BattleActionType.BasicAttack } => BattleActionSemantic.BasicAttack,
            _ => BattleActionSemantic.None,
        };
    }

    public static BattleActionSemantic ResolveSemantic(BattleUnitReadModel unit, BattleSimulationStep? step = null)
    {
        if (!unit.IsAlive)
        {
            return BattleActionSemantic.Down;
        }

        if (unit.ActionState is CombatActionState.Reposition or CombatActionState.AdvanceToAnchor or CombatActionState.BreakContact)
        {
            return BattleActionSemantic.Reposition;
        }

        if (unit.IsDefending || unit.PendingActionType == BattleActionType.WaitDefend)
        {
            return BattleActionSemantic.DefendHold;
        }

        if (unit.PendingActionType == BattleActionType.BasicAttack)
        {
            return BattleActionSemantic.BasicAttack;
        }

        if (unit.PendingActionType == BattleActionType.ActiveSkill)
        {
            if (step != null
                && !string.IsNullOrEmpty(unit.TargetId)
                && step.Units.FirstOrDefault(candidate => candidate.Id == unit.TargetId) is { } target
                && target.Side == unit.Side)
            {
                return BattleActionSemantic.HealSupport;
            }

            if (unit.CurrentSelector.Contains("Ally", StringComparison.Ordinal))
            {
                return BattleActionSemantic.HealSupport;
            }

            return BattleActionSemantic.DamagingSkill;
        }

        return unit.ActionState switch
        {
            CombatActionState.ExecuteAction => BattleActionSemantic.BasicAttack,
            CombatActionState.Recover when unit.IsDefending => BattleActionSemantic.DefendHold,
            _ => BattleActionSemantic.None,
        };
    }

    public static string BuildPlayerFacingState(BattleUnitReadModel unit, BattleSimulationStep? step = null)
    {
        if (!unit.IsAlive)
        {
            return "Down";
        }

        var target = NormalizeTarget(unit.TargetName);
        return unit.ActionState switch
        {
            CombatActionState.ExecuteAction => $"{BuildStateVerb(ResolveSemantic(unit, step), windup: true)} {Mathf.RoundToInt(unit.WindupProgress * 100f)}% -> {target}",
            CombatActionState.Recover when unit.IsDefending => "Guarding",
            CombatActionState.Recover => "Recovering",
            CombatActionState.Reposition => "Repositioning",
            CombatActionState.BreakContact => "Breaking Contact",
            CombatActionState.AdvanceToAnchor => "Returning Home",
            CombatActionState.Approach => $"Closing -> {target}",
            CombatActionState.SecurePosition => $"Holding -> {target}",
            CombatActionState.AcquireTarget => $"Acquiring -> {target}",
            CombatActionState.Spawn => "Deploying",
            _ when unit.IsDefending => "Holding",
            _ => string.IsNullOrEmpty(unit.TargetName)
                ? BuildSemanticLabel(ResolveSemantic(unit, step))
                : $"{BuildSemanticLabel(ResolveSemantic(unit, step))} -> {target}",
        };
    }

    public static string BuildSemanticLabel(BattleActionSemantic semantic)
    {
        return semantic switch
        {
            BattleActionSemantic.BasicAttack => "Basic Attack",
            BattleActionSemantic.DamagingSkill => "Skill",
            BattleActionSemantic.HealSupport => "Heal",
            BattleActionSemantic.DefendHold => "Guard",
            BattleActionSemantic.Reposition => "Reposition",
            BattleActionSemantic.Down => "Down",
            _ => "Ready",
        };
    }

    public static string BuildShortEventVerb(BattleEvent eventData)
    {
        return ResolveSemantic(eventData) switch
        {
            BattleActionSemantic.BasicAttack => "hit",
            BattleActionSemantic.DamagingSkill => "skill",
            BattleActionSemantic.HealSupport => "heal",
            BattleActionSemantic.DefendHold => "guard",
            BattleActionSemantic.Down => "downed",
            _ => "acted",
        };
    }

    public static float ComputePressureScore(BattleSimulationStep step, TeamSide side)
    {
        var friendly = step.Units.Where(unit => unit.Side == side).ToList();
        var enemy = step.Units.Where(unit => unit.Side != side).ToList();
        return ComputeTeamPostureScore(friendly) - ComputeTeamPostureScore(enemy);
    }

    public static string HumanizeToken(string value, string fallback = "-")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(c);
        }

        return builder.ToString().Replace('_', ' ').Trim();
    }

    private static string BuildStateVerb(BattleActionSemantic semantic, bool windup)
    {
        return semantic switch
        {
            BattleActionSemantic.BasicAttack => windup ? "Windup" : "Basic Attack",
            BattleActionSemantic.DamagingSkill => windup ? "Casting" : "Skill",
            BattleActionSemantic.HealSupport => windup ? "Channeling" : "Heal",
            BattleActionSemantic.DefendHold => "Guard",
            BattleActionSemantic.Reposition => "Reposition",
            _ => windup ? "Windup" : "Ready",
        };
    }

    private static int GetStatePriority(BattleUnitReadModel unit)
    {
        if (!unit.IsAlive)
        {
            return -1;
        }

        return unit.ActionState switch
        {
            CombatActionState.ExecuteAction => 6,
            CombatActionState.Reposition => 5,
            CombatActionState.Recover => unit.IsDefending ? 4 : 3,
            CombatActionState.Approach => 2,
            CombatActionState.SecurePosition => 1,
            _ => 0,
        };
    }

    private static float ComputeTeamPostureScore(System.Collections.Generic.IReadOnlyList<BattleUnitReadModel> units)
    {
        if (units.Count == 0)
        {
            return 0f;
        }

        var maxHp = Mathf.Max(1f, units.Sum(unit => Mathf.Max(1f, unit.MaxHealth)));
        var hpRatio = Mathf.Clamp01(units.Sum(unit => Mathf.Max(0f, unit.CurrentHealth)) / maxHp);
        var aliveRatio = units.Count(unit => unit.IsAlive) / (float)units.Count;
        var activeRatio = units.Count(unit =>
                unit.IsAlive && unit.ActionState is CombatActionState.ExecuteAction or CombatActionState.Approach or CombatActionState.SecurePosition)
            / (float)units.Count;
        return (aliveRatio * 0.45f) + (hpRatio * 0.4f) + (activeRatio * 0.15f);
    }

    private static string NormalizeTarget(string? targetName)
    {
        return string.IsNullOrWhiteSpace(targetName) ? "-" : targetName;
    }
}
