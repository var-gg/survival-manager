using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Core;
using SM.Meta;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 캠페인 완주 골든 (Phase 2 #3) — "Unity는 껍데기, 게임플레이 truth는 순수 C#" 비전의 종착 실증.
/// 1-노드 골든(HeadlessPlaythroughGoldenFastTests)이 한 전투를 증명한다면, 이 골든은 **캠페인 그래프
/// (챕터→사이트→노드) 전체를 씬·VisualElement 없이 다회 순회해 엔딩(StoryCleared)까지** 끌고 간다.
/// = AI가 게임을 시작부터 엔딩까지 C#만으로 완주하는 것과 동등.
///
/// 검증된 메커니즘(RunLoopContractFastTests.ExtractSettlement 근거):
/// - 사이트 1개 = 단일 BeginNewExpedition 안에서 전투 노드 전부 + extract 노드 정산 → run 닫힘 → ClearedSiteIds += site.
/// - 사이트 내 노드 advance는 ResolveSelectedExpeditionNode(전투)/ReturnToTownAfterReward(extract)가 구동.
/// - 사이트→사이트, 챕터→챕터 전환은 자동이 아님 → run 닫힌 상태에서 TryCycleCampaignSite/Chapter 명시 호출.
/// - StoryCleared = 전 챕터의 전 사이트가 ClearedSiteIds에 들어가면 true(SessionExpeditionFlow.cs:1361-1362).
///
/// 책임 경계: 화면 presenter 읽기(Roster/Sortie/Warrant)는 1-노드 골든 책임 → 여기선 안 함. 캠페인 순회·종착만.
/// 무한모드(Atlas 파밍)는 비종착이라 범위 밖 — EndlessUnlocked 점화까지만 확인.
/// </summary>
[Category("FastUnit")]
public sealed class CampaignCompletionGoldenFastTests
{
    [Test]
    public void AiPlaythrough_TraversesEntireCampaignToStoryCleared_WithoutScene()
    {
        // --- 세션 + 4인 로스터 (씬 0) ---
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "campaign_completion_golden",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);

        var nav = new RecordingNavSink();
        var narration = new RecordingNarrativeSink();
        var clearedSitesInOrder = new List<string>();
        const int safety = 16;   // 무한루프 가드 (fixture 3사이트 상한)

        // === OUTER: 캠페인 그래프를 StoryCleared 켜질 때까지 사이트 단위로 순회 ===
        while (!session.Profile.CampaignProgress.StoryCleared && clearedSitesInOrder.Count < safety)
        {
            // 0) 다음 미클리어 좌표로 명시 전환 (run 닫힌 상태에서만 가능)
            Assert.That(session.CanChangeCampaignSelection, Is.True, "run이 닫혀 있어야 캠페인 선택 전환 가능.");
            AdvanceToNextUnclearedSite(session);
            var siteId = session.SelectedCampaignSiteId;
            Assert.That(session.Profile.CampaignProgress.ClearedSiteIds, Does.Not.Contain(siteId),
                "전환 후 선택 사이트는 아직 미클리어여야 한다(진행 단조성).");

            // 1) 사이트 진입: Town '원정' → Atlas
            nav.Go(ExpeditionFlowResolver.ResolveExpeditionEntry(session));
            session.BeginNewExpedition();
            DriveNarrative(session, narration, NarrativeMoment.SiteEntered);

            // 2) 전투 노드 전부 정산 (검증된 RunLoopContract 패턴)
            while (session.GetSelectedExpeditionNode()?.RequiresBattle == true)
            {
                nav.Go(ExpeditionFlowResolver.ResolveAtlasContinue(session)); // → Battle
                DriveNarrative(session, narration, NarrativeMoment.BattleStarted);
                Assert.That(session.ResolveSelectedExpeditionNode(), Is.True, "전투 노드 정산 + 다음 노드 advance.");
                DriveNarrative(session, narration, NarrativeMoment.BattleResolved);
            }

            // 3) extract 노드(마지막, RequiresBattle=false) 정산 → 보상 → 복귀
            var extractNode = session.GetSelectedExpeditionNode();
            Assert.That(extractNode, Is.Not.Null, "전투 노드 소진 후 extract 노드가 선택돼 있다.");
            Assert.That(extractNode!.RequiresBattle, Is.False, "마지막 노드는 extract.");
            Assert.That(extractNode.Id, Is.EqualTo($"{siteId}:extract"));

            nav.Go(ExpeditionFlowResolver.ResolveAtlasContinue(session)); // extract → Reward
            Assert.That(session.ResolveSelectedNodeToRewardSettlement(), Is.True, "extract → 보상 정산 핸드오프.");
            Assert.That(session.HasPendingRewardSettlement, Is.True);
            DriveNarrative(session, narration, NarrativeMoment.RewardOpened);
            Assert.That(session.ApplyRewardChoice(0), Is.True);
            DriveNarrative(session, narration, NarrativeMoment.RewardCommitted);
            session.ReturnToTownAfterReward();
            nav.Go(ExpeditionFlowResolver.AfterRewardSettled); // → Town

            // 4) 사이트 닫힘 — ClearedSiteIds에 박혔는지 확인
            Assert.That(session.HasActiveExpeditionRun, Is.False, "extract 정산 후 run 닫힘.");
            Assert.That(session.Profile.CampaignProgress.ClearedSiteIds, Does.Contain(siteId),
                "extract 정산이 사이트를 ClearedSiteIds에 기록.");
            clearedSitesInOrder.Add(siteId);
        }

        // === 종착: 캠페인 엔딩 ===
        Assert.That(session.Profile.CampaignProgress.StoryCleared, Is.True,
            "전 챕터·사이트 클리어 → 캠페인 엔딩(StoryCleared). AI가 시작부터 엔딩까지 C#으로 완주.");
        Assert.That(session.Profile.CampaignProgress.EndlessUnlocked, Is.True,
            "엔딩과 함께 무한모드 해금(EndlessUnlocked).");

        // fixture 캠페인 그래프 결정값 — 3사이트 2챕터 순서대로 클리어
        Assert.That(clearedSitesInOrder,
            Is.EqualTo(new[] { "site_alpha_gate", "site_alpha_depths", "site_beta_watch" }),
            "캠페인 순서대로 사이트를 거쳤다(alpha 2 → beta 1).");
        Assert.That(session.Profile.CampaignProgress.ClearedChapterIds,
            Is.EquivalentTo(new[] { "chapter_alpha", "chapter_beta" }),
            "두 챕터 모두 클리어 기록.");

        // narrative: 전 캠페인 루프에 끼어들었으나 씬·입력 없이 self-complete (seed 없는 baseline)
        Assert.That(narration.Presented, Is.Empty,
            "다회-루프 전체에서 narrative drain이 빈 큐를 무한대기 없이 통과 — 엔딩 컷씬 메커니즘은 CampaignEndingGateFastTests가 별도 증명.");

        // nav: 사이트 진입 횟수 = 거친 사이트 수 (정확 배열 대신 카운트 — fragile 회피)
        Assert.That(nav.Targets.Count(target => target == NavTarget.Atlas), Is.EqualTo(clearedSitesInOrder.Count),
            "사이트 진입(Atlas) 횟수 = 클리어 사이트 수.");
        Assert.That(nav.Targets.Count(target => target == NavTarget.Town), Is.EqualTo(clearedSitesInOrder.Count),
            "사이트 복귀(Town) 횟수 = 클리어 사이트 수.");
        Assert.That(nav.Targets.First(), Is.EqualTo(NavTarget.Atlas), "첫 전환은 사이트 진입.");

        // 무한모드 경계: 엔딩 후 run 닫힘 + 더 거칠 미클리어 사이트 없음(그래프 종착). 파밍 루프는 범위 밖.
        Assert.That(session.CanChangeCampaignSelection, Is.True, "엔딩 후 run 닫힌 상태.");
    }

    // 현재 선택 사이트가 이미 클리어면 다음 미클리어 좌표로 명시 전환(자동 advance 없음 — SessionExpeditionFlow 확인).
    // 같은 챕터에 미클리어 사이트가 남으면 site cycle, 챕터 사이트 소진(클리어로 wrap)이면 chapter cycle.
    private static void AdvanceToNextUnclearedSite(GameSessionState session)
    {
        var progress = session.Profile.CampaignProgress;
        if (!progress.ClearedSiteIds.Contains(session.SelectedCampaignSiteId))
        {
            return; // 현재 선택이 미클리어 — 그대로 진행(첫 진입 = site_alpha_gate).
        }

        session.TryCycleCampaignSite(+1);
        if (progress.ClearedSiteIds.Contains(session.SelectedCampaignSiteId))
        {
            // 사이트 cycle이 클리어된 곳으로 wrap(현 챕터 소진) → 다음 챕터 첫 사이트로.
            session.TryCycleCampaignChapter(+1);
        }
    }

    // bridge.TryPumpQueue + sink.Present의 순수 등가(1-노드 골든과 동일) — narrative moment 발화 후 큐를 sink로 drain.
    // baseline director(seed 없음)는 큐가 비어 즉시 종료, 입력 대기 0.
    private static void DriveNarrative(GameSessionState session, RecordingNarrativeSink sink, NarrativeMoment moment)
    {
        session.AdvanceNarrative(moment, StoryMomentContext.Empty);
        while (session.TryDequeueNarrativePresentation(out var request) && request != null)
        {
            sink.Present(request, () => { });
        }
    }

    private static HeroInstanceRecord CreateHero(string heroId, string classId)
    {
        return new HeroInstanceRecord
        {
            HeroId = heroId,
            Name = heroId,
            ArchetypeId = $"{classId}_archetype",
            RaceId = "human",
            ClassId = classId,
            PositiveTraitId = "trait_positive",
            NegativeTraitId = "trait_negative",
            EquippedItemIds = new List<string>(),
        };
    }
}
