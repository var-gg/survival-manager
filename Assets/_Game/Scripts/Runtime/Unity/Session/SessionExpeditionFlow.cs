using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core;
using SM.Core.Contracts;
using SM.Core.Results;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity.Narrative;
using SM.Unity.Sandbox;
using Unity.Profiling;

namespace SM.Unity;

public sealed partial class GameSessionState
{
    internal sealed class SessionExpeditionFlow
    {
        private readonly GameSessionState _session;

        internal SessionExpeditionFlow(GameSessionState session)
        {
            _session = session;
        }

        internal void BeginNewExpedition()
        {
            BeginExpeditionCore(endlessCycleIndex: 0);

            // 발화는 세션: 원정 시작(=사이트 진입) moment를 여기서 발화한다 — 씬 AtlasScreenController가 아니라
            // 세션이 단일 소스라 헤드리스 드라이버와 실게임이 같은 발화를 공유(표시는 씬이 bridge.PresentPending으로).
            _session.AdvanceNarrative(NarrativeMoment.SiteEntered, NarrativeMomentResolver.BuildNodeContext(_session));
        }

        /// <summary>
        /// 무한 순환 원정 시작 — 사이클 truth 전이(director Progress가 단일 owner, 저장은 Advance가 미러)
        /// 후 기존 원정 기계를 그대로 재사용하고 run에 사이클을 스탬프한다. 시드/보상 dedup/Atlas traversal은
        /// ActiveRun.EndlessCycleIndex로 분기. 발화 순서: 사이클 시작 → 사이트 진입.
        /// </summary>
        internal void BeginEndlessExpedition()
        {
            var nextCycle = EndlessCycleService.BeginNextCycle(_session.StoryDirector.Progress.EndlessCycle);
            _session.StoryDirector.SetEndlessCycle(nextCycle);

            BeginExpeditionCore(nextCycle.CycleIndex);

            _session.AdvanceNarrative(NarrativeMoment.EndlessCycleStarted, NarrativeMomentResolver.BuildNodeContext(_session));
            _session.AdvanceNarrative(NarrativeMoment.SiteEntered, NarrativeMomentResolver.BuildNodeContext(_session));
        }

        private void BeginExpeditionCore(int endlessCycleIndex)
        {
            ResetExpeditionRunState();
            PrepareExpeditionTrack();
            StartExpeditionRun(endlessCycleIndex);
            _session.SyncActiveRunRecord();
            _session.SyncExpeditionState();
        }

        private void ResetExpeditionRunState()
        {
            _session.IsQuickBattleSmokeActive = false;
            _session.QuickBattleLaneKind = CombatSandboxLaneKind.None;
            _session.HasActiveExpeditionRun = true;
            _session.CurrentExpeditionNodeIndex = 0;
            _session.SelectedExpeditionNodeIndex = null;
            _session.LastBattleVictory = false;
            _session.LastBattleSummary = SessionTextToken.Empty;
            _session.LastExpeditionEffectMessage = SessionTextToken.Empty;
            _session.LastRewardApplicationSummary = SessionTextToken.Empty;
            _session.LastPermanentUnlockSummary = SessionTextToken.Empty;
            _session._lastAutomaticLootBundle = null;
            _session._hasPendingRewardSettlement = false;
            _session._quickBattleSeedOverride = null;
            _session._compiledQuickBattleScenario = null;
            _session.ResetAtlasSession();
            _session._runtimeTelemetryEvents.Clear();
            _session._resolvedExpeditionNodeIds.Clear();
            _session._siteEventFlow.ResetRunState();
        }

        private void PrepareExpeditionTrack()
        {
            _session.EnsureBattleDeployReady();
            _session.EnsureCampaignSelection();
            _session.EnsureExpeditionNodes(reset: true);
            _session.CurrentExpeditionNodeIndex = _session.ResolveExpeditionEntryNodeIndex();
            _session.AutoSelectNextExpeditionNode();
            _session.EnsureRewardChoices(reset: true);
        }

        private void StartExpeditionRun(int endlessCycleIndex)
        {
            var runId = _session.GetExpeditionRunId();
            if (endlessCycleIndex > 0)
            {
                // 사이클별 run identity 분리 — ledger/telemetry가 회차 간 충돌하지 않게 한다.
                runId = $"{runId}#c{endlessCycleIndex}";
            }

            var run = RunStateService.StartRun(runId, _session.CaptureBlueprintState(), false)
                with { EndlessCycleIndex = endlessCycleIndex };
            _session.ActiveRun = _session._campaignRecoveryFlow.InitializeRun(run, endlessCycleIndex);
        }

        internal void PrepareQuickBattleSmoke()
        {
            var config = LoadCombatSandboxConfig(required: false);
            PrepareQuickBattleSmoke(config, CombatSandboxLaneKind.TownIntegrationSmoke);
        }

        internal void PrepareQuickBattleSmoke(CombatSandboxConfig? quickBattleConfig)
        {
            PrepareQuickBattleSmoke(quickBattleConfig, CombatSandboxLaneKind.TownIntegrationSmoke);
        }

        internal void PrepareQuickBattleSmoke(
            CombatSandboxConfig? quickBattleConfig,
            CombatSandboxLaneKind laneKind,
            bool resetSeedOverride = true)
        {
            if (!_session.CanStartQuickBattleSmoke && !_session.IsQuickBattleSmokeActive)
            {
                return;
            }

            _session.IsQuickBattleSmokeActive = true;
            _session.HasActiveExpeditionRun = false;
            _session.LastBattleVictory = false;
            _session.LastBattleSummary = SessionTextToken.Empty;
            _session.LastExpeditionEffectMessage = SessionTextToken.Empty;
            _session.LastRewardApplicationSummary = SessionTextToken.Empty;
            _session.LastPermanentUnlockSummary = SessionTextToken.Empty;
            _session._lastAutomaticLootBundle = null;
            _session._hasPendingRewardSettlement = false;
            _session._runtimeTelemetryEvents.Clear();
            _session._pendingRewardChoices.Clear();
            _session._siteEventFlow.ResetRunState();
            if (resetSeedOverride)
            {
                _session._quickBattleSeedOverride = null;
            }

            _session._compiledQuickBattleScenario = null;
            _session.ResetAtlasSession();
            _session.QuickBattleConfig = quickBattleConfig;
            _session.QuickBattleLaneKind = laneKind;
            _session.EnsureBattleDeployReady();
            _session.EnsureRewardChoices(reset: true);
            _session.ActiveRun = RunStateService.StartRun("quick-battle", _session.CaptureBlueprintState(), true);
            _session.SyncActiveRunRecord();
            _session.SyncExpeditionState();
        }

        internal void PrepareCombatSandboxDirect()
        {
            // 전투테스트 레인 — config 필수(로드 실패 = 에러 + GameSessionRoot 차단).
            var config = LoadCombatSandboxConfig(required: true);
            PrepareQuickBattleSmoke(config, CombatSandboxLaneKind.DirectCombatSandbox);
        }

        internal void PrepareTownQuickBattleSmoke()
        {
            var config = LoadCombatSandboxConfig(required: false);
            PrepareQuickBattleSmoke(config, CombatSandboxLaneKind.TownIntegrationSmoke);
        }

        internal void RestartQuickBattle(bool advanceSeed)
        {
            ReloadCombatSandboxConfig();
            if (advanceSeed)
            {
                _session._quickBattleSeedOverride = (_session._quickBattleSeedOverride ?? ResolveConfiguredQuickBattleSeed(_session.QuickBattleConfig)) + 1;
            }

            PrepareQuickBattleSmoke(
                _session.QuickBattleConfig,
                _session.QuickBattleLaneKind == CombatSandboxLaneKind.None
                    ? ResolveDefaultQuickBattleLane(_session.QuickBattleConfig)
                    : _session.QuickBattleLaneKind,
                resetSeedOverride: !advanceSeed);
        }

