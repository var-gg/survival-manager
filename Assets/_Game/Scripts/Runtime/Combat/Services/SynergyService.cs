using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Stats;

namespace SM.Combat.Services;

public static class SynergyService
{
    public static IReadOnlyList<CombatModifierPackage> BuildForTeam(IEnumerable<BattleUnitLoadout> units)
    {
        var list = new List<CombatModifierPackage>();
        var materialized = units
            .Where(unit => unit.EntityKind == SM.Core.Contracts.CombatEntityKind.RosterUnit)
            .ToList();

        // V1 권위 breakpoint: 세력(race) 2/4 · 직업(class) 2/3 (wiki-combat-v1-index / project_v1_system_authority).
        // 이 폴백은 authored 시너지 tier rule 이 없을 때만 쓰이는 안전망이지만 V1 티어와 일치해야 한다.
        foreach (var raceGroup in materialized.GroupBy(x => x.RaceId))
        {
            var count = raceGroup.Count();
            if (count >= 2)
            {
                list.Add(new CombatModifierPackage(
                    $"race:{raceGroup.Key}:{count}",
                    ModifierSource.Synergy,
                    new[] { new StatModifier(StatKey.PhysPower, ModifierOp.Flat, count >= 4 ? 4f : 2f, ModifierSource.Synergy, $"race:{raceGroup.Key}:{count}") }));
            }
        }

        foreach (var classGroup in materialized.GroupBy(x => x.ClassId))
        {
            var count = classGroup.Count();
            if (count >= 2)
            {
                list.Add(new CombatModifierPackage(
                    $"class:{classGroup.Key}:{count}",
                    ModifierSource.Synergy,
                    new[] { new StatModifier(StatKey.Armor, ModifierOp.Flat, count >= 3 ? 4f : 2f, ModifierSource.Synergy, $"class:{classGroup.Key}:{count}") }));
            }
        }

        return list;
    }

    public static IReadOnlyList<CombatModifierPackage> BuildForTeam(
        IEnumerable<BattleUnitLoadout> units,
        IEnumerable<TeamSynergyTierRule> tierRules)
    {
        var materialized = units
            .Where(unit => unit.EntityKind == SM.Core.Contracts.CombatEntityKind.RosterUnit)
            .ToList();
        var compiled = new List<CombatModifierPackage>();
        foreach (var rule in tierRules)
        {
            var count = materialized.Count(unit => unit.CompileTags?.Contains(rule.CountedTagId) == true);
            if (count < rule.Threshold)
            {
                continue;
            }

            compiled.Add(new CombatModifierPackage(
                $"synergy:{rule.SynergyId}:{rule.Threshold}",
                ModifierSource.Synergy,
                rule.Modifiers));
        }

        return compiled.Count > 0 ? compiled : BuildForTeam(materialized);
    }

    /// <summary>
    /// Phase 3 시너지 발현 — 활성화된 시너지 브레이크포인트를 전투 시작(step 0) beat 으로 남긴다.
    /// 식별자는 별도 배선 없이 유닛 loadout 에 보존된 팀 패키지(<see cref="CombatModifierPackage"/>,
    /// Source=Synergy, SourceId="synergy:{id}:{threshold}" 또는 V1 폴백 "race:/class:{id}:{n}")에서
    /// 자가 발견한다. 이로써 스탯-only 시너지도 최소 1회 화면 사건으로 발현된다(마스터 플랜
    /// "No hidden stat-only synergy" acceptance). 팀 단위 beat 라 SourceId/TargetId 는 null 이다.
    /// </summary>
    public static void EmitActivationBeats(BattleState state)
    {
        EmitTeamActivationBeats(state, TeamSide.Ally);
        EmitTeamActivationBeats(state, TeamSide.Enemy);
    }

    private static void EmitTeamActivationBeats(BattleState state, TeamSide side)
    {
        var team = state.GetTeam(side).ToList();
        if (team.Count == 0)
        {
            return;
        }

        var centroid = CombatVector2.Zero;
        foreach (var unit in team)
        {
            centroid += unit.Position;
        }

        centroid /= team.Count;

        var activations = team
            .SelectMany(unit => unit.Definition.TeamPackages
                                ?? (IReadOnlyList<CombatModifierPackage>)System.Array.Empty<CombatModifierPackage>())
            .Where(package => package.Source == ModifierSource.Synergy)
            .GroupBy(package => package.SourceId, System.StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(package => package.SourceId, System.StringComparer.Ordinal);

        foreach (var package in activations)
        {
            state.RecordBeat(
                CombatBeatType.SynergyActivated,
                side,
                sourceId: null,
                targetId: null,
                chainId: 0,
                CombatBeatImportance.SynergyActivated,
                value: 0f,
                centroid,
                package.SourceId);
        }
    }
}
