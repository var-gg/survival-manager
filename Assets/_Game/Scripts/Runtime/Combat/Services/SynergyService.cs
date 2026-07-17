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
                    new[] { new StatModifier(StatKey.PhysPower, ModifierOp.Flat, count >= 4 ? 4f : 2f, ModifierSource.Synergy, $"race:{raceGroup.Key}:{count}") },
                    ResolveGrantedTeamRuleId(raceGroup.Key, count >= 4 ? 4 : 2, string.Empty)));
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
                    new[] { new StatModifier(StatKey.Armor, ModifierOp.Flat, count >= 3 ? 4f : 2f, ModifierSource.Synergy, $"class:{classGroup.Key}:{count}") },
                    ResolveGrantedTeamRuleId(classGroup.Key, count >= 3 ? 3 : 2, string.Empty)));
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
                rule.Modifiers,
                ResolveGrantedTeamRuleId(rule.CountedTagId, rule.Threshold, rule.GrantedTeamRuleId)));
        }

        return compiled.Count > 0 ? compiled : BuildForTeam(materialized);
    }

    // Move 4/후속 class@3 code-SoT overlay. TeamSynergyTierRule asset은 아직 GrantedTeamRuleId를 저작하지 않으므로,
    // 안정 신원(CountedTagId + Threshold)으로 상위 race/class tier 규칙을 실어 authored/fallback 양쪽의
    // CombatModifierPackage가 같은 규칙 id를 운반하게 한다. 미등록 tier는 authored 값을 그대로 보존한다.
    // public: player-visible synergy observation projector(H100PolicyObservationBuilder)가 오라클과 동일한
    // team-rule id를 tier에 실어야 하므로 이 SoT 오버레이를 공유한다.
    public static string ResolveGrantedTeamRuleId(string countedTagId, int threshold, string authoredRuleId)
    {
        if (threshold == 4)
        {
            return countedTagId switch
            {
                "human" => TeamRuleSet.PhalanxRuleId,
                "beastkin" => TeamRuleSet.BloodrushRuleId,
                "undead" => TeamRuleSet.DeathTollRuleId,
                _ => authoredRuleId ?? string.Empty,
            };
        }

        if (threshold == 3)
        {
            return countedTagId switch
            {
                "vanguard" => TeamRuleSet.BulwarkRuleId,
                "duelist" => TeamRuleSet.ExecuteRuleId,
                "ranger" => TeamRuleSet.KillzoneRuleId,
                "mystic" => TeamRuleSet.ResonanceRuleId,
                _ => authoredRuleId ?? string.Empty,
            };
        }

        return authoredRuleId ?? string.Empty;
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