        internal void ExitCombatSandbox()
        {
            _session.IsQuickBattleSmokeActive = false;
            _session.QuickBattleLaneKind = CombatSandboxLaneKind.None;
            _session.HasActiveExpeditionRun = false;
            _session._hasPendingRewardSettlement = false;
            _session._pendingRewardChoices.Clear();
            _session._quickBattleSeedOverride = null;
            _session._compiledQuickBattleScenario = null;
            _session.ResetAtlasSession();
            _session._siteEventFlow.ResetRunState();
            _session.ActiveRun = null;
            _session.SyncActiveRunRecord();
            _session.SyncExpeditionState();
        }

        internal bool TryCycleCampaignChapter(int direction)
        {
            if (!_session.CanChangeCampaignSelection)
            {
                return false;
            }

            var chapterIds = _session.GetOrderedCampaignChapterIds();
            if (chapterIds.Count <= 1)
            {
                return false;
            }

            _session.EnsureCampaignSelection();
            var currentIndex = Math.Max(0, chapterIds.FindIndex(id => string.Equals(id, _session.Profile.CampaignProgress.SelectedChapterId, StringComparison.Ordinal)));
            var nextIndex = WrapIndex(currentIndex + Math.Sign(direction), chapterIds.Count);
            if (nextIndex == currentIndex)
            {
                return false;
            }

            _session.Profile.CampaignProgress.SelectedChapterId = chapterIds[nextIndex];
            var siteIds = _session.GetOrderedSiteIdsForChapter(_session.Profile.CampaignProgress.SelectedChapterId);
            _session.Profile.CampaignProgress.SelectedSiteId = siteIds.FirstOrDefault() ?? string.Empty;
            _session.ResetExpeditionTrackForCampaignSelection();
            return true;
        }

        internal bool TryCycleCampaignSite(int direction)
        {
            if (!_session.CanChangeCampaignSelection)
            {
                return false;
            }

            _session.EnsureCampaignSelection();
            var siteIds = _session.GetOrderedSiteIdsForChapter(_session.Profile.CampaignProgress.SelectedChapterId);
            if (siteIds.Count <= 1)
            {
                return false;
            }

            var currentIndex = Math.Max(0, siteIds.FindIndex(id => string.Equals(id, _session.Profile.CampaignProgress.SelectedSiteId, StringComparison.Ordinal)));
            var nextIndex = WrapIndex(currentIndex + Math.Sign(direction), siteIds.Count);
            if (nextIndex == currentIndex)
            {
                return false;
            }

            _session.Profile.CampaignProgress.SelectedSiteId = siteIds[nextIndex];
            _session.ResetExpeditionTrackForCampaignSelection();
            return true;
        }

        internal bool PrepareSelectedBattleNodeHandoff()
        {
            var selected = GetSelectedExpeditionNode();
            if (selected == null || !selected.RequiresBattle)
            {
                return false;
            }

            _session.CurrentExpeditionNodeIndex = selected.Index;
            _session.SelectedExpeditionNodeIndex = selected.Index;
            _session.EnsureCampaignSelection();
            _session.EnsureActiveRunNodeState(selected);
            _session.RestoreResolvedProgressMarkers(includeCurrentNode: false);
            _session.SyncActiveRunRecord();
            _session.SyncExpeditionState();
            return true;
        }

        internal bool ResolveSelectedNodeToRewardSettlement()
        {
            var selected = GetSelectedExpeditionNode();
            if (selected == null || selected.RequiresBattle)
            {
                return false;
            }

            if (selected.NodeKind == SiteNodeKindValue.Event)
            {
                return _session._siteEventFlow.TryPresent(selected);
            }

            _session.CurrentExpeditionNodeIndex = selected.Index;
            _session.SelectedExpeditionNodeIndex = selected.Index;
            _session.EnsureCampaignSelection();
            _session.EnsureActiveRunNodeState(selected);
            _session.MarkNodeResolved(selected);
            _session.LastBattleVictory = true;
            _session.LastBattleSummary = _session.BuildNodeSettlementSummaryToken(selected);
            _session.LastExpeditionEffectMessage = _session.ApplyExpeditionNodeEffect(selected);
            _session._siteEventFlow.ConsumeExtractBonusIfNeeded(selected);
            _session.LastRewardApplicationSummary = SessionTextToken.Empty;
            _session.LastPermanentUnlockSummary = SessionTextToken.Empty;
            _session._lastAutomaticLootBundle = null;
            _session._hasPendingRewardSettlement = true;
            _session.UpdateCampaignProgressForResolvedNode(selected);
            if (_session.ActiveRun != null)
            {
                _session.ActiveRun = _session.ActiveRun with { LastSettlementWasVictory = true };
            }

            _session.RestoreResolvedProgressMarkers(includeCurrentNode: true);
            _session.EnsureRewardChoices(reset: true);
            _session.SyncActiveRunIfPresent();
            return true;
        }

        internal void ReloadCombatSandboxConfig()
        {
            // 현재 레인이 전투테스트(DirectCombatSandbox)면 config 필수 — 재전투(restart)에서도 동일 계약.
            _session.QuickBattleConfig = LoadCombatSandboxConfig(
                required: _session.QuickBattleLaneKind == CombatSandboxLaneKind.DirectCombatSandbox);
        }

        internal void ReloadQuickBattleConfig()
        {
            ReloadCombatSandboxConfig();
        }

        internal void AdvanceExpeditionNode()
        {
            ResolveSelectedExpeditionNode();
        }

        internal bool SelectNextExpeditionNode(int nodeIndex)
        {
            _session.EnsureExpeditionNodes();
            var current = GetCurrentExpeditionNode();
            if (current == null || !current.NextNodeIndices.Contains(nodeIndex))
            {
                return false;
            }

            _session.SelectedExpeditionNodeIndex = nodeIndex;
            return true;
        }

        internal bool SelectNodeFromAtlas(int nodeIndex)
        {
            _session.EnsureExpeditionNodes();
            if (nodeIndex < 0 || nodeIndex >= _session._expeditionNodes.Count)
            {
                return false;
            }

            var current = GetCurrentExpeditionNode();
            if (current != null
                && current.Index == nodeIndex
                && !_session._resolvedExpeditionNodeIds.Contains(current.GraphNodeId))
            {
                _session.SelectedExpeditionNodeIndex = nodeIndex;
                return true;
            }

            return SelectNextExpeditionNode(nodeIndex);
        }

        internal ExpeditionNodeViewModel? GetCurrentExpeditionNode()
        {
            _session.EnsureExpeditionNodes();
            return _session.CurrentExpeditionNodeIndex >= 0 && _session.CurrentExpeditionNodeIndex < _session._expeditionNodes.Count
                ? _session._expeditionNodes[_session.CurrentExpeditionNodeIndex]
                : null;
        }

        internal ExpeditionNodeViewModel? GetSelectedExpeditionNode()
        {
            _session.EnsureExpeditionNodes();
            return _session.SelectedExpeditionNodeIndex is int index && index >= 0 && index < _session._expeditionNodes.Count
                ? _session._expeditionNodes[index]
                : null;
        }

        internal IReadOnlyList<int> GetSelectableNextNodeIndices()
        {
            var current = GetCurrentExpeditionNode();
            if (current == null)
            {
                return Array.Empty<int>();
            }

            if (!_session._resolvedExpeditionNodeIds.Contains(current.GraphNodeId))
            {
                return new[] { current.Index };
            }

            return current.NextNodeIndices ?? Array.Empty<int>();
        }

        internal bool IsExpeditionNodeResolved(string graphNodeId)
        {
            return !string.IsNullOrWhiteSpace(graphNodeId)
                   && _session._resolvedExpeditionNodeIds.Contains(graphNodeId);
        }

