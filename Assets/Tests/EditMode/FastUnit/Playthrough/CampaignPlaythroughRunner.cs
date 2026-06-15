using System.Collections.Generic;
using System.Linq;
using SM.Unity;

namespace SM.Tests.EditMode.Playthrough;

/// <summary>
/// 헤드리스 캠페인 구동기 — CampaignCompletionGoldenFastTests의 인라인 루프를 추출해
/// 결정 지점마다 <see cref="IPlaythroughDecisionPolicy"/>에 위임한다. 같은 runner를
/// 스크립트 정책(골든)이든 실제 LLM 정책이든 공유하므로, 게임 구동 로직은 한 곳에 모이고
/// "무엇을 고를지"만 policy로 갈린다.
///
/// 검증된 메커니즘(RunLoopContract/CampaignCompletionGolden 근거):
/// - 사이트 1개 = 단일 BeginNewExpedition 안에서 전투 노드 전부 + extract 정산 → run 닫힘 → ClearedSiteIds += site.
/// - 사이트→사이트, 챕터→챕터 전환은 자동이 아님 → TryCycleCampaignSite/Chapter 명시 호출(AdvanceToNextUnclearedSite).
/// - StoryCleared = 전 챕터의 전 사이트가 ClearedSiteIds에 들어가면 true.
///
/// 진형은 캠페인 시작 시 한 번 결정해 유지한다(사이트별 재배치는 후속 — 검증된 단일-배치 경로 보존).
/// narrative drain은 결정이 아니라 presentation seam이므로 여기서 다루지 않는다(CampaignEndingGate가 별도 증명).
/// </summary>
public sealed class CampaignPlaythroughRunner
{
    private readonly GameSessionState _session;
    private readonly IPlaythroughDecisionPolicy _policy;
    private readonly INavSink _nav;

    public CampaignPlaythroughRunner(GameSessionState session, IPlaythroughDecisionPolicy policy, INavSink nav)
    {
        _session = session;
        _policy = policy;
        _nav = nav;
    }

    /// <summary>캠페인을 엔딩(StoryCleared)까지 구동하고 관찰값을 반환한다.</summary>
    /// <param name="safety">무한루프 가드(거친 사이트 상한).</param>
    public CampaignPlaythroughResult Run(int safety = 16)
    {
        // 1) 출격 진형 — policy가 한 번 결정 (캠페인 내내 유지).
        ApplyDeployment();

        var clearedSites = new List<string>();
        var siteObservations = new List<SitePlaythroughObservation>();
        var totalBattleNodes = 0;

        // 2) 캠페인 그래프를 StoryCleared 켜질 때까지 사이트 단위로 순회.
        while (!_session.Profile.CampaignProgress.StoryCleared && clearedSites.Count < safety)
        {
            AdvanceToNextUnclearedSite();
            var chapterId = _session.SelectedCampaignChapterId;
            var siteId = _session.SelectedCampaignSiteId;

            _nav.Go(ExpeditionFlowResolver.ResolveExpeditionEntry(_session));
            _session.BeginNewExpedition();

            // 2a) 전투 노드 전부 자동 정산(승패는 결정 아님 — fixture 자동 승리).
            var battleNodeIds = new List<string>();
            while (_session.GetSelectedExpeditionNode()?.RequiresBattle == true)
            {
                var node = _session.GetSelectedExpeditionNode()!;
                battleNodeIds.Add(node.Id);
                _nav.Go(ExpeditionFlowResolver.ResolveAtlasContinue(_session)); // → Battle
                _session.ResolveSelectedExpeditionNode();
            }
            totalBattleNodes += battleNodeIds.Count;

            // 2b) extract 노드 정산 → 보상 제시 → policy 결정 → 복귀.
            var extractNode = _session.GetSelectedExpeditionNode();
            _nav.Go(ExpeditionFlowResolver.ResolveAtlasContinue(_session)); // extract → Reward
            _session.ResolveSelectedNodeToRewardSettlement();

            var rewardView = BuildRewardView(chapterId, siteId);
            var chosenRewardIndex = _policy.DecideReward(rewardView);
            _session.ApplyRewardChoice(chosenRewardIndex);
            _session.ReturnToTownAfterReward();
            _nav.Go(ExpeditionFlowResolver.AfterRewardSettled); // → Town

            clearedSites.Add(siteId);
            siteObservations.Add(new SitePlaythroughObservation(
                ChapterId: chapterId,
                SiteId: siteId,
                BattleNodeIds: battleNodeIds,
                ExtractNodeId: extractNode?.Id ?? string.Empty,
                RewardOptionCount: rewardView.Options.Count,
                ChosenRewardIndex: chosenRewardIndex));
        }

        var progress = _session.Profile.CampaignProgress;
        return new CampaignPlaythroughResult(
            StoryCleared: progress.StoryCleared,
            EndlessUnlocked: progress.EndlessUnlocked,
            ClearedSiteIds: clearedSites,
            ClearedChapterIds: progress.ClearedChapterIds.ToList(),
            SiteObservations: siteObservations,
            TotalBattleNodes: totalBattleNodes);
    }

