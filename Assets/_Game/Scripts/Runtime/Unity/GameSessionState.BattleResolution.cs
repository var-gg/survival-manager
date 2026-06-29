using System;
using System.Collections.Generic;
using System.Linq;
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

        // formation payoff를 헤드리스에서도 채운다 — 씬은 타임라인 재생 중 ledger.Record를 누적하지만,
        // 헤드리스는 sim 스텝 스트림을 onStep 콜백으로 같은 ledger에 흘려 1회 집계한다(씬과 동일 소스, 동일 집계).
        // 결과(BattleResult)는 콜백 유무와 무관하게 동일하다(순수 관찰). 이로써 보상 화면 payoff readout이
        // 헤드리스 게이트에서도 관측 가능해져 "presentation에만 살아 헤드리스 영구 Empty"이던 이격을 닫는다.
        var highlightLedger = new BattleHighlightLedger();
        result = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps, highlightLedger.Record);

        // 매치 감사 + 진행(HP/EXP/dossier) 정산 — 씬 경로(BattleScreenController.FinishBattle)와 동일.
        var replay = ReplayAssembler.Assemble(
            allySnapshot,
            encounter.Enemies,
            result,
            encounter.Context.BattleSeed,
            startedAtUtc,
            DateTime.UtcNow.ToString("O"));
        RecordBattleAudit(replay);
        // out 파라미터(result)는 람다에서 못 쓰므로(CS1628) FinalUnits를 지역 변수로 캡쳐 후 이름 해석에 사용.
        var finalUnits = result.FinalUnits;
        var formationPayoff = highlightLedger.BuildFormationPayoff(
            actorId => finalUnits.FirstOrDefault(unit => string.Equals(unit.Id, actorId, StringComparison.Ordinal))?.Name ?? actorId);
        MarkBattleResolved(result.Winner == TeamSide.Ally, result.StepCount, result.Events.Count, finalUnits, formationPayoff);
        return true;
    }
}