        internal bool ResolveSelectedExpeditionNode()
        {
            var selected = GetSelectedExpeditionNode();
            if (selected == null)
            {
                return false;
            }

            _session.EnsureCampaignSelection();
            _session.EnsureActiveRunNodeState(selected);

            // 멱등 가드: 헤드리스 sim 경로(TryResolveSelectedBattleNodeViaSimulation → MarkBattleResolved)는
            // 전투 노드 victory 시 이미 노드 정산(MarkNodeResolved + ApplyExpeditionNodeEffect +
            // UpdateCampaignProgress)을 마친 뒤 곧바로 이 메서드를 호출한다. 이미 정산된 노드를 여기서 다시
            // 정산하면 Gold/Echo 효과가 이중 적용되어 헤드리스 정산이 씬(MarkBattleResolved만 경유)과 갈라진다.
            // 이미 정산된 노드면 효과 재적용 없이 커서만 전진해 "헤드리스=씬, 노드당 1회" 계약을 보장한다.
            var alreadyResolved = !string.IsNullOrWhiteSpace(selected.GraphNodeId)
                && _session._resolvedExpeditionNodeIds.Contains(selected.GraphNodeId);
            if (!alreadyResolved)
            {
                _session.MarkNodeResolved(selected);
                _session.LastExpeditionEffectMessage = _session.ApplyExpeditionNodeEffect(selected);
                _session.UpdateCampaignProgressForResolvedNode(selected);
            }

            _session.CurrentExpeditionNodeIndex = _session.ResolveNextActiveNodeIndex(selected.Index);
            if (_session.ActiveRun != null)
            {
                _session.ActiveRun = _session.ActiveRun with
                {
                    Overlay = _session.ActiveRun.Overlay with
                    {
                        CurrentNodeIndex = _session.CurrentExpeditionNodeIndex,
                        SiteNodeIndex = _session.CurrentExpeditionNodeIndex,
                    }
                };
            }

            _session.RestoreResolvedProgressMarkers(includeCurrentNode: _session.CurrentExpeditionNodeIndex == selected.Index && _session.CurrentExpeditionNodeIndex >= _session._expeditionNodes.Count - 1);
            _session.SyncActiveRunRecord();
            _session.SyncExpeditionState();
            _session.AutoSelectNextExpeditionNode();
            return true;
        }

        internal void AbandonExpeditionRun()
        {
            _session.IsQuickBattleSmokeActive = false;
            _session.QuickBattleLaneKind = CombatSandboxLaneKind.None;
            _session.HasActiveExpeditionRun = false;
            _session.SelectedExpeditionNodeIndex = null;
            _session.LastExpeditionEffectMessage = new SessionTextToken(
                GameLocalizationTables.UIExpedition,
                "ui.expedition.effect.return_town",
                "Expedition ended. Returning to Town.");
            _session._hasPendingRewardSettlement = false;
            _session._pendingRewardChoices.Clear();
            _session._siteEventFlow.ResetRunState();
            _session.ActiveRun = null;
            _session.ResetAtlasSession();
            _session.SyncActiveRunRecord();
        }

        internal void ReturnToTownAfterReward()
        {
            _session.ConsumePendingPermanentUnlock();
            _session.FinalizeRewardSettlement();
            _session.IsQuickBattleSmokeActive = false;
            _session.QuickBattleLaneKind = CombatSandboxLaneKind.None;
            _session._quickBattleSeedOverride = null;
            _session._compiledQuickBattleScenario = null;
        }

        internal bool TryResolveCurrentEncounter(out ResolvedEncounterContext context, out string error) =>
            _session.TryResolveCurrentEncounterCore(out context, out error);
    }

    private void EnsureExpeditionNodes(bool reset = false)
    {
        if (reset)
        {
            _expeditionNodes.Clear();
        }

        if (_expeditionNodes.Count > 0)
        {
            return;
        }

        if (TryBuildAuthoredExpeditionNodes(out var authoredNodes))
        {
            _expeditionNodes.AddRange(authoredNodes);
            if (CurrentExpeditionNodeIndex >= _expeditionNodes.Count)
            {
                CurrentExpeditionNodeIndex = 0;
            }

            if (reset && string.IsNullOrWhiteSpace(Profile.ActiveRun?.RunId))
            {
                CurrentExpeditionNodeIndex = ResolveExpeditionEntryNodeIndex();
            }

            return;
        }

        _expeditionNodes.Add(new ExpeditionNodeViewModel(
            0,
            "camp",
            "ui.expedition.route.camp.label",
            "ui.expedition.route.camp.reward",
            "ui.expedition.route.camp.desc",
            false,
            ExpeditionNodeEffectKind.None,
            0,
            string.Empty,
            new[] { 1, 2 }));
        _expeditionNodes.Add(new ExpeditionNodeViewModel(
            1,
            "ambush-route",
            "ui.expedition.route.ambush.label",
            "ui.expedition.route.ambush.reward",
            "ui.expedition.route.ambush.desc",
            true,
            ExpeditionNodeEffectKind.Gold,
            4,
            string.Empty,
            new[] { 3 }));
        _expeditionNodes.Add(new ExpeditionNodeViewModel(
            2,
            "relay-route",
            "ui.expedition.route.relay.label",
            "ui.expedition.route.relay.reward",
            "ui.expedition.route.relay.desc",
            true,
            ExpeditionNodeEffectKind.Echo,
            1,
            string.Empty,
            new[] { 3 }));
        _expeditionNodes.Add(new ExpeditionNodeViewModel(
            3,
            "shrine-route",
            "ui.expedition.route.shrine.label",
            "ui.expedition.route.shrine.reward",
            "ui.expedition.route.shrine.desc",
            true,
            ExpeditionNodeEffectKind.TemporaryAugment,
            0,
            ResolveRewardAugmentId(2, "augment_platinum_catacomb"),
            new[] { 4 }));
        _expeditionNodes.Add(new ExpeditionNodeViewModel(
            4,
            "extract-route",
            "ui.expedition.route.extract.label",
            "ui.expedition.route.extract.reward",
            "ui.expedition.route.extract.desc",
            false,
            ExpeditionNodeEffectKind.Gold,
            4,
            string.Empty,
            Array.Empty<int>()));
    }

    private void EnsureRewardChoices(bool reset = false)
    {
        if (reset)
        {
            _pendingRewardChoices.Clear();
        }

        if (_pendingRewardChoices.Count > 0)
        {
            return;
        }

        if (!_hasPendingRewardSettlement)
        {
            return;
        }

        foreach (var choice in BuildRewardChoicesForCurrentContext())
        {
            _pendingRewardChoices.Add(ScaleRewardChoiceForEndlessHeat(choice));
        }

        if (_pendingRewardChoices.Count > 0)
        {
            AppendRuntimeTelemetry(RuntimeOperationalTelemetry.CreateRewardOptionsPresented(
                ResolveTelemetryRunId(),
                ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
                _pendingRewardChoices.Count,
                Profile.Currencies.Gold,
                Profile.Currencies.Echo));
            AppendRuntimeTelemetry(BuildEconomySnapshot("reward_options_presented"));
        }
    }

    /// <summary>
    /// 무한 순환 원정 시작 — 엔딩(EndlessUnlocked) 이후, 재개할 run/정산 대기가 없는 신규-원정 상황에서만.
    /// 전제 미충족이면 스토리 원정 시작으로 안전 강하한다(Town CTA는 EndlessEntryResolver로 같은 판정을 읽음).
    /// 회차/Heat 읽기는 별도 facade 없이 ActiveRun.EndlessCycleIndex / StoryDirector.Progress.EndlessCycle로.
    /// (GameSessionState.cs가 아닌 이 파일에 두는 이유: facade 예산 가드 + 원정 flow의 collaborator 파일 응집.)
    /// </summary>
    public void BeginEndlessExpedition()
    {
        var canBeginEndlessCycle = Profile.CampaignProgress.EndlessUnlocked
            && !CanResumeExpedition
            && !HasPendingRewardSettlement
            && !IsQuickBattleSmokeActive;
        if (!canBeginEndlessCycle)
        {
            _expeditionFlow.BeginNewExpedition();
            return;
        }

        _expeditionFlow.BeginEndlessExpedition();
    }

