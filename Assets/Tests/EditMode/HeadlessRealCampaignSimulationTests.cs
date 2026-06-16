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

        // 핵심 — 헤드리스가 실제로 싸웠다: 최소 1회 실 전투를 정산했고, 모든 전투가 sim tick을 돌렸다.
        Assert.That(outcomes, Is.Not.Empty,
            "Simulate 모드가 캠페인 흐름 안에서 실 전투를 최소 1회 정산.");
        Assert.That(outcomes.All(outcome => outcome.StepCount > 0), Is.True,
            "모든 전투 노드가 실제 BattleSimulator tick을 돌렸다 — auto-resolve 단축(StepCount 0)이 아님.");

        // 캠페인은 실 승패로 구동된다 — 완주(StoryCleared)했거나 실 패배(DefeatedSiteId)로 멈췄거나, 하드코딩 승리가 아니다.
        Assert.That(result.StoryCleared || result.DefeatedSiteId != null, Is.True,
            "캠페인이 실 전투 결과로 종료 — 완주 또는 실 패배(둘 다 정당한 실 sim 귀결).");

        TestContext.WriteLine(
            $"[HeadlessRealCampaign] StoryCleared={result.StoryCleared} " +
            $"clearedSites={result.ClearedSiteIds.Count} battles={outcomes.Count} " +
            $"victories={outcomes.Count(outcome => outcome.Victory)} " +
            $"defeatedSite={result.DefeatedSiteId ?? "(none)"} " +
            $"steps[min={outcomes.Min(o => o.StepCount)},max={outcomes.Max(o => o.StepCount)}]");
        TestContext.WriteLine($"[Squad] {squadDump}");
        TestContext.WriteLine($"[Battles] " + string.Join(" | ", outcomes.Select(o =>
            $"{o.NodeId}={(o.Victory ? "W" : "L")}({o.StepCount})")));
    }
}
