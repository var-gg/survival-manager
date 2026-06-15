using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core;
using SM.Meta;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Atlas;
using SM.Unity.UI.Town.Preview;

namespace SM.Tests.EditMode;

/// <summary>
/// 헤드리스 플레이스루 관찰 리포트 — "AI가 씬 없이 게임을 플레이하며 화면을 읽는다"를 실제로 수행하고
/// 본 것을 사람이 읽을 리포트로 남긴다. 단언이 아니라 **관찰**이 목적: 각 화면의 player-facing ViewState를
/// 읽어 플레이 일지(Library/SM/Reports/headless-playthrough-report.md)로 기록한다.
/// 비전("Unity는 껍데기, 완성 게임과 동등한 C# 시뮬")을 내부에서 dogfood로 입증하는 산출물.
/// </summary>
[Category("FastUnit")]
public sealed class HeadlessPlaythroughObservabilityReport
{
    private const string ReportPath = "Library/SM/Reports/headless-playthrough-report.md";

    [Test]
    public void AiPlaysCampaignStartToEnding_AndWritesObservedReport()
    {
        var report = new StringBuilder();
        void Line(string text = "") => report.AppendLine(text);

        Line("# 헤드리스 플레이스루 — AI가 C#만으로 본 게임");
        Line();
        Line("씬·VisualElement 없이 세션 API + presenter BuildState로 게임을 처음부터 엔딩까지 플레이하며 관찰한 기록.");
        Line();

        // ── 부팅 & 로스터 ──
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "headless_playthrough_observability",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);

        Line("## 1. 부팅 & 로스터 (Town에서 내가 본 명부)");
        var roster = new RosterGridPresenter(
            session, lookup, new RecordingRosterGridView(),
            classId => classId, raceId => raceId,
            (c, a) => string.IsNullOrEmpty(c) ? a : c).BuildState();
        Line($"- 보유 동료: {roster.Heroes.Count}명 / 정원 {roster.RosterCap}");
        foreach (var hero in roster.Heroes)
        {
            Line($"  - {hero.DisplayName} · {hero.ArchetypeLabel} · Lv{hero.Level}");
        }
        Line($"- data-driven 필터 칩: {string.Join(", ", roster.Filters!.Select(f => $"{f.Key}({f.Count})"))}");
        Line();

        // ── 편성 & 출격 확인 ──
        DeployExactly(session,
            ("hero-1", DeploymentAnchorId.FrontTop),
            ("hero-2", DeploymentAnchorId.BackTop),
            ("hero-3", DeploymentAnchorId.FrontCenter),
            ("hero-4", DeploymentAnchorId.BackCenter));

        var catalog = new LaunchCoreRosterBaselineCatalog(lookup);
        var sortie = new SortieConfirmPresenter(session, catalog, id => id, id => id, anchor => anchor.ToString()).BuildState();
        Line("## 2. 편성 & 출격 확인 (내가 보낼 진형)");
        Line($"- 진형 슬롯: {sortie.DeploySlots.Count}칸 중 {sortie.DeployCount}칸 배치");
        Line($"- 출격 가능: {sortie.CanLaunch}");
        Line($"- 활성 시너지: {sortie.SynergyChips.Count(chip => chip.IsActive)}개 (전체 {sortie.SynergyChips.Count} 칩)");
        Line();

        // ── 캠페인 완주 ──
        var nav = new RecordingNavSink();
        var narration = new RecordingNarrativeSink();
        var clearedSites = new List<string>();
        var totalBattleNodes = 0;
        const int safety = 16;

