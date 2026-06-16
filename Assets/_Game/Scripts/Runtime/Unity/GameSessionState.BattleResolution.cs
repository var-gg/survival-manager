using System;
using System.Collections.Generic;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Unity;

public sealed partial class GameSessionState
{
    /// <summary>
    /// 현재 선택된 전투 노드의 BattleState를 구성한다 — 아군 로드아웃 스냅샷 + 적군 인카운터 해석 +
    /// BattleFactory(진형/시너지) + 인카운터 bootstrap. 씬(BattleScreenController 재생)과
    /// 헤드리스(run-to-end) 양쪽이 공유하는 **전투 구성 단일 소스** — "전투 구성은 세션, 완주/재생은 소비자".
    /// allySnapshot도 함께 반환한다(씬은 replay 조립·HUD에, 헤드리스는 run-to-end replay에 재사용).
    /// </summary>
    public bool TryBuildSelectedBattleState(
        out BattleState state,
        out ResolvedEncounterContext encounter,
        out BattleLoadoutSnapshot allySnapshot,
        out string error)
    {
        state = null!;
        encounter = null!;
        allySnapshot = null!;

        allySnapshot = BuildBattleLoadoutSnapshot();
        if (allySnapshot.Allies.Count == 0)
        {
            error = "전투 준비된 아군이 없습니다.";
            return false;
        }

        if (!_combatContentLookup.TryGetCombatSnapshot(out var combatSnapshot, out error))
        {
            return false;
        }

        if (!TryResolveCurrentEncounter(out encounter, out error))
        {
            return false;
        }

        state = BattleFactory.Create(
            allySnapshot.Allies,
            encounter.Enemies,
            allySnapshot.TeamTactic.Posture,
            encounter.EnemyPosture,
            BattleSimulator.DefaultFixedStepSeconds,
            seed: encounter.Context.BattleSeed,
            statusRules: allySnapshot.StatusRules ?? CombatStatusRuleCompiler.Compile(combatSnapshot));
        new EncounterResolutionService(combatSnapshot).ApplyBattleBootstrap(state, encounter);
        return true;
    }

    /// <summary>
    /// 현재 선택된 전투 노드를 **실 전투 sim으로 완주 정산**한다(헤드리스 — 씬 재생 없이). 결정론 sim이
    /// 실 승패를 계산하고, finalUnits까지 MarkBattleResolved에 전달 → 영웅 HP/EXP·dossier가 실 전투 결과로
    /// 갱신된다. 캠페인 드라이버가 auto-resolve 단축(ResolveSelectedExpeditionNode) 대신 이걸 호출하면
    /// 헤드리스 플레이가 진짜로 싸운다. 전투 구성은 <see cref="TryBuildSelectedBattleState"/>(씬과 공유 단일 소스)에 위임.
    /// </summary>
    public bool TryResolveSelectedBattleNodeViaSimulation(out BattleResult result, out string error)
    {
        result = null!;
        var startedAtUtc = DateTime.UtcNow.ToString("O");

        if (!TryBuildSelectedBattleState(out var state, out var encounter, out var allySnapshot, out error))
        {
            return false;
        }

        result = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps);

        // 매치 감사 + 진행(HP/EXP/dossier) 정산 — 씬 경로(BattleScreenController.cs:1172-1197)와 동일하되
        // 재생 원장(formationPayoff)은 헤드리스라 없으므로 생략(보상 화면 표시용 artifact, 게임플레이 truth 아님).
        var replay = ReplayAssembler.Assemble(
            allySnapshot,
            encounter.Enemies,
            result,
            encounter.Context.BattleSeed,
            startedAtUtc,
            DateTime.UtcNow.ToString("O"));
        RecordBattleAudit(replay);
        MarkBattleResolved(result.Winner == TeamSide.Ally, result.StepCount, result.Events.Count, result.FinalUnits);
        return true;
    }
}
