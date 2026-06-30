using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Editor.SeedData;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Tests.EditMode.Playthrough;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 헤드리스 실 캠페인 **결정성** 게이트 — <see cref="HeadlessRealCampaignSimulationTests"/>가 "실제로 싸웠나"를
/// 증명한다면, 이 골든은 "같은 입력이면 같은 결과인가"를 증명한다. 같은 프로세스 안에서 두 개의 독립
/// <see cref="GameSessionState"/>(같은 ProfileId·같은 콘텐츠)로 캠페인을 끝까지 구동해, 전투 노드 W/L·step
/// 시퀀스와 최종 영웅 진행(레벨/EXP)·dossier가 **byte-identical** 인지 단언한다.
///
/// <para>배경(엔지니어링 감사 follow-up — analysis-engineering-audit-headless-divergence-fallback-deadcode-2026-06):
/// "같은 시드·분대인데 런간 W/L이 변동한다"는 신규발견 (a)의 회귀 게이트다. 감사 #2가 전투 시드를 RunId GUID에서
/// 콘텐츠 좌표 SHA256으로 결정화한 뒤(<see cref="SeedDeterminismFastTests"/>), 6-에이전트 정적 감사가 게임플레이에
/// 도달하는 프로세스 가변 엔트로피가 0건임을 증명했다 — 즉 캠페인 결과는 결정적이어야 한다. 이 테스트가 그
/// 불변식을 잠근다. 동일-프로세스 2회로 가변 static state·컬렉션 순회 순서·정렬 누락을 포착한다(프로세스 간
/// FP 결정성은 <see cref="BattleDeterminismBaselineTests"/>의 same-seed step-stream 골든이 별도로 받친다).</para>
///
/// <para>BatchOnly: <see cref="RuntimeCombatContentLookup"/> 실 Resources 콘텐츠가 필요하다
/// (<see cref="HeadlessRealCampaignSimulationTests"/>와 동일 setup).</para>
/// </summary>
[Category("BatchOnly")]
public sealed class HeadlessCampaignDeterminismTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(HeadlessCampaignDeterminismTests));
    }

    [Test]
    public void TwoIndependentCampaignRuns_AreByteIdentical_SameProfileSeed()
    {
        var first = RunHeadlessCampaign();
        var second = RunHeadlessCampaign();

        // 진단 로그 먼저 — 단언 실패 시에도 두 런의 전체 시퀀스가 콘솔에 남아 어느 노드에서 갈렸는지 보인다.
        TestContext.WriteLine($"[run A] {first.OutcomeSignature}");
        TestContext.WriteLine($"[run B] {second.OutcomeSignature}");
        TestContext.WriteLine($"[run A] progression={first.ProgressionSignature}");
        TestContext.WriteLine($"[run B] progression={second.ProgressionSignature}");

        // (1) 전투 노드 W/L·step 시퀀스가 완전히 동일 — "victories 5 vs 4" 류 런간 변동의 직접 회귀 차단.
        Assert.That(second.OutcomeSignature, Is.EqualTo(first.OutcomeSignature),
            "같은 ProfileId·콘텐츠로 두 번 구동한 캠페인의 전투 W/L·step 시퀀스가 갈렸다 — sim 비결정.");

        // (2) 캠페인 종료 형태(완주/패배 사이트·클리어 사이트)도 동일.
        Assert.That(second.TerminationSignature, Is.EqualTo(first.TerminationSignature),
            "캠페인 종료 형태(StoryCleared/DefeatedSite/ClearedSites)가 런간 갈렸다.");

        // (3) 정산 후처리 결과(영웅 레벨/EXP·dossier)도 동일 — 전투 결과가 같으면 진행도 같아야 한다.
        Assert.That(second.ProgressionSignature, Is.EqualTo(first.ProgressionSignature),
            "전투 후 영웅 진행(레벨/EXP)·dossier가 런간 갈렸다.");

        // (4) 게이트가 의미를 가지려면 실제로 싸웠어야 한다(빈 시퀀스 동일은 통과로 치지 않는다).
        Assert.That(first.BattleCount, Is.GreaterThan(0),
            "전투 노드가 0건 — 결정성 단언이 공허하지 않도록 최소 1전투를 요구.");
    }

    private static CampaignRunSnapshot RunHeadlessCampaign()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile { ProfileId = "headless_determinism" }); // 기본 분대 시드(hero-1..N)
        session.SetCurrentScene(SceneNames.Town);

        var runner = new CampaignPlaythroughRunner(
            session,
            new ScriptedPlaythroughPolicy(rewardIndex: 0),
            new RecordingNavSink(),
            PlaythroughBattleResolution.Simulate);
        var result = runner.Run();

        var outcomes = result.SiteObservations
            .SelectMany(site => site.BattleOutcomes ?? (IReadOnlyList<PlaythroughBattleOutcome>)Array.Empty<PlaythroughBattleOutcome>())
            .ToList();

        var outcomeSignature = string.Join(
            " | ",
            outcomes.Select(outcome => $"{outcome.NodeId}={(outcome.Victory ? "W" : "L")}({outcome.StepCount})"));

        var terminationSignature =
            $"cleared={result.StoryCleared};defeated={result.DefeatedSiteId ?? "(none)"};" +
            $"sites=[{string.Join(",", result.ClearedSiteIds)}]";

        // 영웅 진행을 HeroId ordinal 정렬해 직렬화 — 직렬화 순서가 결과를 흔들지 않도록(서명 자체는 결정적이어야).
        var progressionSignature = string.Join(
            ";",
            session.Profile.HeroProgressions
                .OrderBy(record => record.HeroId, StringComparer.Ordinal)
                .Select(record => $"{record.HeroId}:L{record.Level}/X{record.Experience}"))
            + " || dossier=" + session.Profile.Dossier.Count;

        return new CampaignRunSnapshot(outcomeSignature, terminationSignature, progressionSignature, outcomes.Count);
    }

    private readonly record struct CampaignRunSnapshot(
        string OutcomeSignature,
        string TerminationSignature,
        string ProgressionSignature,
        int BattleCount);
}
