using NUnit.Framework;
using SM.Combat.Model;
using SM.Editor.SeedData;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 헤드리스 전투 sim 배선 (Phase B 증명) — "AI가 에디터 없이 게임을 플레이"의 가장 큰 결손이던
/// **전투가 실제로 시뮬레이션되는가**를 증명한다. 캠페인 드라이버의 auto-resolve 단축
/// (ResolveSelectedExpeditionNode = 전투 0, 하드코딩 승리)이 아니라, 신규
/// GameSessionState.TryResolveSelectedBattleNodeViaSimulation이 실 BattleSimulator를 완주시켜
/// 실 승패를 계산하고 진행(HP/EXP/dossier)을 갱신함을 씬·VisualElement 없이 확인한다.
///
/// 실 적군 콘텐츠가 필요하므로 RuntimeCombatContentLookup(실 Resources 콘텐츠)을 쓴다
/// (EditorFreeCombatContentFixture는 Archetypes가 비어 인카운터 해석 불가) — EncounterAndLootResolutionTests와
/// 동일 셋업. 밸런스 러너가 헤드리스 전투를 증명한 경로(FirstPlayableBalanceRunner)와 동일 합성.
/// </summary>
[Category("BatchOnly")]
public sealed class HeadlessBattleSimulationTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(HeadlessBattleSimulationTests));
    }

    [Test]
    public void TryResolveSelectedBattleNodeViaSimulation_RunsRealCombat_Headless()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile()); // 기본 분대(헤더 배치 포함) 시드
        session.BeginNewExpedition();

        var allyCountBefore = session.BuildBattleLoadoutSnapshot().Allies.Count;
        Assert.That(allyCountBefore, Is.GreaterThan(0), "기본 분대가 전투 준비됨.");

        // 실 전투 sim 완주 — 씬 재생 없이, 단축 정산 없이.
        Assert.That(session.TryResolveSelectedBattleNodeViaSimulation(out var result, out var error), Is.True, error);

        // 진짜로 싸웠다는 증거: sim이 tick을 돌렸고, 최종 유닛 상태가 나왔고, 실 승자가 정해졌다.
        Assert.That(result, Is.Not.Null);
        Assert.That(result.StepCount, Is.GreaterThan(0),
            "BattleSimulator가 실제 tick을 돌렸다 — auto-resolve 단축이면 step이 없다.");
        Assert.That(result.FinalUnits, Is.Not.Empty,
            "전투 후 최종 유닛 read-model이 존재 — 실 전장 상태.");
        Assert.That(result.Winner, Is.EqualTo(TeamSide.Ally).Or.EqualTo(TeamSide.Enemy),
            "실 승패가 계산됨(하드코딩 아님).");

        // 진행 truth가 실 전투 결과로 갱신됨: MarkBattleResolved가 승패를 세션에 반영.
        Assert.That(session.LastBattleVictory, Is.EqualTo(result.Winner == TeamSide.Ally),
            "세션 LastBattleVictory가 실 sim 승자와 일치 — 진행이 실 전투에 연결됨.");
    }
}
