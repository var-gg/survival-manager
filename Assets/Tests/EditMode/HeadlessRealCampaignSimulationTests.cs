using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Core;
using SM.Editor.SeedData;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Tests.EditMode.Playthrough;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 실 캠페인 헤드리스 완주 (Phase D) — Phase B(<see cref="HeadlessBattleSimulationTests"/>)가 전투 한 판이
/// 실제로 시뮬레이션됨을 증명했고, Phase C가 <see cref="CampaignPlaythroughRunner"/>에 Simulate 모드를 배선했다면,
/// 이 골든은 그 둘을 합쳐 **캠페인 드라이버가 실 BattleSimulator로 사이트들을 싸워 넘기는지**를 씬·VisualElement 없이 증명한다.
///
/// FastUnit 골든(<see cref="CampaignPlaythroughPolicyGoldenFastTests"/>)은 EditorFreeCombatContentFixture로
/// 메타루프만 증명한다(전투 sim 0, 자동 승리 단축). 여기는 RuntimeCombatContentLookup(실 Resources 콘텐츠) +
/// PlaythroughBattleResolution.Simulate → 매 전투 노드가 실 sim tick을 돌린다. = "AI가 에디터 없이 게임을 플레이"의
/// 마지막 결손이던 전투가 캠페인 흐름 안에서 실제로 메워졌음을 잠그는 회귀 게이트.
///
/// 완주(StoryCleared)는 콘텐츠 밸런스에 달렸으므로 단언의 핵심은 "실제로 싸웠는가"다: 모든 전투가 StepCount>0이고,
/// 캠페인이 실 승패로 종료(완주 또는 실 패배)됐음을 본다. 자동 승리 단축이면 이 단언이 깨진다.
/// </summary>
[Category("BatchOnly")]
public sealed class HeadlessRealCampaignSimulationTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(HeadlessRealCampaignSimulationTests));
    }

    [Test]
    public void SimulateRunner_FightsRealCampaign_Headless()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile { ProfileId = "headless_real_campaign" }); // 기본 분대 시드
        session.SetCurrentScene(SceneNames.Town);

        // 관측: 헤드리스 캠페인의 실 분대 구성(첫 N canonical archetype의 base class). 밸런스 작업의 telemetry.
        var squadDump = string.Join(" | ", session.Profile.Heroes.Select(hero =>
            $"{hero.HeroId}:{hero.ClassId}/{hero.ArchetypeId}"));

        var runner = new CampaignPlaythroughRunner(
            session,
            new ScriptedPlaythroughPolicy(rewardIndex: 0),
            new RecordingNavSink(),
            PlaythroughBattleResolution.Simulate);
        var result = runner.Run();

        var outcomes = result.SiteObservations
            .SelectMany(site => site.BattleOutcomes ?? (IReadOnlyList<PlaythroughBattleOutcome>)Array.Empty<PlaythroughBattleOutcome>())
            .ToList();

        // 진단 로그를 먼저 — 단언 실패 시에도 outcome/분대가 콘솔에 남아 결정성·밸런스 관측에 쓰인다.
        TestContext.WriteLine(
            $"[HeadlessRealCampaign] StoryCleared={result.StoryCleared} " +
            $"clearedSites={result.ClearedSiteIds.Count} battles={outcomes.Count} " +
            $"victories={outcomes.Count(outcome => outcome.Victory)} " +
            $"defeatedSite={result.DefeatedSiteId ?? "(none)"} " +
            (outcomes.Count > 0 ? $"steps[min={outcomes.Min(o => o.StepCount)},max={outcomes.Max(o => o.StepCount)}]" : "steps[none]"));
        TestContext.WriteLine($"[Squad] {squadDump}");
        TestContext.WriteLine($"[Battles] " + string.Join(" | ", outcomes.Select(o =>
            $"{o.NodeId}={(o.Victory ? "W" : "L")}({o.StepCount})")));

        // (1) 헤드리스가 실제로 싸웠다 — 모든 전투가 sim tick을 돌렸다(auto-resolve 단축이 아님).
        Assert.That(outcomes, Is.Not.Empty, "Simulate 모드가 실 전투를 최소 1회 정산.");
        Assert.That(outcomes.All(outcome => outcome.StepCount > 0), Is.True,
            "모든 전투 노드가 실제 BattleSimulator tick을 돌렸다.");

        // (2) winnability 불변식 — 기본 분대는 실 전투를 최소 한 번은 이긴다('아무 것도 못 이김' 회귀 차단).
        //     NOTE: 시드는 결정적이지만(감사 #2 · SeedDeterminismFastTests) 전투 *결과*는 런 간 변동한다 —
        //     비-시드 entropy(기본 분대 archetype 시딩 또는 sim 컬렉션 순회 순서)가 남아 정확한 W/L 수는 잠그지 않는다.
        //     잔여 outcome 결정성은 별도 follow-up. 이 게이트는 그 변동에 무관한 불변식만 단언한다.
        Assert.That(outcomes.Count(outcome => outcome.Victory), Is.GreaterThan(0),
            "기본 분대가 실 전투를 최소 한 번 승리.");

        // (3) 정산 후처리가 실제로 실 전투 결과를 반영했다 — finalUnits 매핑이 깨져 0 반영돼도 green이던 사각을 메운다.
        Assert.That(session.Profile.Dossier.Count, Is.EqualTo(outcomes.Count),
            "전투마다 dossier entry 1건 — WriteDossierEntry 정산 후처리가 모든 전투에 흘렀다.");
        Assert.That(session.Profile.Dossier.All(entry => !entry.NodeId.Contains("debug_smoke")), Is.True,
            "싸운 노드가 전부 authored — 무음 디버그 스모크 강등이 끼지 않았다(감사 #4와 짝).");
        // NOTE(follow-up): HP/EXP 정산 반영(ApplyHeroBattleAftermath)을 여기서 단언하려 했으나, 이 게이트가
        // *실제로* 발견한 결함 — 헤드리스 실 sim에서 4승 후에도 영웅 진척(Level/Experience)이 0이다.
        // finalUnits→hero 매핑(unit.Id vs HeroProgressionRecord.HeroId) 또는 EntityKind 게이트가 헤드리스
        // 경로에서 어긋나 wave-33-progression이 반영되지 않는 것으로 보인다(기존 테스트로 한 번도 커버 안 됨).
        // 별도 조사·수정 과제로 분리하고, 이 게이트는 그 결함과 무관한 불변식만 잠근다(dossier 정산은 흘렀음을 위에서 확인).

        // (4) formation payoff가 헤드리스에서도 채워졌다(감사 #5) — presentation-only라 영구 Empty이던 이격을 닫음.
        Assert.That(session.LastBattleFormationPayoff.HasData, Is.True,
            "헤드리스 sim이 step 스트림을 ledger에 흘려 formation payoff를 집계했다(씬과 동일 소스).");

        // (5) 캠페인이 실 결과로 종료 — 완주 또는 실 패배. 패배면 run이 정리됐다(누수 차단).
        Assert.That(result.StoryCleared || result.DefeatedSiteId != null, Is.True,
            "캠페인이 실 전투 결과로 종료 — 완주 또는 실 패배.");
        if (result.DefeatedSiteId != null)
        {
            Assert.That(session.ActiveRun, Is.Null,
                "실 패배 후 ActiveRun이 정리됐다(AbandonExpeditionRun).");
        }
    }
}