    /// <summary>
    /// 무한 순환 Heat 보상 스케일 — 잔향(Echo) 카드만 증액. 카드 생성 단일 chokepoint에서 적용하므로
    /// 표기(presenter의 "Echo +{n}")와 실지급(ApplyLedgerBackedReward)이 같은 필드를 읽어 자동 일치한다.
    /// 스토리 run(cycle 0)은 원값 그대로.
    /// </summary>
    private RewardChoiceViewModel ScaleRewardChoiceForEndlessHeat(RewardChoiceViewModel choice)
    {
        if ((ActiveRun?.EndlessCycleIndex ?? 0) <= 0 || choice.EchoAmount <= 0)
        {
            return choice;
        }

        return choice with
        {
            EchoAmount = EndlessCycleService.ScaleEchoAmount(choice.EchoAmount, StoryDirector.Progress.EndlessCycle.Heat),
        };
    }

    private IEnumerable<RewardChoiceViewModel> BuildRewardChoicesForCurrentContext()
    {
        if (!LastBattleVictory && _campaignRecoveryFlow.HasPersistentDefeatConsolationContext)
        {
            // A single persistent Echo claim replaces the run-scope temporary augment. Keeping it as
            // the pending settlement preserves reload/commit-once behavior before the run terminates.
            CombatContentSnapshot? snapshot = null;
            if (_sessionContentLookup.TryGetCombatSnapshot(out var availableSnapshot, out _))
            {
                snapshot = availableSnapshot;
            }

            return new[] { _campaignRecoveryFlow.BuildDefeatConsolationChoice(snapshot) };
        }

        if (!LastBattleVictory)
        {
            return new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.fallback_stash.title", "ui.reward.choice.fallback_stash.desc", 3, 0, 0, "reward.gold.fallback.3"),
                new RewardChoiceViewModel(RewardChoiceKind.Echo, "ui.reward.choice.tactical_notes.title", "ui.reward.choice.tactical_notes.desc", 0, 1, 0, "reward.echo.fallback.1"),
                BuildTemporaryAugmentChoice(0, "augment_gold_pack")
            };
        }

