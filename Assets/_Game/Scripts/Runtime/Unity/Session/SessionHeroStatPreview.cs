using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Unity;

public sealed partial class GameSessionState
{
    /// <summary>
    /// 마을 시트용 — 지정 영웅의 effective 전투 스탯을 위해 <b>단일-영웅 합성 blueprint</b>로 canonical
    /// <see cref="LoadoutCompiler"/>를 호출해 컴파일된 유닛을 돌려준다. 배치 여부와 무관하게 동작한다.
    ///
    /// 전투 진입용 <see cref="BuildBattleLoadoutSnapshot"/>과 달리 <b>세션 truth를 절대 건드리지 않는다</b>
    /// (ActiveRun / LastCompiledBattleSnapshot / Profile 미변경). CaptureBlueprintState 도 호출하지 않는다
    /// (그 메서드는 Profile.SquadBlueprints / ActiveBlueprintId 를 mutate 한다).
    ///
    /// 임시 augment(런-스코프 보상)와 정치 지원 package는 마을 시트에 부적절하므로 제외하고, 영구 augment는
    /// active blueprint 기준으로 포함한다(blueprint-wide 효과). 시너지는 개별 유닛 Packages 에 포함되지 않는다
    /// (전투 UnitSnapshot 동일).
    ///
    /// 컴파일 불가(스냅샷 로드 실패 / 아키타입 누락 / 스킬 계약 미충족) 시 null — 호출측은 base 스탯으로 폴백한다.
    /// </summary>
    public BattleUnitLoadout? TryBuildHeroStatPreview(string heroId)
    {
        if (string.IsNullOrWhiteSpace(heroId))
        {
            return null;
        }

        if (!_combatContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            return null;
        }

        var anchor = DeploymentAnchorOrder.FirstOrDefault();
        var blueprintId = string.IsNullOrWhiteSpace(Profile.ActiveBlueprintId)
            ? "blueprint.default"
            : Profile.ActiveBlueprintId;

        // 합성 blueprint: 대상 영웅만 한 앵커에 배치한다. 역할/택틱/포메이션은 numeric 스탯에 영향 없으므로
        // 세션 현재값을 그대로 쓰고 나머지는 비운다. Profile 기록 없이 record 로만 생성 → 무부작용.
        var previewBlueprint = new SquadBlueprintState(
            blueprintId,
            "stat-preview",
            SelectedTeamPosture,
            SelectedTeamTacticId,
            new Dictionary<DeploymentAnchorId, string> { [anchor] = heroId },
            new[] { heroId },
            new Dictionary<string, string>(),
            null);

        // 임시 augment 없음 — 마을 시트는 영구 로드아웃 기준. CompileVersion 만 현재로 맞춘다.
        var previewOverlay = new RunOverlayState(
            0,
            Array.Empty<string>(),
            Array.Empty<string>(),
            LoadoutCompiler.CurrentCompileVersion,
            string.Empty);

        try
        {
            var compiled = _loadoutCompiler.Compile(
                ToHeroRecords(Profile).ToList(),
                ToHeroLoadoutStates(Profile),
                ToHeroProgressionStates(Profile),
                ToItemInstanceStates(Profile),
                ToSkillInstanceStates(Profile),
                ToPassiveSelections(Profile),
                ToPermanentAugmentLoadout(Profile, blueprintId),
                previewBlueprint,
                previewOverlay,
                snapshot);

            return compiled.Allies.FirstOrDefault(unit =>
                string.Equals(unit.Id, heroId, StringComparison.Ordinal));
        }
        catch (InvalidOperationException)
        {
            // 스킬 계약 미충족 등 컴파일 불가 — base 스탯 폴백.
            return null;
        }
    }
}
