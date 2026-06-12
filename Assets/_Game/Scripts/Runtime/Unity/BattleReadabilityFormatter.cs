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
        // P2: 마지막 이벤트가 아니라 가장 극적인 이벤트가 포커스를 가진다 — 킬(다이브 킬 최상) >
        // 강타 > 회복 > 나머지. 같은 점수면 나중 이벤트가 이긴다(종전 "last" 동작 보존). 판정은
        // typed 필드만 사용(J8 — note 문자열 비파싱).
        var dramaticEvent = ResolveDramaticEvent(step);
        if (dramaticEvent != null)
        {
            focus = new BattleStepFocus(
                dramaticEvent.ActorId.Value,
                dramaticEvent.ActorName,
                dramaticEvent.TargetId?.Value,
                NormalizeTarget(dramaticEvent.TargetName),
                ResolveSemantic(dramaticEvent),
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

    private static BattleEvent? ResolveDramaticEvent(BattleSimulationStep step)
    {
        BattleEvent? best = null;
        var bestScore = int.MinValue;
        foreach (var eventData in step.Events)
        {
            var score = ResolveDramaticScore(eventData);
            if (score >= bestScore)
            {
                best = eventData;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>P2 극적 점수 — 카메라/상태줄 포커스 우선순위. typed 필드만 사용.</summary>
    internal static int ResolveDramaticScore(BattleEvent eventData)
    {
        if (eventData.EventKind == BattleEventKind.Kill)
        {
            return eventData.KillPayload is { IsBacklineDiveKill: true } ? 100 : 90;
        }

        if (eventData.LogCode == BattleLogCode.ActiveSkillHeal)
        {
            return 30;
        }

        // Heavy 임팩트 기준(HeavyImpactDamageThreshold 16f와 동일 대역) 이상의 강타.
        return eventData.Value >= 16f ? 50 : 10;
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

    public static string BuildPlayerFacingState(BattleUnitReadModel unit, BattleSimulationStep? step = null, string? localeCode = null)
    {
        if (!unit.IsAlive)
        {
            return IsKorean(localeCode) ? "전투불능" : "Down";
        }

        var target = NormalizeTarget(unit.TargetName);
        return unit.ActionState switch
        {
            CombatActionState.ExecuteAction => $"{BuildStateVerb(ResolveSemantic(unit, step), windup: true, localeCode)} {Mathf.RoundToInt(unit.WindupProgress * 100f)}% -> {target}",
            CombatActionState.Recover when unit.IsDefending => IsKorean(localeCode) ? "방어 중" : "Guarding",
            CombatActionState.Recover => IsKorean(localeCode) ? "재정비" : "Recovering",
            CombatActionState.Reposition => IsKorean(localeCode) ? "위치 조정" : "Repositioning",
            CombatActionState.BreakContact => IsKorean(localeCode) ? "거리 벌림" : "Breaking Contact",
            CombatActionState.AdvanceToAnchor => IsKorean(localeCode) ? "복귀 중" : "Returning Home",
            CombatActionState.Approach => $"{(IsKorean(localeCode) ? "접근" : "Closing")} -> {target}",
            CombatActionState.SecurePosition => $"{(IsKorean(localeCode) ? "대기" : "Holding")} -> {target}",
            CombatActionState.AcquireTarget => $"{(IsKorean(localeCode) ? "대상 탐색" : "Acquiring")} -> {target}",
            CombatActionState.Spawn => IsKorean(localeCode) ? "배치 중" : "Deploying",
            _ when unit.IsDefending => IsKorean(localeCode) ? "대기" : "Holding",
            _ => string.IsNullOrEmpty(unit.TargetName)
                ? BuildSemanticLabel(ResolveSemantic(unit, step), localeCode)
                : $"{BuildSemanticLabel(ResolveSemantic(unit, step), localeCode)} -> {target}",
        };
    }

    public static string BuildSemanticLabel(BattleActionSemantic semantic, string? localeCode = null)
    {
        if (IsKorean(localeCode))
        {
            return semantic switch
            {
                BattleActionSemantic.BasicAttack => "기본 공격",
                BattleActionSemantic.DamagingSkill => "스킬",
                BattleActionSemantic.HealSupport => "회복",
                BattleActionSemantic.DefendHold => "방어",
                BattleActionSemantic.Reposition => "재배치",
                BattleActionSemantic.Down => "전투불능",
                _ => "준비",
            };
        }

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

    /// <summary>Phase 4 — beat 종류의 플레이어용 표시명(콜아웃/로그 라벨).</summary>
    public static string BuildBeatLabel(CombatBeatType type, string? localeCode = null)
    {
        if (IsKorean(localeCode))
        {
            return type switch
            {
                CombatBeatType.SynergyActivated => "시너지 발동",
                CombatBeatType.BattleStartEffect => "개전 효과",
                CombatBeatType.OnKillEffect => "처치 흡수",
                CombatBeatType.HpThresholdEffect => "위기 발동",
                CombatBeatType.AllyDeathEffect => "응전 태세",
                CombatBeatType.ComboPrimerApplied => "콤보 포석",
                CombatBeatType.ComboConsumed => "콤보 작렬",
                _ => "사건",
            };
        }

        return type switch
        {
            CombatBeatType.SynergyActivated => "Synergy Online",
            CombatBeatType.BattleStartEffect => "Opening Effect",
            CombatBeatType.OnKillEffect => "Kill Effect",
            CombatBeatType.HpThresholdEffect => "Clutch Trigger",
            CombatBeatType.AllyDeathEffect => "Avenger Trigger",
            CombatBeatType.ComboPrimerApplied => "Combo Primer",
            CombatBeatType.ComboConsumed => "Combo Strike",
            _ => "Event",
        };
    }

    public static string BuildShortEventVerb(BattleEvent eventData, string? localeCode = null)
    {
        if (IsKorean(localeCode))
        {
            return ResolveSemantic(eventData) switch
            {
                BattleActionSemantic.BasicAttack => "타격",
                BattleActionSemantic.DamagingSkill => "스킬",
                BattleActionSemantic.HealSupport => "회복",
                BattleActionSemantic.DefendHold => "방어",
                BattleActionSemantic.Down => "전투불능",
                _ => "행동",
            };
        }

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

    private static string BuildStateVerb(BattleActionSemantic semantic, bool windup, string? localeCode)
    {
        if (IsKorean(localeCode))
        {
            return semantic switch
            {
                BattleActionSemantic.BasicAttack => windup ? "준비" : "기본 공격",
                BattleActionSemantic.DamagingSkill => windup ? "시전" : "스킬",
                BattleActionSemantic.HealSupport => windup ? "집중" : "회복",
                BattleActionSemantic.DefendHold => "방어",
                BattleActionSemantic.Reposition => "재배치",
                _ => windup ? "준비" : "대기",
            };
        }

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

    private static bool IsKorean(string? localeCode)
    {
        return string.Equals(localeCode, "ko", StringComparison.OrdinalIgnoreCase);
    }
}
