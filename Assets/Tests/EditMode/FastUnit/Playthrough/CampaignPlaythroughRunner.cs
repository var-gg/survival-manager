using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
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
    private readonly PlaythroughBattleResolution _battleResolution;

    public CampaignPlaythroughRunner(
        GameSessionState session,
        IPlaythroughDecisionPolicy policy,
        INavSink nav,
        PlaythroughBattleResolution battleResolution = PlaythroughBattleResolution.AutoResolve)
    {
        _session = session;
        _policy = policy;
        _nav = nav;
        _battleResolution = battleResolution;
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
        string? defeatedSiteId = null;

        // 2) 캠페인 그래프를 StoryCleared 켜질 때까지 사이트 단위로 순회.
        while (!_session.Profile.CampaignProgress.StoryCleared && clearedSites.Count < safety)
        {
            AdvanceToNextUnclearedSite();

            _nav.Go(ExpeditionFlowResolver.ResolveExpeditionEntry(_session));
            _session.BeginNewExpedition();
            var observation = PlaySelectedSiteToSettlement();

            totalBattleNodes += observation.BattleNodeIds.Count;
            siteObservations.Add(observation);

            if (observation.ChosenRewardIndex < 0)
            {
                // 패배 — 원정 중단, 사이트 미클리어. 캠페인 루프도 종료.
                defeatedSiteId = observation.SiteId;
                break;
            }

            clearedSites.Add(observation.SiteId);
        }

        var progress = _session.Profile.CampaignProgress;
        return new CampaignPlaythroughResult(
            StoryCleared: progress.StoryCleared,
            EndlessUnlocked: progress.EndlessUnlocked,
            ClearedSiteIds: clearedSites,
            ClearedChapterIds: progress.ClearedChapterIds.ToList(),
            SiteObservations: siteObservations,
            TotalBattleNodes: totalBattleNodes,
            DefeatedSiteId: defeatedSiteId);
    }

    /// <summary>
    /// 엔딩 후 무한 순환을 <paramref name="cycles"/>회 구동한다 — 실게임과 같은 세션 API
    /// (BeginEndlessExpedition)를 타므로 사이클 truth 전이·발화·시드 분화가 골든과 프로덕션에서 동일하다.
    /// V1 라우팅: 캠페인 종료 시점의 선택 사이트를 회차마다 재주행(사이트 로테이션은 후속).
    /// 전제: Run()이 StoryCleared/EndlessUnlocked까지 완주한 뒤 호출.
    /// </summary>
    public EndlessPlaythroughResult RunEndlessCycles(int cycles)
    {
        if (!_session.Profile.CampaignProgress.EndlessUnlocked)
        {
            throw new InvalidOperationException("무한 순환은 EndlessUnlocked 이후에만 구동할 수 있다.");
        }

        var cycleObservations = new List<EndlessCycleObservation>();
        for (var i = 0; i < cycles; i++)
        {
            _nav.Go(ExpeditionFlowResolver.ResolveExpeditionEntry(_session));
            _session.BeginEndlessExpedition();

            var cycleIndex = _session.ActiveRun?.EndlessCycleIndex ?? 0;
            var heat = _session.Profile.Narrative.EndlessCycle.Heat;
            // RunId는 StartRun이 생성하는 GUID(회차 무관 항상 유일) — 회차 identity는 ExpeditionId 접미(#cN)가 운반.
            var expeditionId = _session.ActiveRun?.ExpeditionId ?? string.Empty;
            var echoBefore = _session.Profile.Currencies.Echo;
            var goldBefore = _session.Profile.Currencies.Gold;

            var observation = PlaySelectedSiteToSettlement();

            // 회차별 분화 증거: 정산이 남긴 reward_choice ledger의 CommitId(= cycle-salt 들어간 contextHash 파생).
            var lastCommitId = _session.Profile.RewardLedger
                .LastOrDefault(entry => entry.SourceKind.EndsWith(":reward_choice", StringComparison.Ordinal))
                ?.CommitId ?? string.Empty;

            cycleObservations.Add(new EndlessCycleObservation(
                CycleIndex: cycleIndex,
                Heat: heat,
                ExpeditionId: expeditionId,
                RewardCommitId: lastCommitId,
                Site: observation,
                EchoDelta: _session.Profile.Currencies.Echo - echoBefore,
                GoldDelta: _session.Profile.Currencies.Gold - goldBefore));

            if (observation.ChosenRewardIndex < 0)
            {
                break; // 패배 — 순환 중단(관찰값은 남긴다).
            }
        }

        var narrative = _session.Profile.Narrative.EndlessCycle;
        return new EndlessPlaythroughResult(
            PersistedCycleIndex: narrative.CycleIndex,
            PersistedHeat: narrative.Heat,
            Cycles: cycleObservations);
    }

    /// <summary>
    /// 현재 선택 사이트를 전투 노드 전부 → extract 정산 → 보상 선택 → Town 복귀까지 플레이한다.
    /// 패배 시 ChosenRewardIndex = -1 관찰값을 반환(run은 Abandon 처리).
    /// Run()과 RunEndlessCycles()가 공유하는 사이트 1회 몸통 — 캠페인 골든이 검증한 경로 그대로.
    /// </summary>
    private SitePlaythroughObservation PlaySelectedSiteToSettlement()
    {
        var chapterId = _session.SelectedCampaignChapterId;
        var siteId = _session.SelectedCampaignSiteId;

        // 전투 노드 순회. AutoResolve = fixture 메타루프 증명(sim 0, 자동 승리 단축).
        // Simulate = 실 BattleSimulator 완주 → 헤드리스가 진짜로 싸운다. 승리만 노드 전진,
        // 패배는 사이트 미클리어로 원정 중단(골든은 승리 전제이므로 패배가 표면화됨).
        var battleNodeIds = new List<string>();
        var battleOutcomes = new List<PlaythroughBattleOutcome>();
        while (true)
        {
            while (TryAdvanceIntermediateNonBattle())
            {
            }

            if (_session.GetSelectedExpeditionNode()?.RequiresBattle != true)
            {
                break;
            }

            var node = _session.GetSelectedExpeditionNode()!;
            battleNodeIds.Add(node.Id);
            _nav.Go(ExpeditionFlowResolver.ResolveAtlasContinue(_session)); // → Battle

            if (_battleResolution == PlaythroughBattleResolution.Simulate)
            {
                if (!_session.TryResolveSelectedBattleNodeViaSimulation(out var battleResult, out var error))
                {
                    throw new InvalidOperationException($"전투 sim 실패({node.Id}): {error}");
                }

                var victory = battleResult.Winner == TeamSide.Ally;
                battleOutcomes.Add(new PlaythroughBattleOutcome(node.Id, victory, battleResult.StepCount));
                if (!victory)
                {
                    _session.AbandonExpeditionRun();
                    return new SitePlaythroughObservation(
                        ChapterId: chapterId,
                        SiteId: siteId,
                        BattleNodeIds: battleNodeIds,
                        BattleOutcomes: battleOutcomes,
                        ExtractNodeId: string.Empty,
                        RewardOptionCount: 0,
                        ChosenRewardIndex: -1);
                }
            }

            _session.ResolveSelectedExpeditionNode(); // 노드 커서 전진 + 노드 효과(전리품).
        }

        // extract 노드 정산 → 보상 제시 → policy 결정 → 복귀.
        var extractNode = _session.GetSelectedExpeditionNode();
        _nav.Go(ExpeditionFlowResolver.ResolveAtlasContinue(_session)); // extract → Reward
        _session.ResolveSelectedNodeToRewardSettlement();

        var rewardView = BuildRewardView(chapterId, siteId);
        var chosenRewardIndex = _policy.DecideReward(rewardView);
        // 정산 직전 run 상태 관찰 — 무한 골든이 dedup 분기 입력(사이클 스탬프·CommitId)을 단언한다.
        var cycleAtSettlement = _session.ActiveRun?.EndlessCycleIndex ?? 0;
        var commitIdAtSettlement = _session.ActiveRun?.Overlay.RewardCommitId ?? string.Empty;
        var ledgerBeforeChoice = _session.Profile.RewardLedger.Count;
        var chosenKind = rewardView.Options[chosenRewardIndex].Kind.ToString();
        _session.ApplyRewardChoice(chosenRewardIndex);
        var ledgerDelta = _session.Profile.RewardLedger.Count - ledgerBeforeChoice;
        _session.ReturnToTownAfterReward();
        _nav.Go(ExpeditionFlowResolver.AfterRewardSettled); // → Town

        return new SitePlaythroughObservation(
            ChapterId: chapterId,
            SiteId: siteId,
            BattleNodeIds: battleNodeIds,
            BattleOutcomes: battleOutcomes,
            ExtractNodeId: extractNode?.Id ?? string.Empty,
            RewardOptionCount: rewardView.Options.Count,
            ChosenRewardIndex: chosenRewardIndex,
            EndlessCycleIndexAtSettlement: cycleAtSettlement,
            RewardCommitIdAtSettlement: commitIdAtSettlement,
            ChosenRewardKind: chosenKind,
            RewardLedgerDelta: ledgerDelta);
    }

    // SM.Tests.FastUnit cannot reference the editor-only CampaignDefaultRouteNavigator.
    // Keep the same first-edge/first-legal-choice semantics locally so real-content and
    // editor-free consumers share the safe canonical route without widening asmdef edges.
    private bool TryAdvanceIntermediateNonBattle()
    {
        var node = _session.GetSelectedExpeditionNode();
        if (node == null || node.RequiresBattle || node.NextNodeIndices.Count == 0)
        {
            return false;
        }

        if (!_session.ResolveSelectedNodeToRewardSettlement())
        {
            throw new InvalidOperationException($"Failed to resolve default non-battle node '{node.GraphNodeId}'.");
        }

        if (_session.PendingSiteEvent != null)
        {
            var defaultAction = _session.SiteEvents.GetLegalActions().FirstOrDefault();
            if (defaultAction == null || !_session.SiteEvents.ApplyChoice(defaultAction.ChoiceId))
            {
                throw new InvalidOperationException($"Failed to apply the default choice for site event '{node.EventId}'.");
            }
        }

        _session.ReturnToTownAfterReward();
        return true;
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

/// <summary>
/// 전투 노드 해석 방식 — 같은 runner를 두 소비자가 공유하는 seam.
/// </summary>
public enum PlaythroughBattleResolution
{
    /// <summary>auto-resolve 단축. fixture(실 전투 콘텐츠 없음)로 메타루프 완주만 증명 — sim 0, 자동 승리.</summary>
    AutoResolve,

    /// <summary>실 BattleSimulator 완주. 헤드리스가 진짜로 싸운다(RuntimeCombatContentLookup 등 실 콘텐츠 필요).</summary>
    Simulate,
}

/// <summary>플레이스루 종착 관찰값 — 골든이 단언하고 리포트 하네스가 읽는 결과 묶음.</summary>
public sealed record CampaignPlaythroughResult(
    bool StoryCleared,
    bool EndlessUnlocked,
    IReadOnlyList<string> ClearedSiteIds,
    IReadOnlyList<string> ClearedChapterIds,
    IReadOnlyList<SitePlaythroughObservation> SiteObservations,
    int TotalBattleNodes,
    string? DefeatedSiteId = null);

/// <summary>사이트 한 곳을 거치며 관찰한 내역. Simulate 모드에서만 BattleOutcomes가 채워진다.</summary>
public sealed record SitePlaythroughObservation(
    string ChapterId,
    string SiteId,
    IReadOnlyList<string> BattleNodeIds,
    string ExtractNodeId,
    int RewardOptionCount,
    int ChosenRewardIndex,
    IReadOnlyList<PlaythroughBattleOutcome>? BattleOutcomes = null,
    int EndlessCycleIndexAtSettlement = 0,
    string RewardCommitIdAtSettlement = "",
    string ChosenRewardKind = "",
    int RewardLedgerDelta = 0);

/// <summary>실 sim으로 정산한 전투 노드 결과 — 헤드리스가 실제로 tick을 돌렸음을 증명하는 관찰값.</summary>
public sealed record PlaythroughBattleOutcome(
    string NodeId,
    bool Victory,
    int StepCount);

/// <summary>무한 순환 구동 종착 관찰값 — 영속된 사이클 truth와 회차별 내역.</summary>
public sealed record EndlessPlaythroughResult(
    int PersistedCycleIndex,
    int PersistedHeat,
    IReadOnlyList<EndlessCycleObservation> Cycles);

/// <summary>무한 순환 1회차 관찰값 — 원정 identity/CommitId/보상 증감이 회차별로 분화됨을 골든이 단언한다.</summary>
public sealed record EndlessCycleObservation(
    int CycleIndex,
    int Heat,
    string ExpeditionId,
    string RewardCommitId,
    SitePlaythroughObservation Site,
    int EchoDelta,
    int GoldDelta);