    private void ApplyDeployment()
    {
        // 앵커 초기화 후 policy 결정 적용.
        foreach (var anchor in _session.DeploymentAnchors)
        {
            _session.AssignHeroToAnchor(anchor, null);
        }

        var view = new PlaythroughDeploymentView(
            _session.Profile.Heroes
                .Select(hero => new PlaythroughHero(hero.HeroId, hero.ClassId, hero.ArchetypeId, ResolveLevel(hero.HeroId)))
                .ToList(),
            _session.DeploymentAnchors.ToList());

        foreach (var placement in _policy.DecideDeployment(view))
        {
            _session.AssignHeroToAnchor(placement.Anchor, placement.HeroId);
        }
    }

    private int ResolveLevel(string heroId)
    {
        var progression = _session.Profile.HeroProgressions
            .FirstOrDefault(record => string.Equals(record.HeroId, heroId, System.StringComparison.Ordinal));
        return progression?.Level ?? 1;
    }

    private PlaythroughRewardView BuildRewardView(string chapterId, string siteId)
    {
        var options = _session.PendingRewardChoices
            .Select((choice, index) => new PlaythroughRewardOption(
                index, choice.Kind, choice.PayloadId, choice.GoldAmount, choice.EchoAmount))
            .ToList();
        return new PlaythroughRewardView(chapterId, siteId, options);
    }

    // 현재 선택 사이트가 이미 클리어면 다음 미클리어 좌표로 명시 전환(자동 advance 없음).
    // 같은 챕터에 미클리어 사이트가 남으면 site cycle, 챕터 소진(클리어로 wrap)이면 chapter cycle.
    private void AdvanceToNextUnclearedSite()
    {
        var progress = _session.Profile.CampaignProgress;
        if (!progress.ClearedSiteIds.Contains(_session.SelectedCampaignSiteId))
        {
            return;
        }

        _session.TryCycleCampaignSite(+1);
        if (progress.ClearedSiteIds.Contains(_session.SelectedCampaignSiteId))
        {
            _session.TryCycleCampaignChapter(+1);
        }
    }
}

/// <summary>플레이스루 종착 관찰값 — 골든이 단언하고 리포트 하네스가 읽는 결과 묶음.</summary>
public sealed record CampaignPlaythroughResult(
    bool StoryCleared,
    bool EndlessUnlocked,
    IReadOnlyList<string> ClearedSiteIds,
    IReadOnlyList<string> ClearedChapterIds,
    IReadOnlyList<SitePlaythroughObservation> SiteObservations,
    int TotalBattleNodes);

/// <summary>사이트 한 곳을 거치며 관찰한 내역.</summary>
public sealed record SitePlaythroughObservation(
    string ChapterId,
    string SiteId,
    IReadOnlyList<string> BattleNodeIds,
    string ExtractNodeId,
    int RewardOptionCount,
    int ChosenRewardIndex);