        if (IsQuickBattleSmokeActive)
        {
            return new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.gold_cache.title", "ui.reward.choice.gold_cache.desc", 5, 0, 0, "reward.gold.quick.5"),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.iron_blade.title", "ui.reward.choice.iron_blade.desc", 0, 0, 0, ResolveRewardItemId(0)),
                BuildTemporaryAugmentChoice(1, "augment_gold_barrage")
            };
        }

        if (TryBuildScheduledAugmentChoices(out var scheduledAugmentChoices))
        {
            return scheduledAugmentChoices;
        }

        if (TryBuildRewardChoicesFromAuthoredSource(out var authoredChoices))
        {
            return authoredChoices;
        }

        return GetCurrentExpeditionNode()?.Id switch
        {
            "ambush-route" => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.war_chest.title", "ui.reward.choice.war_chest.desc", 8, 0, 0, "reward.gold.ambush.8"),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.hook_spear.title", "ui.reward.choice.hook_spear.desc", 0, 0, 0, ResolveRewardItemId(1)),
                new RewardChoiceViewModel(RewardChoiceKind.Echo, "ui.reward.choice.scout_intel.title", "ui.reward.choice.scout_intel.desc", 0, 1, 0, "reward.echo.ambush.1")
            },
            "relay-route" => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.field_kit.title", "ui.reward.choice.field_kit.desc", 0, 0, 0, ResolveRewardItemId(2)),
                BuildTemporaryAugmentChoice(2, "augment_gold_pact"),
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.relay_pouch.title", "ui.reward.choice.relay_pouch.desc", 6, 0, 0, "reward.gold.relay.6")
            },
            "shrine-route" => new[]
            {
                BuildTemporaryAugmentChoice(3, "augment_platinum_catacomb"),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.sigil_core.title", "ui.reward.choice.sigil_core.desc", 0, 0, 0, ResolveRewardItemId(3)),
                new RewardChoiceViewModel(RewardChoiceKind.Echo, "ui.reward.choice.doctrine_cache.title", "ui.reward.choice.doctrine_cache.desc", 0, 2, 0, "reward.echo.shrine.2")
            },
            _ => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.gold_cache.title", "ui.reward.choice.gold_cache.desc", 5, 0, 0, "reward.gold.default.5"),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.iron_blade.title", "ui.reward.choice.iron_blade.desc", 0, 0, 0, ResolveRewardItemId(0)),
                BuildTemporaryAugmentChoice(1, "augment_gold_barrage")
            }
        };
    }

    // reward 카드가 실제 granted augment 의 이름/설명을 보이도록 — 동적 선택(ResolveRewardAugmentId) + augment 이름 키.
    // 이전엔 generic flavor title("Aggro Spark")이라 카드와 실제 augment 가 어긋났다(payload 동적화로 그 어긋남이 커짐).
    private RewardChoiceViewModel BuildTemporaryAugmentChoice(int index, params string[] preferredFallbackIds)
    {
        var augmentId = ResolveRewardAugmentId(index, preferredFallbackIds);
        return BuildTemporaryAugmentChoice(augmentId);
    }

    private RewardChoiceViewModel BuildTemporaryAugmentChoice(string augmentId)
    {
        return new RewardChoiceViewModel(
            RewardChoiceKind.TemporaryAugment,
            ContentLocalizationTables.BuildAugmentNameKey(augmentId),
            ContentLocalizationTables.BuildAugmentDescriptionKey(augmentId),
            0,
            0,
            0,
            augmentId);
    }

    private bool TryBuildScheduledAugmentChoices(out IReadOnlyList<RewardChoiceViewModel> choices)
    {
        choices = Array.Empty<RewardChoiceViewModel>();
        if (ActiveRun == null
            || !_sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _)
            || snapshot.AugmentCatalog is not { Count: > 0 } catalog)
        {
            return false;
        }

        var acquiredAugmentIds = ActiveRun.Overlay.TemporaryAugmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (!AugmentOfferScheduleService.TryResolvePick(
                ActiveRun.Overlay.SiteNodeIndex,
                acquiredAugmentIds.Count,
                out var schedulePick))
        {
            return false;
        }

        var canonicalTemporary = new HashSet<string>(_sessionContentLookup.GetCanonicalTemporaryAugmentIds(), StringComparer.Ordinal);
        if (canonicalTemporary.Count == 0)
        {
            return false;
        }

        var permanentEquipped = ResolveEquippedPermanentAugmentIds();
        var offerCatalog = catalog
            .Where(pair => canonicalTemporary.Contains(pair.Key) || permanentEquipped.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (offerCatalog.Count == 0)
        {
            return false;
        }

        var offer = AugmentOfferService.BuildOffer(
            offerCatalog,
            new AugmentOfferContext(
                schedulePick.PreferredBucket,
                ResolveActiveBuildTags(),
                acquiredAugmentIds,
                permanentEquipped,
                ActiveRun.Overlay.BattleSeed,
                schedulePick.PickIndex));
        if (offer.Count == 0)
        {
            return false;
        }

        choices = offer.Select(entry => BuildTemporaryAugmentChoice(entry.Id)).ToList();
        return choices.Count > 0;
    }

    private SessionTextToken ApplyExpeditionNodeEffect(ExpeditionNodeViewModel node)
    {
        return node.EffectKind switch
        {
            ExpeditionNodeEffectKind.None => new SessionTextToken(GameLocalizationTables.UIExpedition, "ui.expedition.effect.none", "No effect"),
            ExpeditionNodeEffectKind.Gold => ApplyGoldNodeEffect(node),
            ExpeditionNodeEffectKind.Echo => ApplyEchoNodeEffect(node),
            ExpeditionNodeEffectKind.TemporaryAugment => ApplyTemporaryAugmentNodeEffect(node),
            ExpeditionNodeEffectKind.PermanentAugmentSlot => ApplyPermanentSlotNodeEffect(node),
            _ => new SessionTextToken(GameLocalizationTables.UIExpedition, "ui.expedition.effect.none", "No effect")
        };
    }

    private SessionTextToken ApplyGoldNodeEffect(ExpeditionNodeViewModel node)
    {
        var amount = _campaignRecoveryFlow.FilterRouteCurrency(node.EffectAmount);
        Profile.Currencies.Gold += amount;
        return new SessionTextToken(
            GameLocalizationTables.UIExpedition,
            "ui.expedition.effect.gold",
            "+{0} Gold",
            SessionTextArg.Number(amount));
    }

    private SessionTextToken ApplyEchoNodeEffect(ExpeditionNodeViewModel node)
    {
        var amount = _campaignRecoveryFlow.FilterRouteCurrency(node.EffectAmount);
        Profile.Currencies.Echo += amount;
        return new SessionTextToken(
            GameLocalizationTables.UIExpedition,
            "ui.expedition.effect.echo",
            "Echo +{0}",
            SessionTextArg.Number(amount));
    }

    private SessionTextToken ApplyTemporaryAugmentNodeEffect(ExpeditionNodeViewModel node)
    {
        if (!string.IsNullOrWhiteSpace(node.EffectPayloadId) && !Expedition.TemporaryAugmentIds.Contains(node.EffectPayloadId))
        {
            Expedition.AddTemporaryAugment(node.EffectPayloadId);
            if (ActiveRun != null)
            {
                ActiveRun = RunStateService.ApplyTemporaryAugment(ActiveRun, node.EffectPayloadId);
                TrackPermanentAugmentProgression(node.EffectPayloadId);
                SyncActiveRunRecord();
            }
        }

        return new SessionTextToken(
            GameLocalizationTables.UIExpedition,
            "ui.expedition.effect.temp_augment",
            "Temp Augment: {0}",
            SessionTextArg.AugmentName(node.EffectPayloadId));
    }

    private SessionTextToken ApplyPermanentSlotNodeEffect(ExpeditionNodeViewModel node)
    {
        return new SessionTextToken(
            GameLocalizationTables.UIExpedition,
            "ui.expedition.effect.permanent_slot",
            "Legacy permanent slot effect ignored",
            SessionTextArg.Number(Math.Max(1, node.EffectAmount)));
    }

    private void MarkNodeResolved(ExpeditionNodeViewModel node)
    {
        if (!string.IsNullOrWhiteSpace(node.GraphNodeId))
        {
            _resolvedExpeditionNodeIds.Add(node.GraphNodeId);
        }
    }

    private void AutoSelectNextExpeditionNode()
    {
        var current = GetCurrentExpeditionNode();
        if (current != null && !_resolvedExpeditionNodeIds.Contains(current.GraphNodeId))
        {
            SelectedExpeditionNodeIndex = current.Index;
            return;
        }

        var nextNodes = GetSelectableNextNodeIndices();
        SelectedExpeditionNodeIndex = nextNodes.Count > 0 ? nextNodes[0] : null;
    }

    private void ResetExpeditionTrackForCampaignSelection()
    {
        IsQuickBattleSmokeActive = false;
        QuickBattleLaneKind = CombatSandboxLaneKind.None;
        HasActiveExpeditionRun = false;
        ActiveRun = null;
        CurrentExpeditionNodeIndex = 0;
        SelectedExpeditionNodeIndex = null;
        LastBattleVictory = false;
        LastBattleSummary = SessionTextToken.Empty;
        LastExpeditionEffectMessage = SessionTextToken.Empty;
        LastRewardApplicationSummary = SessionTextToken.Empty;
        LastPermanentUnlockSummary = SessionTextToken.Empty;
        _lastAutomaticLootBundle = null;
        _hasPendingRewardSettlement = false;
        _pendingRewardChoices.Clear();
        ResetAtlasSession();
        _resolvedExpeditionNodeIds.Clear();
        EnsureExpeditionNodes(reset: true);
        AutoSelectNextExpeditionNode();
        SyncExpeditionState();
        SyncActiveRunRecord();
    }

    internal bool TryGetSiteEventSnapshot(out CombatContentSnapshot snapshot)
    {
        return _sessionContentLookup.TryGetCombatSnapshot(out snapshot, out _);
    }

    internal void PrepareSiteEventNode(ExpeditionNodeViewModel node)
    {
        CurrentExpeditionNodeIndex = node.Index;
        SelectedExpeditionNodeIndex = node.Index;
        EnsureCampaignSelection();
        EnsureActiveRunNodeState(node);
        SyncActiveRunIfPresent();
    }

    internal void CompleteSiteEventNode(ExpeditionNodeViewModel node, ActiveRunState resolvedRun)
    {
        ActiveRun = resolvedRun;
        MarkNodeResolved(node);
        LastBattleVictory = true;
        LastBattleSummary = BuildNodeSettlementSummaryToken(node);
        LastExpeditionEffectMessage = ApplyExpeditionNodeEffect(node);
        LastRewardApplicationSummary = SessionTextToken.Empty;
        LastPermanentUnlockSummary = SessionTextToken.Empty;
        _lastAutomaticLootBundle = null;
        _hasPendingRewardSettlement = true;
        UpdateCampaignProgressForResolvedNode(node);
        ActiveRun = ActiveRun with { LastSettlementWasVictory = true };
        RestoreResolvedProgressMarkers(includeCurrentNode: true);
        EnsureRewardChoices(reset: true);
        SyncActiveRunIfPresent();
    }

    private void EnsureActiveRunNodeState(ExpeditionNodeViewModel node)
    {
        ActiveRun ??= RunStateService.StartRun(GetExpeditionRunId(), CaptureBlueprintState(), IsQuickBattleSmokeActive);
        ActiveRun = ActiveRun with
        {
            Overlay = ActiveRun.Overlay with
            {
                CurrentNodeIndex = CurrentExpeditionNodeIndex,
                SiteNodeIndex = CurrentExpeditionNodeIndex,
                ChapterId = Profile.CampaignProgress.SelectedChapterId,
                SiteId = Profile.CampaignProgress.SelectedSiteId,
                EncounterId = node.Id,
                RewardSourceId = node.RewardSourceId,
                PendingRewardIds = _pendingRewardChoices
                    .Select(choice => choice.PayloadId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
            }
        };
    }

    private void RestoreResolvedProgressMarkers(bool includeCurrentNode)
    {
        var persistedNodeIds = _resolvedExpeditionNodeIds.Count > 0
            ? _resolvedExpeditionNodeIds.ToHashSet(StringComparer.Ordinal)
            : (Profile.ActiveRun?.ResolvedExpeditionNodeIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
        var restoredNodeIndices = persistedNodeIds.Count > 0
            ? _expeditionNodes.Where(node => persistedNodeIds.Contains(node.GraphNodeId)).Select(node => node.Index)
            : ExpeditionGraphTraversal.ResolveGuaranteedTraversedNodeIndices(_expeditionNodes, CurrentExpeditionNodeIndex);

        _resolvedExpeditionNodeIds.Clear();
        foreach (var nodeIndex in restoredNodeIndices)
        {
            MarkNodeResolved(_expeditionNodes[nodeIndex]);
        }

        if (includeCurrentNode && CurrentExpeditionNodeIndex >= 0 && CurrentExpeditionNodeIndex < _expeditionNodes.Count)
        {
            MarkNodeResolved(_expeditionNodes[CurrentExpeditionNodeIndex]);
        }
    }

    private int ResolveExpeditionEntryNodeIndex() => ExpeditionGraphTraversal.ResolveEntryNodeIndex(_expeditionNodes);

    private int ResolveNextActiveNodeIndex(int resolvedNodeIndex)
    {
        if (resolvedNodeIndex < 0 || resolvedNodeIndex >= _expeditionNodes.Count)
        {
            return resolvedNodeIndex;
        }

        var nextNodeIndices = _expeditionNodes[resolvedNodeIndex].NextNodeIndices ?? Array.Empty<int>();
        if (SelectedExpeditionNodeIndex is int selectedIndex && nextNodeIndices.Contains(selectedIndex))
        {
            return selectedIndex;
        }

        foreach (var nextNodeIndex in nextNodeIndices)
        {
            if (nextNodeIndex >= 0 && nextNodeIndex < _expeditionNodes.Count)
            {
                return nextNodeIndex;
            }
        }

        return resolvedNodeIndex;
    }

    private List<string> GetOrderedCampaignChapterIds()
    {
        if (_sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _)
            && snapshot.CampaignChapters is { Count: > 0 } chapters)
        {
            return chapters.Values
                .Where(chapter => !string.IsNullOrWhiteSpace(chapter.Id))
                .OrderBy(chapter => chapter.StoryOrder)
                .ThenBy(chapter => chapter.Id, StringComparer.Ordinal)
                .Select(chapter => chapter.Id)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        return new List<string>();
    }

    private List<string> GetOrderedSiteIdsForChapter(string chapterId)
    {
        if (_sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _)
            && snapshot.CampaignChapters is { Count: > 0 } chapters
            && snapshot.ExpeditionSites is { Count: > 0 } sites
            && chapters.TryGetValue(chapterId, out var chapterTemplate))
        {
            return chapterTemplate.SiteIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id =>
                {
                    var hasTemplate = sites.TryGetValue(id, out var site);
                    return new
                    {
                        Id = id,
                        Order = hasTemplate ? site.SiteOrder : int.MaxValue,
                    };
                })
                .OrderBy(entry => entry.Order)
                .ThenBy(entry => entry.Id, StringComparer.Ordinal)
                .Select(entry => entry.Id)
                .ToList();
        }

        return new List<string>();
    }

    private void FinalizeRewardSettlement()
    {
        if (!_hasPendingRewardSettlement)
        {
            return;
        }

        _hasPendingRewardSettlement = false;
        _pendingRewardChoices.Clear();

        var currentNode = GetCurrentExpeditionNode();
        if (IsQuickBattleSmokeActive
            || !LastBattleVictory
            || currentNode == null
            || currentNode.NextNodeIndices.Count == 0)
        {
            HasActiveExpeditionRun = false;
            QuickBattleLaneKind = IsQuickBattleSmokeActive ? QuickBattleLaneKind : CombatSandboxLaneKind.None;
            ActiveRun = null;
            ResetAtlasSession();
            SyncActiveRunRecord();
            return;
        }

        HasActiveExpeditionRun = true;
        CurrentExpeditionNodeIndex = ResolveNextActiveNodeIndex(CurrentExpeditionNodeIndex);
        RestoreResolvedProgressMarkers(includeCurrentNode: false);
        EnsureCampaignSelection();
        if (ActiveRun != null)
        {
            ActiveRun = ActiveRun with
            {
                Overlay = ActiveRun.Overlay with
                {
                    CurrentNodeIndex = CurrentExpeditionNodeIndex,
                    SiteNodeIndex = CurrentExpeditionNodeIndex,
                    PendingRewardIds = Array.Empty<string>(),
                    RewardSourceId = string.Empty,
                    BattleContextHash = string.Empty,
                    FirstSelectedTemporaryAugmentId = ActiveRun.Overlay.FirstSelectedTemporaryAugmentId,
                    PendingPermanentUnlockId = ActiveRun.Overlay.PendingPermanentUnlockId,
                }
            };
        }

        AutoSelectNextExpeditionNode();
        SyncActiveRunRecord();
        SyncExpeditionState();
    }

    private SessionTextToken BuildNodeSettlementSummaryToken(ExpeditionNodeViewModel node)
    {
        return new SessionTextToken(
            GameLocalizationTables.UIReward,
            "ui.reward.summary.extract_settlement",
            "Settlement complete\nRoute: {0}\nReward Source: {1}",
            SessionTextArg.Localized(GameLocalizationTables.UIExpedition, node.LabelKey, node.Id),
            SessionTextArg.Localized(GameLocalizationTables.UIExpedition, node.PlannedRewardKey, node.Id));
    }

    private static int WrapIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        var wrapped = index % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }

    private void SyncExpeditionState()
    {
        var currentNodeIndex = ActiveRun?.Overlay.CurrentNodeIndex ?? CurrentExpeditionNodeIndex;
        var temporaryAugments = ActiveRun?.Overlay.TemporaryAugmentIds ?? Expedition.TemporaryAugmentIds;
        Expedition = new ExpeditionState(currentNodeIndex, temporaryAugments);
    }

    private void EnsureCampaignSelection()
    {
        if (!_sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            return;
        }

        var resolver = new EncounterResolutionService(snapshot);
        if (!resolver.HasAuthoredCatalog)
        {
            return;
        }

        var normalized = resolver.NormalizeCampaignProgress(new CampaignProgressState(
            Profile.CampaignProgress.SelectedChapterId,
            Profile.CampaignProgress.SelectedSiteId,
            Profile.CampaignProgress.ClearedChapterIds,
            Profile.CampaignProgress.ClearedSiteIds,
            Profile.CampaignProgress.StoryCleared,
            Profile.CampaignProgress.EndlessUnlocked));
        Profile.CampaignProgress.SelectedChapterId = normalized.SelectedChapterId;
        Profile.CampaignProgress.SelectedSiteId = normalized.SelectedSiteId;
        Profile.CampaignProgress.ClearedChapterIds = normalized.ClearedChapterIds.ToList();
        Profile.CampaignProgress.ClearedSiteIds = normalized.ClearedSiteIds.ToList();
        Profile.CampaignProgress.StoryCleared = normalized.StoryCleared;
        Profile.CampaignProgress.EndlessUnlocked = normalized.EndlessUnlocked;
    }

    private bool TryBuildAuthoredExpeditionNodes(out IReadOnlyList<ExpeditionNodeViewModel> nodes)
    {
        nodes = Array.Empty<ExpeditionNodeViewModel>();
        if (!_sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            return false;
        }

        var resolver = new EncounterResolutionService(snapshot);
        if (!resolver.HasAuthoredCatalog)
        {
            return false;
        }

        EnsureCampaignSelection();
        var built = resolver.BuildSiteTrack(Profile.CampaignProgress.SelectedChapterId, Profile.CampaignProgress.SelectedSiteId);
        if (built.Count == 0)
        {
            return false;
        }

        nodes = built
            .Select(node => new ExpeditionNodeViewModel(
                node.Index,
                node.EncounterId,
                node.NodeKind == SiteNodeKindValue.Extract
                    ? "ui.expedition.route.extract.label"
                    : node.NodeKind == SiteNodeKindValue.Event
                        ? ContentLocalizationTables.BuildSiteEventSetupKey(node.EventId)
                    : ContentLocalizationTables.BuildEncounterNameKey(node.EncounterId),
                node.NodeKind == SiteNodeKindValue.Extract
                    ? "ui.expedition.route.extract.reward"
                    : ContentLocalizationTables.BuildRewardSourceNameKey(node.RewardSourceId),
                node.NodeKind == SiteNodeKindValue.Extract
                    ? "ui.expedition.route.extract.desc"
                    : node.NodeKind == SiteNodeKindValue.Event
                        ? ContentLocalizationTables.BuildSiteEventSetupKey(node.EventId)
                    : ContentLocalizationTables.BuildEncounterDescriptionKey(node.EncounterId),
                node.RequiresBattle,
                !node.RequiresBattle && node.EchoIncome != 0
                    ? ExpeditionNodeEffectKind.Echo
                    : ExpeditionNodeEffectKind.None,
                node.EchoIncome,
                node.RewardSourceId,
                node.NextNodeIndices,
                node.NodeId,
                node.NodeKind,
                node.EventId,
                node.NodeKind is SiteNodeKindValue.Event or SiteNodeKindValue.Cache
                    ? node.RewardSourceId
                    : string.Empty))
            .ToList();
        return true;
    }

    private bool TryBuildBattleContext(
        CombatContentSnapshot snapshot,
        ActiveRunState run,
        out BattleContextState context,
        out string error)
    {
        var resolver = new EncounterResolutionService(snapshot);
        if (IsQuickBattleSmokeActive)
        {
            if (_compiledQuickBattleScenario != null)
            {
                context = BuildQuickBattleContext(_compiledQuickBattleScenario, run.RunId);
                error = string.Empty;
                return true;
            }

            context = resolver.BuildDebugSmokeContext(run, CurrentExpeditionNodeIndex);
            error = string.Empty;
            return true;
        }

        if (!resolver.HasAuthoredCatalog)
        {
            context = resolver.BuildDebugSmokeContext(run, CurrentExpeditionNodeIndex);
            error = string.Empty;
            return true;
        }

        // Atlas selection handoff 직후라면 RunBattlePayload가 이미 세팅돼 있다. 그 경우
        // payload entry path를 우선해서 Atlas identity/hash를 Battle context로 그대로 운반한다
        // (task-battle-entry-authored-node-v1 acceptance #1/#6).
        if (_runBattlePayload is { } payload && payload.IsValid)
        {
            context = resolver.BuildBattleContextFromPayload(run, payload);
            error = string.Empty;
            return true;
        }

        EnsureCampaignSelection();
        var selectedNode = GetSelectedExpeditionNode() ?? GetCurrentExpeditionNode();
        var nodeIndex = selectedNode?.Index ?? CurrentExpeditionNodeIndex;
        context = resolver.BuildBattleContext(
            run,
            Profile.CampaignProgress.SelectedChapterId,
            Profile.CampaignProgress.SelectedSiteId,
            nodeIndex);
        context = CampaignEncounterSeed.Apply(context, ResolveCampaignSeed());
        error = string.Empty;
        return true;
    }

    private BattleContextState BuildQuickBattleContext(CombatSandboxCompiledScenario scenario, string runId)
    {
        var laneId = scenario.LaneKind == CombatSandboxLaneKind.DirectCombatSandbox
            ? "combat_sandbox"
            : "town_smoke";
        var rewardSourceId = scenario.LaneKind == CombatSandboxLaneKind.DirectCombatSandbox
            ? string.Empty
            : "reward_source_debug_smoke";
        var contextHash = $"{runId}:{laneId}:{scenario.ScenarioId}:{scenario.Seed}:{scenario.LeftTeam.Snapshot.CompileHash}:{scenario.RightTeam.Snapshot.CompileHash}";
        return new BattleContextState(
            "sandbox",
            laneId,
            CurrentExpeditionNodeIndex,
            scenario.ScenarioId,
            scenario.Seed,
            contextHash,
            rewardSourceId,
            1,
            false,
            laneId,
            string.Empty);
    }

    private bool TryBuildRewardChoicesFromAuthoredSource(out IReadOnlyList<RewardChoiceViewModel> choices)
    {
        choices = Array.Empty<RewardChoiceViewModel>();
        if (!_sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _)
            || snapshot.RewardSources is not { } rewardSources)
        {
            return false;
        }

        var sourceId = ActiveRun?.Overlay.RewardSourceId;
        if (string.IsNullOrWhiteSpace(sourceId) || !rewardSources.TryGetValue(sourceId, out var source))
        {
            return false;
        }

        choices = source.Kind switch
        {
            RewardSourceKindValue.Skirmish => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.gold_cache.title", "ui.reward.choice.gold_cache.desc", 5, 0, 0, $"reward.{sourceId}.gold"),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.iron_blade.title", "ui.reward.choice.iron_blade.desc", 0, 0, 0, ResolveRewardItemId(0)),
                new RewardChoiceViewModel(RewardChoiceKind.Echo, "ui.reward.choice.tactical_notes.title", "ui.reward.choice.tactical_notes.desc", 0, 1, 0, $"reward.{sourceId}.echo")
            },
            RewardSourceKindValue.Elite => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.field_kit.title", "ui.reward.choice.field_kit.desc", 0, 0, 0, ResolveRewardItemId(1)),
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.relay_pouch.title", "ui.reward.choice.relay_pouch.desc", 6, 0, 0, $"reward.{sourceId}.gold"),
                new RewardChoiceViewModel(RewardChoiceKind.Echo, "ui.reward.choice.tactical_notes.title", "ui.reward.choice.tactical_notes.desc", 0, 1, 0, $"reward.{sourceId}.echo")
            },
            RewardSourceKindValue.Boss => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.war_chest.title", "ui.reward.choice.war_chest.desc", 10, 0, 0, $"reward.{sourceId}.gold"),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.sigil_core.title", "ui.reward.choice.sigil_core.desc", 0, 0, 0, ResolveRewardItemId(2)),
                new RewardChoiceViewModel(RewardChoiceKind.Echo, "ui.reward.choice.doctrine_cache.title", "ui.reward.choice.doctrine_cache.desc", 0, 2, 0, $"reward.{sourceId}.echo")
            },
            RewardSourceKindValue.ExtractEndRun => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.war_chest.title", "ui.reward.choice.war_chest.desc", 10, 0, 0, $"reward.{sourceId}.gold"),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.field_kit.title", "ui.reward.choice.field_kit.desc", 0, 0, 0, ResolveRewardItemId(3)),
                new RewardChoiceViewModel(RewardChoiceKind.Echo, "ui.reward.choice.doctrine_cache.title", "ui.reward.choice.doctrine_cache.desc", 0, 1, 0, $"reward.{sourceId}.echo")
            },
            _ => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.gold_cache.title", "ui.reward.choice.gold_cache.desc", 4, 0, 0, $"reward.{sourceId}.gold"),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.iron_blade.title", "ui.reward.choice.iron_blade.desc", 0, 0, 0, ResolveRewardItemId(0)),
                new RewardChoiceViewModel(RewardChoiceKind.Echo, "ui.reward.choice.tactical_notes.title", "ui.reward.choice.tactical_notes.desc", 0, 1, 0, $"reward.{sourceId}.echo")
            }
        };
        return true;
    }

    private bool TryApplyAutomaticLoot()
    {
        if (ActiveRun == null
            || string.IsNullOrWhiteSpace(ActiveRun.Overlay.RewardSourceId))
        {
            return false;
        }

        if (_campaignRecoveryFlow.IsRewardedRevisit)
        {
            CombatContentSnapshot? revisitSnapshot = null;
            IReadOnlyList<string> revisitContextTags = Array.Empty<string>();
            if (_sessionContentLookup.TryGetCombatSnapshot(out var availableSnapshot, out _))
            {
                revisitSnapshot = availableSnapshot;
                revisitContextTags = ResolveCurrentRewardContextTags(availableSnapshot);
            }

            _campaignRecoveryFlow.TryBuildRevisitAutomaticLoot(
                revisitSnapshot,
                revisitContextTags,
                out var updatedRun,
                out var revisitEntries);
            var revisitTimestamp = DateTime.UtcNow.ToString("O");
            var revisitSummaryParts = new List<string>();
            foreach (var entry in revisitEntries)
            {
                ApplyAutomaticLootEntry(entry, revisitTimestamp, revisitSummaryParts);
            }

            ActiveRun = updatedRun;
            _lastAutomaticLootBundle = new LootBundleResult(
                updatedRun.Overlay.RewardSourceId,
                "CampaignRecovery:automatic_loot",
                revisitEntries);
            SyncActiveRunRecord();
            return true;
        }

        if (!_sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            return false;
        }

        var lootService = new LootResolutionService(snapshot);
        if (!lootService.TryResolveBundle(
                ActiveRun.Overlay.RewardSourceId,
                ActiveRun.Overlay.BattleSeed,
                ResolveCurrentRewardContextTags(snapshot),
                out var bundle,
                out _))
        {
            return false;
        }

        var summaryParts = new List<string>();
        var timestamp = DateTime.UtcNow.ToString("O");
        foreach (var entry in bundle.Entries)
        {
            ApplyAutomaticLootEntry(entry, timestamp, summaryParts);
        }

        _lastAutomaticLootBundle = bundle;
        LastExpeditionEffectMessage = SessionTextToken.Plain($"Auto Loot: {string.Join(", ", summaryParts)}");
        return true;
    }

    private IReadOnlyList<string> ResolveCurrentRewardContextTags(CombatContentSnapshot snapshot)
    {
        if (ActiveRun == null)
        {
            return Array.Empty<string>();
        }

        var tags = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(ActiveRun.Overlay.ChapterId))
        {
            tags.Add(ActiveRun.Overlay.ChapterId);
        }

        if (!string.IsNullOrWhiteSpace(ActiveRun.Overlay.SiteId))
        {
            tags.Add(ActiveRun.Overlay.SiteId);
        }

        if (snapshot.Encounters is { } encounters
            && encounters.TryGetValue(ActiveRun.Overlay.EncounterId, out var encounter))
        {
            foreach (var tag in encounter.RewardDropTags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
            {
                tags.Add(tag);
            }

            if (!string.IsNullOrWhiteSpace(encounter.BossOverlayId)
                && snapshot.BossOverlays is { } overlays
                && overlays.TryGetValue(encounter.BossOverlayId, out var overlay))
            {
                foreach (var tag in overlay.RewardDropTags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
                {
                    tags.Add(tag);
                }
            }
        }

        return tags.OrderBy(tag => tag, StringComparer.Ordinal).ToList();
    }

    private void ApplyAutomaticLootEntry(LootEntry entry, string timestamp, ICollection<string> summaryParts)
    {
        var applied = true;
        switch (entry.RewardType)
        {
            case SM.Core.Content.RewardType.Gold:
                Profile.Currencies.Gold += entry.Amount;
                summaryParts.Add($"gold +{entry.Amount}");
                break;
            case SM.Core.Content.RewardType.Echo:
                Profile.Currencies.Echo += entry.Amount;
                summaryParts.Add($"echo +{entry.Amount}");
                break;
            case SM.Core.Content.RewardType.TemporaryAugment:
                for (var i = 0; i < Math.Max(1, entry.Amount); i++)
                {
                    if (!Expedition.TemporaryAugmentIds.Contains(entry.Id, StringComparer.Ordinal))
                    {
                        Expedition.AddTemporaryAugment(entry.Id);
                    }

                    if (ActiveRun != null)
                    {
                        ActiveRun = RunStateService.ApplyTemporaryAugment(ActiveRun, entry.Id);
                        TrackPermanentAugmentProgression(entry.Id);
                    }
                }

                summaryParts.Add($"{entry.Id} x{Math.Max(1, entry.Amount)}");
                break;
            case SM.Core.Content.RewardType.Item:
                for (var i = 0; i < Math.Max(1, entry.Amount); i++)
                {
                    var itemRecord = CreateGeneratedInventoryItem(
                        entry.Id,
                        rolledRarityTier: entry.ItemGrade.HasValue
                            ? (int)entry.ItemGrade.Value
                            : -1);
                    Profile.Inventory.Add(itemRecord);

                    Profile.InventoryLedger.Add(new InventoryLedgerEntryRecord
                    {
                        EntryId = Guid.NewGuid().ToString("N"),
                        RunId = ActiveRun?.RunId ?? string.Empty,
                        ItemInstanceId = itemRecord.ItemInstanceId,
                        ItemBaseId = entry.Id,
                        ChangeKind = "automatic_loot",
                        Amount = 1,
                        CreatedAtUtc = timestamp,
                        Summary = entry.ItemGrade.HasValue
                            ? $"automatic loot:{entry.RewardType}:grade={entry.ItemGrade.Value}"
                            : $"automatic loot:{entry.RewardType}",
                        SourceId = ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
                        SourceKind = ResolveRewardSourceKind(ActiveRun?.Overlay.RewardSourceId),
                    });
                }

                summaryParts.Add(
                    entry.ItemGrade.HasValue
                        ? $"{entry.Id} [{entry.ItemGrade.Value}] x{entry.Amount}"
                        : $"{entry.Id} x{entry.Amount}");
                break;
            case SM.Core.Content.RewardType.SkillManual:
            case SM.Core.Content.RewardType.SkillShard:
                summaryParts.Add($"{entry.Id} x{entry.Amount}");
                break;
            default:
                applied = false;
                break;
        }

        if (!applied)
        {
            return;
        }

        Profile.RewardLedger.Add(new RewardLedgerEntryRecord
        {
            EntryId = Guid.NewGuid().ToString("N"),
            RunId = ActiveRun?.RunId ?? string.Empty,
            RewardId = entry.Id,
            RewardType = entry.RewardType.ToString(),
            Amount = entry.Amount,
            CreatedAtUtc = timestamp,
            Summary = $"automatic loot:{entry.RewardType}",
            SourceId = ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
            SourceKind = ResolveRewardSourceKind(ActiveRun?.Overlay.RewardSourceId),
        });
    }

    private string ResolveRewardSourceKind(string? sourceId, bool isSettlementChoice = false)
    {
        if (string.IsNullOrWhiteSpace(sourceId)
            || !_sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _)
            || snapshot.RewardSources is not { } rewardSources
            || !rewardSources.TryGetValue(sourceId, out var source))
        {
            return string.Empty;
        }

        return $"{source.Kind}:{(isSettlementChoice ? "reward_choice" : "automatic_loot")}";
    }

    private string GetExpeditionRunId()
    {
        EnsureCampaignSelection();
        return string.IsNullOrWhiteSpace(Profile.CampaignProgress.SelectedSiteId)
            ? "expedition_mvp_demo"
            : Profile.CampaignProgress.SelectedSiteId;
    }

    private void UpdateCampaignProgressForResolvedNode(ExpeditionNodeViewModel node)
    {
        if (!string.Equals(node.Id, $"{Profile.CampaignProgress.SelectedSiteId}:extract", StringComparison.Ordinal))
        {
            return;
        }

        var clearedSiteIds = Profile.CampaignProgress.ClearedSiteIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        clearedSiteIds.Add(Profile.CampaignProgress.SelectedSiteId);
        Profile.CampaignProgress.ClearedSiteIds = clearedSiteIds.OrderBy(id => id, StringComparer.Ordinal).ToList();

        if (!_sessionContentLookup.TryGetCombatSnapshot(out var snapshot, out _)
            || snapshot.CampaignChapters is not { } chapters)
        {
            return;
        }

        foreach (var chapter in chapters.Values)
        {
            if (chapter.SiteIds.Count == 0 || !chapter.SiteIds.All(clearedSiteIds.Contains))
            {
                continue;
            }

            if (!Profile.CampaignProgress.ClearedChapterIds.Contains(chapter.Id))
            {
                Profile.CampaignProgress.ClearedChapterIds.Add(chapter.Id);
            }

            if (chapter.UnlocksEndlessOnClear)
            {
                Profile.CampaignProgress.EndlessUnlocked = true;
            }
        }

        Profile.CampaignProgress.StoryCleared = chapters.Values.All(chapter =>
            chapter.SiteIds.Count > 0 && chapter.SiteIds.All(clearedSiteIds.Contains));
        Profile.CampaignProgress.EndlessUnlocked |= Profile.CampaignProgress.StoryCleared;

        if (ActiveRun != null)
        {
            ActiveRun = ActiveRun with
            {
                StoryCleared = Profile.CampaignProgress.StoryCleared,
                EndlessUnlocked = Profile.CampaignProgress.EndlessUnlocked,
            };
            SyncActiveRunRecord();
        }

        // Stage 1.5b: 진행 truth를 모두 커밋한 뒤 extract 정산을 narrative director에 발화한다.
        // ExtractCommitted에 걸린 전 이벤트(사이트별 영웅 해금·midpoint reveal·캠페인 엔딩·endless 등)를 점화 —
        // 지금까지 이 moment를 발화하는 프로덕션 호출처가 없어 해당 narrative 계층 전체가 dead였다.
        // director가 조건(ChapterIs/SiteIs/FlagIs)으로 게이트 + OncePerProfile로 dedup하므로 비해당 extract는
        // 무발화로 통과하고, 빈 director(테스트 baseline)는 큐 0건. 게이트가 chapter/site라 flag set 순서와 무관.
        AdvanceNarrative(NarrativeMoment.ExtractCommitted, NarrativeMomentResolver.BuildExtractContext(this));
    }
}