        Line("## 3. 원정 — 캠페인 그래프를 엔딩까지 순회");
        while (!session.Profile.CampaignProgress.StoryCleared && clearedSites.Count < safety)
        {
            AdvanceToNextUnclearedSite(session);
            var chapterId = session.SelectedCampaignChapterId;
            var siteId = session.SelectedCampaignSiteId;

            nav.Go(ExpeditionFlowResolver.ResolveExpeditionEntry(session));
            session.BeginNewExpedition();
            DriveNarrative(session, narration, NarrativeMoment.SiteEntered);

            var battleNodes = new List<string>();
            while (session.GetSelectedExpeditionNode()?.RequiresBattle == true)
            {
                var node = session.GetSelectedExpeditionNode()!;
                battleNodes.Add(node.Id);
                nav.Go(ExpeditionFlowResolver.ResolveAtlasContinue(session));
                DriveNarrative(session, narration, NarrativeMoment.BattleStarted);
                session.ResolveSelectedExpeditionNode();
                DriveNarrative(session, narration, NarrativeMoment.BattleResolved);
            }
            totalBattleNodes += battleNodes.Count;

            var extract = session.GetSelectedExpeditionNode();
            nav.Go(ExpeditionFlowResolver.ResolveAtlasContinue(session));
            session.ResolveSelectedNodeToRewardSettlement();
            var rewardCount = session.PendingRewardChoices.Count;
            DriveNarrative(session, narration, NarrativeMoment.RewardOpened);
            session.ApplyRewardChoice(0);
            DriveNarrative(session, narration, NarrativeMoment.RewardCommitted);
            session.ReturnToTownAfterReward();
            nav.Go(ExpeditionFlowResolver.AfterRewardSettled);

            clearedSites.Add(siteId);
            Line($"### 사이트 #{clearedSites.Count}: {chapterId} / {siteId}");
            Line($"- 전투 노드 {battleNodes.Count}개 정산: {string.Join(" → ", battleNodes.Select(StripSitePrefix))}");
            Line($"- 추출 노드: {extract?.Id} (보상 {rewardCount}안 중 1번 선택)");
            Line($"- 캠페인 진행: 클리어 사이트 {session.Profile.CampaignProgress.ClearedSiteIds.Count} · 클리어 챕터 {session.Profile.CampaignProgress.ClearedChapterIds.Count}");
            Line();
        }

        // ── 엔딩 ──
        var progress = session.Profile.CampaignProgress;
        Line("## 4. 엔딩");
        Line($"- **StoryCleared: {progress.StoryCleared}** — 캠페인 완주(엔딩 도달)");
        Line($"- **EndlessUnlocked: {progress.EndlessUnlocked}** — 무한모드 해금");
        Line($"- 거친 사이트: {string.Join(", ", clearedSites)}");
        Line($"- 클리어 챕터: {string.Join(", ", progress.ClearedChapterIds)}");
        Line();

        Line("## 5. 플레이 메타 (씬 0으로 도달)");
        Line($"- 총 전투 노드: {totalBattleNodes}");
        Line($"- 화면 전환(nav): Atlas {Count(nav, NavTarget.Atlas)} · Battle {Count(nav, NavTarget.Battle)} · Reward {Count(nav, NavTarget.Reward)} · Town {Count(nav, NavTarget.Town)}");
        Line($"- narrative drain: 연출 dispatch {narration.Presented.Count}건 (seed 없는 baseline=0, 무한대기 0)");
        Line($"- 전체를 VisualElement·SceneManager·MonoBehaviour 컨트롤러 없이 순수 C#으로 구동.");

        // ── 파일로 기록 + 최소 단언(관찰이 실제로 끝까지 갔는가) ──
        var fullPath = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, report.ToString());
        TestContext.WriteLine(report.ToString());

        Assert.That(progress.StoryCleared, Is.True, "관찰 플레이스루가 엔딩까지 도달.");
        Assert.That(clearedSites, Is.EqualTo(new[] { "site_alpha_gate", "site_alpha_depths", "site_beta_watch" }));
    }

    private static string StripSitePrefix(string nodeId)
    {
        var idx = nodeId.IndexOf(':');
        return idx >= 0 ? nodeId[(idx + 1)..] : nodeId;
    }

    private static int Count(RecordingNavSink nav, NavTarget target) => nav.Targets.Count(t => t == target);

    private static void AdvanceToNextUnclearedSite(GameSessionState session)
    {
        var progress = session.Profile.CampaignProgress;
        if (!progress.ClearedSiteIds.Contains(session.SelectedCampaignSiteId))
        {
            return;
        }

        session.TryCycleCampaignSite(+1);
        if (progress.ClearedSiteIds.Contains(session.SelectedCampaignSiteId))
        {
            session.TryCycleCampaignChapter(+1);
        }
    }

    private static void DriveNarrative(GameSessionState session, RecordingNarrativeSink sink, NarrativeMoment moment)
    {
        session.AdvanceNarrative(moment, StoryMomentContext.Empty);
        while (session.TryDequeueNarrativePresentation(out var request) && request != null)
        {
            sink.Present(request, () => { });
        }
    }

    private static void DeployExactly(GameSessionState session, params (string HeroId, DeploymentAnchorId Anchor)[] placements)
    {
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        foreach (var (heroId, anchor) in placements)
        {
            session.AssignHeroToAnchor(anchor, heroId);
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
