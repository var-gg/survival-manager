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

        internal void BeginNewExpedition() => _session.BeginNewExpeditionCore();

        internal void PrepareQuickBattleSmoke() => _session.PrepareQuickBattleSmokeCore();

        internal void PrepareQuickBattleSmoke(CombatSandboxConfig? quickBattleConfig) =>
            _session.PrepareQuickBattleSmokeCore(quickBattleConfig);

        internal void PrepareQuickBattleSmoke(
            CombatSandboxConfig? quickBattleConfig,
            CombatSandboxLaneKind laneKind,
            bool resetSeedOverride = true) =>
            _session.PrepareQuickBattleSmokeCore(quickBattleConfig, laneKind, resetSeedOverride);

        internal void PrepareCombatSandboxDirect() => _session.PrepareCombatSandboxDirectCore();

        internal void PrepareTownQuickBattleSmoke() => _session.PrepareTownQuickBattleSmokeCore();

        internal void RestartQuickBattle(bool advanceSeed) => _session.RestartQuickBattleCore(advanceSeed);

        internal void ExitCombatSandbox() => _session.ExitCombatSandboxCore();

        internal bool TryCycleCampaignChapter(int direction) => _session.TryCycleCampaignChapterCore(direction);

        internal bool TryCycleCampaignSite(int direction) => _session.TryCycleCampaignSiteCore(direction);

        internal bool PrepareSelectedBattleNodeHandoff() => _session.PrepareSelectedBattleNodeHandoffCore();

        internal bool ResolveSelectedNodeToRewardSettlement() => _session.ResolveSelectedNodeToRewardSettlementCore();

        internal void ReloadCombatSandboxConfig() => _session.ReloadCombatSandboxConfigCore();

        internal void ReloadQuickBattleConfig() => _session.ReloadQuickBattleConfigCore();

        internal void AdvanceExpeditionNode() => _session.AdvanceExpeditionNodeCore();

        internal bool SelectNextExpeditionNode(int nodeIndex) => _session.SelectNextExpeditionNodeCore(nodeIndex);

        internal ExpeditionNodeViewModel? GetCurrentExpeditionNode() => _session.GetCurrentExpeditionNodeCore();

        internal ExpeditionNodeViewModel? GetSelectedExpeditionNode() => _session.GetSelectedExpeditionNodeCore();

        internal IReadOnlyList<int> GetSelectableNextNodeIndices() => _session.GetSelectableNextNodeIndicesCore();

        internal bool ResolveSelectedExpeditionNode() => _session.ResolveSelectedExpeditionNodeCore();

        internal void AbandonExpeditionRun() => _session.AbandonExpeditionRunCore();

        internal void ReturnToTownAfterReward() => _session.ReturnToTownAfterRewardCore();

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
            _pendingRewardChoices.Add(choice);
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

    private IEnumerable<RewardChoiceViewModel> BuildRewardChoicesForCurrentContext()
    {
        if (!LastBattleVictory)
        {
            return new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.fallback_stash.title", "ui.reward.choice.fallback_stash.desc", 3, 0, 0, "reward.gold.fallback.3"),
                new RewardChoiceViewModel(RewardChoiceKind.Echo, "ui.reward.choice.tactical_notes.title", "ui.reward.choice.tactical_notes.desc", 0, 1, 0, "reward.echo.fallback.1"),
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "ui.reward.choice.guard_spark.title", "ui.reward.choice.guard_spark.desc", 0, 0, 0, ResolveRewardAugmentId(0, "augment_gold_pack"))
            };
        }

        if (IsQuickBattleSmokeActive)
        {
            return new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.gold_cache.title", "ui.reward.choice.gold_cache.desc", 5, 0, 0, "reward.gold.quick.5"),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.iron_blade.title", "ui.reward.choice.iron_blade.desc", 0, 0, 0, ResolveRewardItemId(0)),
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "ui.reward.choice.aggro_spark.title", "ui.reward.choice.aggro_spark.desc", 0, 0, 0, ResolveRewardAugmentId(1, "augment_gold_barrage"))
            };
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
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "ui.reward.choice.anchor_beat.title", "ui.reward.choice.anchor_beat.desc", 0, 0, 0, ResolveRewardAugmentId(2, "augment_gold_pact")),
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.relay_pouch.title", "ui.reward.choice.relay_pouch.desc", 6, 0, 0, "reward.gold.relay.6")
            },
            "shrine-route" => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "ui.reward.choice.guard_spark.title", "ui.reward.choice.guard_spark.desc", 0, 0, 0, ResolveRewardAugmentId(3, "augment_platinum_catacomb")),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.sigil_core.title", "ui.reward.choice.sigil_core.desc", 0, 0, 0, ResolveRewardItemId(3)),
                new RewardChoiceViewModel(RewardChoiceKind.Echo, "ui.reward.choice.doctrine_cache.title", "ui.reward.choice.doctrine_cache.desc", 0, 2, 0, "reward.echo.shrine.2")
            },
            _ => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "ui.reward.choice.gold_cache.title", "ui.reward.choice.gold_cache.desc", 5, 0, 0, "reward.gold.default.5"),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.iron_blade.title", "ui.reward.choice.iron_blade.desc", 0, 0, 0, ResolveRewardItemId(0)),
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "ui.reward.choice.aggro_spark.title", "ui.reward.choice.aggro_spark.desc", 0, 0, 0, ResolveRewardAugmentId(1, "augment_gold_barrage"))
            }
        };
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
        Profile.Currencies.Gold += node.EffectAmount;
        return new SessionTextToken(
            GameLocalizationTables.UIExpedition,
            "ui.expedition.effect.gold",
            "+{0} Gold",
            SessionTextArg.Number(node.EffectAmount));
    }

    private SessionTextToken ApplyEchoNodeEffect(ExpeditionNodeViewModel node)
    {
        Profile.Currencies.Echo += node.EffectAmount;
        return new SessionTextToken(
            GameLocalizationTables.UIExpedition,
            "ui.expedition.effect.echo",
            "Echo +{0}",
            SessionTextArg.Number(node.EffectAmount));
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
        if (!string.IsNullOrWhiteSpace(node.Id))
        {
            _resolvedExpeditionNodeIds.Add(node.Id);
        }
    }

    private void AutoSelectNextExpeditionNode()
    {
        var current = GetCurrentExpeditionNode();
        if (current != null && !_resolvedExpeditionNodeIds.Contains(current.Id))
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
        _resolvedExpeditionNodeIds.Clear();
        EnsureExpeditionNodes(reset: true);
        AutoSelectNextExpeditionNode();
        SyncExpeditionState();
        SyncActiveRunRecord();
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
        _resolvedExpeditionNodeIds.Clear();
        for (var index = 0; index < CurrentExpeditionNodeIndex && index < _expeditionNodes.Count; index++)
        {
            MarkNodeResolved(_expeditionNodes[index]);
        }

        if (includeCurrentNode
            && CurrentExpeditionNodeIndex >= 0
            && CurrentExpeditionNodeIndex < _expeditionNodes.Count)
        {
            MarkNodeResolved(_expeditionNodes[CurrentExpeditionNodeIndex]);
        }
    }

    private int ResolveNextActiveNodeIndex(int resolvedNodeIndex)
    {
        return resolvedNodeIndex + 1 < _expeditionNodes.Count
            ? resolvedNodeIndex + 1
            : resolvedNodeIndex;
    }

    private List<string> GetOrderedCampaignChapterIds()
    {
        return _combatContentLookup.GetOrderedCampaignChapters()
            .Where(chapter => !string.IsNullOrWhiteSpace(chapter.Id))
            .Select(chapter => chapter.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private List<string> GetOrderedSiteIdsForChapter(string chapterId)
    {
        if (!_combatContentLookup.TryGetCampaignChapterDefinition(chapterId, out var chapter))
        {
            return new List<string>();
        }

        return chapter.SiteIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id =>
            {
                var hasDefinition = _combatContentLookup.TryGetExpeditionSiteDefinition(id, out var site);
                return new
                {
                    Id = id,
                    Order = hasDefinition ? site.SiteOrder : int.MaxValue,
                };
            })
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(entry => entry.Id)
            .ToList();
    }

    private void FinalizeRewardSettlement()
    {
        if (!_hasPendingRewardSettlement)
        {
            return;
        }

        _hasPendingRewardSettlement = false;
        _pendingRewardChoices.Clear();

        if (IsQuickBattleSmokeActive || !LastBattleVictory || CurrentExpeditionNodeIndex >= _expeditionNodes.Count - 1)
        {
            HasActiveExpeditionRun = false;
            QuickBattleLaneKind = IsQuickBattleSmokeActive ? QuickBattleLaneKind : CombatSandboxLaneKind.None;
            ActiveRun = null;
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
        if (!_combatContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
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
        if (!_combatContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
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
                string.Equals(node.EncounterId, $"{Profile.CampaignProgress.SelectedSiteId}:extract", StringComparison.Ordinal)
                    ? "ui.expedition.route.extract.label"
                    : ContentLocalizationTables.BuildEncounterNameKey(node.EncounterId),
                string.Equals(node.EncounterId, $"{Profile.CampaignProgress.SelectedSiteId}:extract", StringComparison.Ordinal)
                    ? "ui.expedition.route.extract.reward"
                    : ContentLocalizationTables.BuildRewardSourceNameKey(node.RewardSourceId),
                string.Equals(node.EncounterId, $"{Profile.CampaignProgress.SelectedSiteId}:extract", StringComparison.Ordinal)
                    ? "ui.expedition.route.extract.desc"
                    : ContentLocalizationTables.BuildEncounterDescriptionKey(node.EncounterId),
                node.RequiresBattle,
                ExpeditionNodeEffectKind.None,
                0,
                node.RewardSourceId,
                node.Index + 1 < built.Count ? new[] { node.Index + 1 } : Array.Empty<int>()))
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

        EnsureCampaignSelection();
        var selectedNode = GetSelectedExpeditionNode() ?? GetCurrentExpeditionNode();
        var nodeIndex = selectedNode?.Index ?? CurrentExpeditionNodeIndex;
        context = resolver.BuildBattleContext(
            run,
            Profile.CampaignProgress.SelectedChapterId,
            Profile.CampaignProgress.SelectedSiteId,
            nodeIndex);
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
        if (!_combatContentLookup.TryGetCombatSnapshot(out var snapshot, out _)
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
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "ui.reward.choice.aggro_spark.title", "ui.reward.choice.aggro_spark.desc", 0, 0, 0, ResolveRewardAugmentId(0, "augment_gold_barrage"))
            },
            RewardSourceKindValue.Elite => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Item, "ui.reward.choice.field_kit.title", "ui.reward.choice.field_kit.desc", 0, 0, 0, ResolveRewardItemId(1)),
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "ui.reward.choice.anchor_beat.title", "ui.reward.choice.anchor_beat.desc", 0, 0, 0, ResolveRewardAugmentId(1, "augment_gold_pact")),
                new RewardChoiceViewModel(RewardChoiceKind.Echo, "ui.reward.choice.tactical_notes.title", "ui.reward.choice.tactical_notes.desc", 0, 1, 0, $"reward.{sourceId}.echo")
            },
            RewardSourceKindValue.Boss => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "ui.reward.choice.guard_spark.title", "ui.reward.choice.guard_spark.desc", 0, 0, 0, ResolveRewardAugmentId(2, "augment_platinum_catacomb")),
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
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "ui.reward.choice.aggro_spark.title", "ui.reward.choice.aggro_spark.desc", 0, 0, 0, ResolveRewardAugmentId(1, "augment_gold_barrage"))
            }
        };
        return true;
    }

    private bool TryApplyAutomaticLoot()
    {
        if (ActiveRun == null
            || string.IsNullOrWhiteSpace(ActiveRun.Overlay.RewardSourceId)
            || !_combatContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
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
            case SM.Core.Content.RewardType.SkillManual:
            case SM.Core.Content.RewardType.SkillShard:
                for (var i = 0; i < Math.Max(1, entry.Amount); i++)
                {
                    var instanceId = $"{entry.Id}-{Guid.NewGuid():N}";
                    Profile.Inventory.Add(new InventoryItemRecord
                    {
                        ItemInstanceId = instanceId,
                        ItemBaseId = entry.Id,
                        EquippedHeroId = string.Empty,
                        AffixIds = new List<string>()
                    });

                    Profile.InventoryLedger.Add(new InventoryLedgerEntryRecord
                    {
                        EntryId = Guid.NewGuid().ToString("N"),
                        RunId = ActiveRun?.RunId ?? string.Empty,
                        ItemInstanceId = instanceId,
                        ItemBaseId = entry.Id,
                        ChangeKind = "automatic_loot",
                        Amount = 1,
                        CreatedAtUtc = timestamp,
                        Summary = $"automatic loot:{entry.RewardType}",
                        SourceId = ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
                        SourceKind = ResolveRewardSourceKind(ActiveRun?.Overlay.RewardSourceId),
                    });
                }

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
            || !_combatContentLookup.TryGetCombatSnapshot(out var snapshot, out _)
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

        if (!_combatContentLookup.TryGetCombatSnapshot(out var snapshot, out _)
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
    }
}
