using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Core.Results;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity.Sandbox;
using Unity.Profiling;

namespace SM.Unity;

public sealed class GameSessionState
{
    private static readonly ProfilerMarker BindProfileMarker = new("SM.GameSessionState.BindProfile");

    private static readonly DeploymentAnchorId[] DeploymentAnchorOrder =
    {
        DeploymentAnchorId.FrontTop,
        DeploymentAnchorId.FrontCenter,
        DeploymentAnchorId.FrontBottom,
        DeploymentAnchorId.BackTop,
        DeploymentAnchorId.BackCenter,
        DeploymentAnchorId.BackBottom
    };

    private readonly ICombatContentLookup _combatContentLookup;
    private readonly LoadoutCompiler _loadoutCompiler = new();
    private readonly List<string> _expeditionSquadHeroIds = new();
    private readonly Dictionary<DeploymentAnchorId, string?> _deploymentAssignments = new();
    private readonly List<RecruitUnitPreview> _recruitOffers = new();
    private readonly List<ExpeditionNodeViewModel> _expeditionNodes = new();
    private readonly List<RewardChoiceViewModel> _pendingRewardChoices = new();
    private readonly List<TelemetryEventRecord> _runtimeTelemetryEvents = new();
    private readonly HashSet<string> _resolvedExpeditionNodeIds = new(StringComparer.Ordinal);
    private LootBundleResult? _lastAutomaticLootBundle;
    private bool _hasPendingRewardSettlement;
    private int? _quickBattleSeedOverride;
    private int _recruitOfferGeneration;
    private RecruitPhaseState _recruitPhaseState = new();
    private RecruitPityState _recruitPityState = new();
    private DuplicateConversionResult? _lastDuplicateConversion;
    private CombatSandboxCompiledScenario? _compiledQuickBattleScenario;

    public SaveProfile Profile { get; private set; } = new();
    public ActiveRunState? ActiveRun { get; private set; }
    public BattleLoadoutSnapshot? LastCompiledBattleSnapshot { get; private set; }
    public RosterState Roster { get; private set; } = new();
    public ExpeditionState Expedition { get; private set; } = new();
    public string CurrentSceneName { get; private set; } = SceneNames.Boot;
    public int PermanentAugmentSlotCount { get; private set; } = 1;
    public int CurrentExpeditionNodeIndex { get; private set; }
    public int? SelectedExpeditionNodeIndex { get; private set; }
    public bool LastBattleVictory { get; private set; }
    public bool IsQuickBattleSmokeActive { get; private set; }
    public CombatSandboxConfig? QuickBattleConfig { get; private set; }
    public CombatSandboxLaneKind QuickBattleLaneKind { get; private set; }
    public bool HasActiveExpeditionRun { get; private set; }
    public SessionTextToken LastBattleSummary { get; private set; } = SessionTextToken.Empty;
    public SessionTextToken LastExpeditionEffectMessage { get; private set; } = SessionTextToken.Empty;
    public SessionTextToken LastRewardApplicationSummary { get; private set; } = SessionTextToken.Empty;
    public SessionTextToken LastPermanentUnlockSummary { get; private set; } = SessionTextToken.Empty;
    public TeamPostureType SelectedTeamPosture { get; private set; } = TeamPostureType.StandardAdvance;
    public string SelectedTeamTacticId { get; private set; } = string.Empty;
    public IReadOnlyList<string> ExpeditionSquadHeroIds => _expeditionSquadHeroIds;
    public IReadOnlyList<DeploymentAnchorId> DeploymentAnchors => DeploymentAnchorOrder;
    public IReadOnlyList<string> BattleDeployHeroIds => DeploymentAnchorOrder
        .Select(anchor => _deploymentAssignments.TryGetValue(anchor, out var heroId) ? heroId : null)
        .Where(heroId => !string.IsNullOrWhiteSpace(heroId))
        .Cast<string>()
        .ToList();
    public IReadOnlyDictionary<DeploymentAnchorId, string?> DeploymentAssignments => _deploymentAssignments;
    public IReadOnlyList<RecruitUnitPreview> RecruitOffers => _recruitOffers;
    public IReadOnlyList<ExpeditionNodeViewModel> ExpeditionNodes => _expeditionNodes;
    public IReadOnlyList<RewardChoiceViewModel> PendingRewardChoices => _pendingRewardChoices;
    public IReadOnlyList<TelemetryEventRecord> RuntimeTelemetryEvents => _runtimeTelemetryEvents;
    public LootBundleResult? LastAutomaticLootBundle => _lastAutomaticLootBundle;
    public bool HasPendingRewardSettlement => _hasPendingRewardSettlement;
    public bool CanStartQuickBattleSmoke => !HasActiveExpeditionRun && !_hasPendingRewardSettlement;
    public bool IsDirectCombatSandboxLane => IsQuickBattleSmokeActive && QuickBattleLaneKind == CombatSandboxLaneKind.DirectCombatSandbox;
    public bool IsTownIntegrationSmokeLane => IsQuickBattleSmokeActive && QuickBattleLaneKind == CombatSandboxLaneKind.TownIntegrationSmoke;
    public bool CanResumeExpedition => HasActiveExpeditionRun && !IsQuickBattleSmokeActive && !_hasPendingRewardSettlement && CurrentExpeditionNodeIndex < _expeditionNodes.Count;
    public bool CanChangeCampaignSelection => !HasActiveExpeditionRun;
    public string SelectedCampaignChapterId => Profile.CampaignProgress.SelectedChapterId;
    public string SelectedCampaignSiteId => Profile.CampaignProgress.SelectedSiteId;
    public RecruitPhaseState RecruitPhase => _recruitPhaseState.Clone();
    public RecruitPityState RecruitPity => _recruitPityState.Clone();
    public DuplicateConversionResult? LastDuplicateConversion => _lastDuplicateConversion;
    public EconomyWallet Wallet => new()
    {
        Gold = Profile.Currencies.Gold,
        Echo = Profile.Currencies.Echo,
    };
    public int CurrentRecruitRefreshCost => RefreshCostService.GetRefreshCost(_recruitPhaseState);
    public bool CanUseScout => !_recruitPhaseState.ScoutUsedThisPhase;
    public TeamPlanProfile CurrentTeamPlan => BuildTeamPlanProfile();

    public GameSessionState(ICombatContentLookup combatContentLookup)
    {
        _combatContentLookup = combatContentLookup;
    }

    public void BindProfile(SaveProfile profile)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using (BindProfileMarker.Auto())
        {
            Profile = profile;
            Profile.Heroes ??= new List<HeroInstanceRecord>();

            if (Profile.Heroes.Count == 0)
            {
                SeedDemoProfile();
            }

            Profile.Currencies ??= new CurrencyRecord();
            Profile.Inventory ??= new List<InventoryItemRecord>();
            Profile.UnlockedPermanentAugmentIds ??= new List<string>();
            Profile.CampaignProgress ??= new CampaignProgressRecord();
            Profile.HeroLoadouts ??= new List<HeroLoadoutRecord>();
            Profile.HeroProgressions ??= new List<HeroProgressionRecord>();
            Profile.SkillInstances ??= new List<SkillInstanceRecord>();
            Profile.PassiveSelections ??= new List<PassiveSelectionRecord>();
            Profile.PermanentAugmentLoadouts ??= new List<PermanentAugmentLoadoutRecord>();
            Profile.SquadBlueprints ??= new List<SquadBlueprintRecord>();
            Profile.ActiveRun ??= new ActiveRunRecord();
            Profile.MatchHeaders ??= new List<MatchRecordHeader>();
            Profile.MatchBlobs ??= new List<MatchRecordBlob>();
            Profile.InventoryLedger ??= new List<InventoryLedgerEntryRecord>();
            Profile.RewardLedger ??= new List<RewardLedgerEntryRecord>();
            Profile.SuspicionFlags ??= new List<SuspicionFlagRecord>();
            Profile.RunSummaries ??= new List<RunSummaryRecord>();
            Profile.ArenaDefenseSnapshots ??= new List<ArenaDefenseSnapshotRecord>();
            Profile.ArenaBlueprintSlots ??= new List<ArenaBlueprintSlotRecord>();
            Profile.ArenaMatchRecords ??= new List<ArenaMatchRecordRecord>();
            Profile.ArenaSeasons ??= new List<ArenaSeasonStateRecord>();
            Profile.ArenaRewardLedger ??= new List<ArenaRewardLedgerEntryRecord>();
            NormalizeProfileContentIds();
            EnsureProfileBuildState();

            if (string.IsNullOrWhiteSpace(Profile.DisplayName))
            {
                Profile.DisplayName = "Player";
            }

            PermanentAugmentSlotCount = MetaBalanceDefaults.MaxPermanentAugmentSlots;
            CurrentExpeditionNodeIndex = 0;
            SelectedExpeditionNodeIndex = null;
            LastBattleVictory = false;
            IsQuickBattleSmokeActive = false;
            QuickBattleLaneKind = CombatSandboxLaneKind.None;
            HasActiveExpeditionRun = false;
            LastBattleSummary = SessionTextToken.Empty;
            LastExpeditionEffectMessage = SessionTextToken.Empty;
            LastRewardApplicationSummary = SessionTextToken.Empty;
            LastPermanentUnlockSummary = SessionTextToken.Empty;
            _lastAutomaticLootBundle = null;
            _hasPendingRewardSettlement = false;
            _lastDuplicateConversion = null;
            _quickBattleSeedOverride = null;
            _compiledQuickBattleScenario = null;
            SelectedTeamPosture = TeamPostureType.StandardAdvance;
            SelectedTeamTacticId = string.Empty;
            _recruitOfferGeneration = 0;
            _resolvedExpeditionNodeIds.Clear();
            _runtimeTelemetryEvents.Clear();
            ResetDeploymentAssignments();
            RestoreRecruitStates();

            Roster = new RosterState(ToHeroRecords(Profile));
            EnsureRecruitOffers();
            EnsureDefaultSquad();
            EnsureBattleDeployReady();
            EnsureCampaignSelection();
            EnsureExpeditionNodes(reset: true);
            AutoSelectNextExpeditionNode();
            RestoreActiveRunFromProfile();
            EnsureRewardChoices(reset: true);
            SyncExpeditionState();
        }

        stopwatch.Stop();
        RuntimeInstrumentation.LogDuration(
            nameof(GameSessionState) + ".BindProfile",
            stopwatch.Elapsed,
            $"heroes={Profile.Heroes.Count}; inventory={Profile.Inventory.Count}");
    }

    public void BeginNewExpedition()
    {
        IsQuickBattleSmokeActive = false;
        QuickBattleLaneKind = CombatSandboxLaneKind.None;
        HasActiveExpeditionRun = true;
        CurrentExpeditionNodeIndex = 0;
        SelectedExpeditionNodeIndex = null;
        LastBattleVictory = false;
        LastBattleSummary = SessionTextToken.Empty;
        LastExpeditionEffectMessage = SessionTextToken.Empty;
        LastRewardApplicationSummary = SessionTextToken.Empty;
        LastPermanentUnlockSummary = SessionTextToken.Empty;
        _lastAutomaticLootBundle = null;
        _hasPendingRewardSettlement = false;
        _quickBattleSeedOverride = null;
        _compiledQuickBattleScenario = null;
        _runtimeTelemetryEvents.Clear();
        _resolvedExpeditionNodeIds.Clear();
        EnsureBattleDeployReady();
        EnsureCampaignSelection();
        EnsureExpeditionNodes(reset: true);
        AutoSelectNextExpeditionNode();
        EnsureRewardChoices(reset: true);
        ActiveRun = RunStateService.StartRun(GetExpeditionRunId(), CaptureBlueprintState(), false);
        SyncActiveRunRecord();
        SyncExpeditionState();
    }

    public void PrepareQuickBattleSmoke()
    {
        var config = LoadQuickBattleConfig();
        PrepareQuickBattleSmoke(config, CombatSandboxLaneKind.TownIntegrationSmoke);
    }

    public void PrepareCombatSandboxDirect()
    {
        var config = LoadQuickBattleConfig();
        PrepareQuickBattleSmoke(config, CombatSandboxLaneKind.DirectCombatSandbox);
    }

    public void PrepareTownQuickBattleSmoke()
    {
        var config = LoadQuickBattleConfig();
        PrepareQuickBattleSmoke(config, CombatSandboxLaneKind.TownIntegrationSmoke);
    }

    internal void PrepareQuickBattleSmoke(CombatSandboxConfig? quickBattleConfig)
    {
        PrepareQuickBattleSmoke(quickBattleConfig, CombatSandboxLaneKind.TownIntegrationSmoke);
    }

    public void RestartQuickBattle(bool advanceSeed)
    {
        ReloadQuickBattleConfig();
        if (advanceSeed)
        {
            _quickBattleSeedOverride = (_quickBattleSeedOverride ?? ResolveConfiguredQuickBattleSeed(QuickBattleConfig)) + 1;
        }

        PrepareQuickBattleSmoke(
            QuickBattleConfig,
            QuickBattleLaneKind == CombatSandboxLaneKind.None
                ? ResolveDefaultQuickBattleLane(QuickBattleConfig)
                : QuickBattleLaneKind,
            resetSeedOverride: !advanceSeed);
    }

    public void ExitCombatSandbox()
    {
        IsQuickBattleSmokeActive = false;
        QuickBattleLaneKind = CombatSandboxLaneKind.None;
        HasActiveExpeditionRun = false;
        _hasPendingRewardSettlement = false;
        _pendingRewardChoices.Clear();
        _quickBattleSeedOverride = null;
        _compiledQuickBattleScenario = null;
        ActiveRun = null;
        SyncActiveRunRecord();
        SyncExpeditionState();
    }

    internal void PrepareQuickBattleSmoke(
        CombatSandboxConfig? quickBattleConfig,
        CombatSandboxLaneKind laneKind,
        bool resetSeedOverride = true)
    {
        if (!CanStartQuickBattleSmoke && !IsQuickBattleSmokeActive)
        {
            return;
        }

        IsQuickBattleSmokeActive = true;
        HasActiveExpeditionRun = false;
        LastBattleVictory = false;
        LastBattleSummary = SessionTextToken.Empty;
        LastExpeditionEffectMessage = SessionTextToken.Empty;
        LastRewardApplicationSummary = SessionTextToken.Empty;
        LastPermanentUnlockSummary = SessionTextToken.Empty;
        _lastAutomaticLootBundle = null;
        _hasPendingRewardSettlement = false;
        _runtimeTelemetryEvents.Clear();
        _pendingRewardChoices.Clear();
        if (resetSeedOverride)
        {
            _quickBattleSeedOverride = null;
        }

        _compiledQuickBattleScenario = null;
        QuickBattleConfig = quickBattleConfig;
        QuickBattleLaneKind = laneKind;
        EnsureBattleDeployReady();
        EnsureRewardChoices(reset: true);
        ActiveRun = RunStateService.StartRun("quick-battle", CaptureBlueprintState(), true);
        SyncActiveRunRecord();
        SyncExpeditionState();
    }

    public bool TryCycleCampaignChapter(int direction)
    {
        if (!CanChangeCampaignSelection)
        {
            return false;
        }

        var chapterIds = GetOrderedCampaignChapterIds();
        if (chapterIds.Count <= 1)
        {
            return false;
        }

        EnsureCampaignSelection();
        var currentIndex = Math.Max(0, chapterIds.FindIndex(id => string.Equals(id, Profile.CampaignProgress.SelectedChapterId, StringComparison.Ordinal)));
        var nextIndex = WrapIndex(currentIndex + Math.Sign(direction), chapterIds.Count);
        if (nextIndex == currentIndex)
        {
            return false;
        }

        Profile.CampaignProgress.SelectedChapterId = chapterIds[nextIndex];
        var siteIds = GetOrderedSiteIdsForChapter(Profile.CampaignProgress.SelectedChapterId);
        Profile.CampaignProgress.SelectedSiteId = siteIds.FirstOrDefault() ?? string.Empty;
        ResetExpeditionTrackForCampaignSelection();
        return true;
    }

    public bool TryCycleCampaignSite(int direction)
    {
        if (!CanChangeCampaignSelection)
        {
            return false;
        }

        EnsureCampaignSelection();
        var siteIds = GetOrderedSiteIdsForChapter(Profile.CampaignProgress.SelectedChapterId);
        if (siteIds.Count <= 1)
        {
            return false;
        }

        var currentIndex = Math.Max(0, siteIds.FindIndex(id => string.Equals(id, Profile.CampaignProgress.SelectedSiteId, StringComparison.Ordinal)));
        var nextIndex = WrapIndex(currentIndex + Math.Sign(direction), siteIds.Count);
        if (nextIndex == currentIndex)
        {
            return false;
        }

        Profile.CampaignProgress.SelectedSiteId = siteIds[nextIndex];
        ResetExpeditionTrackForCampaignSelection();
        return true;
    }

    public bool PrepareSelectedBattleNodeHandoff()
    {
        var selected = GetSelectedExpeditionNode();
        if (selected == null || !selected.RequiresBattle)
        {
            return false;
        }

        CurrentExpeditionNodeIndex = selected.Index;
        SelectedExpeditionNodeIndex = selected.Index;
        EnsureCampaignSelection();
        EnsureActiveRunNodeState(selected);
        RestoreResolvedProgressMarkers(includeCurrentNode: false);
        SyncActiveRunRecord();
        SyncExpeditionState();
        return true;
    }

    public bool ResolveSelectedNodeToRewardSettlement()
    {
        var selected = GetSelectedExpeditionNode();
        if (selected == null || selected.RequiresBattle)
        {
            return false;
        }

        CurrentExpeditionNodeIndex = selected.Index;
        SelectedExpeditionNodeIndex = selected.Index;
        EnsureCampaignSelection();
        EnsureActiveRunNodeState(selected);
        MarkNodeResolved(selected);
        LastBattleVictory = true;
        LastBattleSummary = BuildNodeSettlementSummaryToken(selected);
        LastExpeditionEffectMessage = ApplyExpeditionNodeEffect(selected);
        LastRewardApplicationSummary = SessionTextToken.Empty;
        LastPermanentUnlockSummary = SessionTextToken.Empty;
        _lastAutomaticLootBundle = null;
        _hasPendingRewardSettlement = true;
        UpdateCampaignProgressForResolvedNode(selected);
        if (ActiveRun != null)
        {
            ActiveRun = ActiveRun with { LastSettlementWasVictory = true };
        }

        RestoreResolvedProgressMarkers(includeCurrentNode: true);
        EnsureRewardChoices(reset: true);
        SyncActiveRunIfPresent();
        return true;
    }

    public void ReloadQuickBattleConfig()
    {
        QuickBattleConfig = LoadQuickBattleConfig();
    }

    private bool TryBuildQuickBattleCompiledScenario(out CombatSandboxCompiledScenario scenario, out string error)
    {
        scenario = null!;
        error = string.Empty;

        if (!IsQuickBattleSmokeActive || QuickBattleConfig == null)
        {
            return false;
        }

        var compiler = new CombatSandboxScenarioCompiler(_combatContentLookup);
        var context = new CombatSandboxCompilationContext(
            Profile,
            _deploymentAssignments,
            _expeditionSquadHeroIds,
            SelectedTeamPosture,
            SelectedTeamTacticId,
            Expedition.TemporaryAugmentIds,
            CurrentExpeditionNodeIndex);
        if (!compiler.TryCompileScenario(
                context,
                QuickBattleConfig,
                QuickBattleLaneKind == CombatSandboxLaneKind.None
                    ? ResolveDefaultQuickBattleLane(QuickBattleConfig)
                    : QuickBattleLaneKind,
                _quickBattleSeedOverride,
                out scenario,
                out error))
        {
            return false;
        }

        _compiledQuickBattleScenario = scenario;
        return true;
    }

    private static CombatSandboxConfig? LoadQuickBattleConfig()
    {
        return UnityEngine.Resources.Load<CombatSandboxConfig>("_Game/Content/Definitions/QuickBattle/quick_battle_default");
    }

    private static CombatSandboxLaneKind ResolveDefaultQuickBattleLane(CombatSandboxConfig? config)
    {
        if (config == null || config.DefaultLaneKind == CombatSandboxLaneKind.None)
        {
            return CombatSandboxLaneKind.DirectCombatSandbox;
        }

        return config.DefaultLaneKind;
    }

    private static int ResolveConfiguredQuickBattleSeed(CombatSandboxConfig? config)
    {
        if (config == null)
        {
            return 17;
        }

        if (config.Execution.Seed != 0)
        {
            return config.Execution.Seed;
        }

        return config.Seed != 0 ? config.Seed : 17;
    }

    public void AdvanceExpeditionNode()
    {
        ResolveSelectedExpeditionNode();
    }

    public bool SelectNextExpeditionNode(int nodeIndex)
    {
        EnsureExpeditionNodes();
        var current = GetCurrentExpeditionNode();
        if (current == null || !current.NextNodeIndices.Contains(nodeIndex))
        {
            return false;
        }

        SelectedExpeditionNodeIndex = nodeIndex;
        return true;
    }

    public ExpeditionNodeViewModel? GetCurrentExpeditionNode()
    {
        EnsureExpeditionNodes();
        return CurrentExpeditionNodeIndex >= 0 && CurrentExpeditionNodeIndex < _expeditionNodes.Count
            ? _expeditionNodes[CurrentExpeditionNodeIndex]
            : null;
    }

    public ExpeditionNodeViewModel? GetSelectedExpeditionNode()
    {
        EnsureExpeditionNodes();
        return SelectedExpeditionNodeIndex is int index && index >= 0 && index < _expeditionNodes.Count
            ? _expeditionNodes[index]
            : null;
    }

    public IReadOnlyList<int> GetSelectableNextNodeIndices()
    {
        var current = GetCurrentExpeditionNode();
        if (current == null)
        {
            return Array.Empty<int>();
        }

        if (!_resolvedExpeditionNodeIds.Contains(current.Id))
        {
            return new[] { current.Index };
        }

        return current.NextNodeIndices ?? Array.Empty<int>();
    }

    public bool ResolveSelectedExpeditionNode()
    {
        var selected = GetSelectedExpeditionNode();
        if (selected == null)
        {
            return false;
        }

        EnsureCampaignSelection();
        EnsureActiveRunNodeState(selected);
        MarkNodeResolved(selected);
        LastExpeditionEffectMessage = ApplyExpeditionNodeEffect(selected);
        UpdateCampaignProgressForResolvedNode(selected);
        CurrentExpeditionNodeIndex = ResolveNextActiveNodeIndex(selected.Index);
        if (ActiveRun != null)
        {
            ActiveRun = ActiveRun with
            {
                Overlay = ActiveRun.Overlay with
                {
                    CurrentNodeIndex = CurrentExpeditionNodeIndex,
                    SiteNodeIndex = CurrentExpeditionNodeIndex,
                }
            };
        }
        RestoreResolvedProgressMarkers(includeCurrentNode: CurrentExpeditionNodeIndex == selected.Index && CurrentExpeditionNodeIndex >= _expeditionNodes.Count - 1);
        SyncActiveRunRecord();
        SyncExpeditionState();
        AutoSelectNextExpeditionNode();
        return true;
    }

    public void AbandonExpeditionRun()
    {
        IsQuickBattleSmokeActive = false;
        QuickBattleLaneKind = CombatSandboxLaneKind.None;
        HasActiveExpeditionRun = false;
        SelectedExpeditionNodeIndex = null;
        LastExpeditionEffectMessage = new SessionTextToken(
            GameLocalizationTables.UIExpedition,
            "ui.expedition.effect.return_town",
            "Expedition ended. Returning to Town.");
        _hasPendingRewardSettlement = false;
        _pendingRewardChoices.Clear();
        ActiveRun = null;
        SyncActiveRunRecord();
    }

    public void ReturnToTownAfterReward()
    {
        ConsumePendingPermanentUnlock();
        FinalizeRewardSettlement();
        IsQuickBattleSmokeActive = false;
        QuickBattleLaneKind = CombatSandboxLaneKind.None;
        _quickBattleSeedOverride = null;
        _compiledQuickBattleScenario = null;
    }

    public void SetCurrentScene(string sceneName)
    {
        CurrentSceneName = sceneName;
        if (string.Equals(sceneName, SceneNames.Town, StringComparison.Ordinal))
        {
            ResetRecruitPhaseForTownEntry();
            AppendRuntimeTelemetry(BuildEconomySnapshot("town_entry"));
        }
    }

    public bool CanManualProfileReload(out string reason)
    {
        if (!string.Equals(CurrentSceneName, SceneNames.Town, StringComparison.Ordinal))
        {
            reason = "프로필 재로드는 Town에서만 허용됩니다.";
            return false;
        }

        if (HasActiveExpeditionRun)
        {
            reason = "진행 중인 expedition이 있어 프로필을 다시 불러올 수 없습니다.";
            return false;
        }

        if (_hasPendingRewardSettlement)
        {
            reason = "보상 settlement가 남아 있어 프로필을 다시 불러올 수 없습니다.";
            return false;
        }

        if (IsQuickBattleSmokeActive)
        {
            reason = "Quick Battle smoke overlay 중에는 프로필을 다시 불러올 수 없습니다.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public Result RerollRecruitOffers()
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("Refresh는 Town에서만 사용할 수 있습니다.");
        }

        var refreshCost = RefreshCostService.GetRefreshCost(_recruitPhaseState);
        if (refreshCost > 0 && Profile.Currencies.Gold < refreshCost)
        {
            return Result.Fail($"Gold가 부족합니다. refresh에는 {refreshCost} Gold가 필요합니다.");
        }

        Profile.Currencies.Gold -= refreshCost;
        _recruitPhaseState = RefreshCostService.ConsumeRefresh(_recruitPhaseState);
        _recruitOfferGeneration += 1;
        _recruitOffers.Clear();
        EnsureRecruitOffers();
        AppendRuntimeTelemetry(MetaTelemetryRecorder.CreateRecruitRefreshed(
            ResolveTelemetryRunId(),
            refreshCost,
            _recruitPhaseState.PaidRefreshCountThisPhase));
        SyncRecruitState();
        return Result.Success();
    }

    public Result Recruit(int offerIndex)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("Recruit는 Town에서만 사용할 수 있습니다.");
        }

        if (offerIndex < 0 || offerIndex >= _recruitOffers.Count)
        {
            return Result.Fail("유효하지 않은 영입 후보입니다.");
        }

        var offer = _recruitOffers[offerIndex];
        if (Profile.Currencies.Gold < offer.Metadata.GoldCost)
        {
            return Result.Fail($"Gold가 부족합니다. 영입에는 {offer.Metadata.GoldCost} Gold가 필요합니다.");
        }

        if (Profile.Heroes.Count >= MetaBalanceDefaults.TownRosterCap)
        {
            return Result.Fail($"Town roster cap {MetaBalanceDefaults.TownRosterCap}에 도달했습니다.");
        }

        if (!TryGrantRecruitPreview(offer, RecruitOfferSource.RecruitPhase, out _, out var error))
        {
            return Result.Fail(error);
        }

        Profile.Currencies.Gold -= offer.Metadata.GoldCost;
        _recruitOffers.RemoveAt(offerIndex);
        AppendRuntimeTelemetry(MetaTelemetryRecorder.CreateRecruitPurchased(
            ResolveTelemetryRunId(),
            offer,
            offerIndex));
        SyncRecruitState();
        return Result.Success();
    }

    public Result UseScout(ScoutDirective directive)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("Scout는 Town에서만 사용할 수 있습니다.");
        }

        directive ??= new ScoutDirective();
        if (directive.IsNone)
        {
            return Result.Fail("Scout directive가 필요합니다.");
        }

        if (_recruitPhaseState.ScoutUsedThisPhase)
        {
            return Result.Fail("이번 recruit phase에서는 이미 scout를 사용했습니다.");
        }

        if (Profile.Currencies.Echo < RecruitmentBalanceCatalog.ScoutEchoCost)
        {
            return Result.Fail($"Echo가 부족합니다. scout에는 {RecruitmentBalanceCatalog.ScoutEchoCost} Echo가 필요합니다.");
        }

        Profile.Currencies.Echo -= RecruitmentBalanceCatalog.ScoutEchoCost;
        _recruitPhaseState.ScoutUsedThisPhase = true;
        _recruitPhaseState.PendingScoutDirective = directive.Clone();
        AppendRuntimeTelemetry(MetaTelemetryRecorder.CreateScoutUsed(
            ResolveTelemetryRunId(),
            directive,
            RecruitmentBalanceCatalog.ScoutEchoCost));
        SyncRecruitState();
        return Result.Success();
    }

    public Result RetrainHero(string heroId, RetrainOperationKind operation)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("Retrain은 Town에서만 사용할 수 있습니다.");
        }

        if (!TryGetHero(heroId, out var hero))
        {
            return Result.Fail("유닛을 찾을 수 없습니다.");
        }

        if (!_combatContentLookup.Snapshot.Archetypes.TryGetValue(hero.ArchetypeId, out var archetype))
        {
            return Result.Fail($"Archetype '{hero.ArchetypeId}'를 찾을 수 없습니다.");
        }

        var currentFlexActiveId = ResolveHeroFlexActiveId(hero, archetype);
        var currentFlexPassiveId = ResolveHeroFlexPassiveId(hero, archetype);
        var retrainState = hero.RetrainState?.Clone() ?? new UnitRetrainState();
        var cost = RecruitmentBalanceCatalog.DefaultRetrainCosts.GetTotalCost(operation, retrainState);
        if (Profile.Currencies.Echo < cost)
        {
            return Result.Fail($"Echo가 부족합니다. retrain에는 {cost} Echo가 필요합니다.");
        }

        var result = RetrainService.Retrain(
            archetype,
            currentFlexActiveId,
            currentFlexPassiveId,
            operation,
            retrainState,
            BuildTeamPlanProfile(),
            RecruitmentBalanceCatalog.DefaultRetrainCosts,
            BuildStableSeed(heroId, retrainState.RetrainCount + (int)operation + _recruitOfferGeneration));

        Profile.Currencies.Echo -= result.EchoCost;
        hero.FlexActiveId = result.FlexActiveId;
        hero.FlexPassiveId = result.FlexPassiveId;
        hero.RetrainState = result.RetrainState;
        hero.EconomyFootprint ??= new UnitEconomyFootprint();
        hero.EconomyFootprint.RetrainEchoPaid += result.EchoCost;
        Roster = new RosterState(ToHeroRecords(Profile));
        SyncHeroBuildState(hero);
        AppendRuntimeTelemetry(MetaTelemetryRecorder.CreateRetrainPerformed(
            ResolveTelemetryRunId(),
            hero.HeroId,
            hero.ArchetypeId,
            operation,
            result));
        SyncActiveRunIfPresent();
        return Result.Success();
    }

    public Result DismissHero(string heroId)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("Dismiss는 Town에서만 사용할 수 있습니다.");
        }

        if (Profile.Heroes.Count <= 1)
        {
            return Result.Fail("마지막 roster unit은 dismiss할 수 없습니다.");
        }

        if (!TryGetHero(heroId, out var hero))
        {
            return Result.Fail("유닛을 찾을 수 없습니다.");
        }

        var refund = DismissService.CalculateRefund(hero.EconomyFootprint ?? new UnitEconomyFootprint());
        Profile.Currencies.Gold += refund.GoldRefund;
        Profile.Currencies.Echo += refund.EchoRefund;
        UnequipHeroItems(hero.HeroId);
        RemoveHeroFromRoster(hero.HeroId);
        Roster = new RosterState(ToHeroRecords(Profile));
        _recruitOffers.RemoveAll(offer => string.Equals(offer.UnitBlueprintId, hero.ArchetypeId, StringComparison.Ordinal));
        EnsureRecruitOffers();
        SyncRecruitState();
        SyncActiveRunIfPresent();
        return Result.Success();
    }

    public Result GrantHeroDirect(string archetypeId, RecruitOfferSource source = RecruitOfferSource.DirectGrant)
    {
        if (!_combatContentLookup.Snapshot.Archetypes.TryGetValue(archetypeId, out var template))
        {
            return Result.Fail($"Archetype '{archetypeId}'를 찾을 수 없습니다.");
        }

        var preview = RecruitPreviewBuilder.Roll(
            template,
            BuildTeamPlanProfile(),
            null,
            FlexRollBiasMode.NativeBiased,
            BuildStableSeed(archetypeId, Profile.Heroes.Count + _recruitOfferGeneration));
        var directPreview = new RecruitUnitPreview
        {
            UnitBlueprintId = archetypeId,
            UnitInstanceSeed = $"grant:{source}:{archetypeId}:{Profile.Heroes.Count}",
            FlexActiveId = preview.FlexActiveId,
            FlexPassiveId = preview.FlexPassiveId,
            Metadata = new RecruitOfferMetadata
            {
                SlotType = RecruitOfferSlotType.StandardA,
                Tier = template.RecruitTier,
                GoldCost = RecruitmentBalanceCatalog.DefaultRecruitTierCosts.GetCost(template.RecruitTier),
            }
        };

        return TryGrantRecruitPreview(directPreview, source, out _, out var error)
            ? Result.Success()
            : Result.Fail(error);
    }

    public Result EquipItem(string heroId, string itemInstanceId)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("장비 장착은 Town에서만 가능합니다.");
        }

        if (!TryGetHero(heroId, out var hero))
        {
            return Result.Fail("유닛을 찾을 수 없습니다.");
        }

        var item = Profile.Inventory.FirstOrDefault(
            i => string.Equals(i.ItemInstanceId, itemInstanceId, StringComparison.Ordinal));
        if (item == null)
        {
            return Result.Fail($"아이템 '{itemInstanceId}'을 찾을 수 없습니다.");
        }

        if (!string.IsNullOrWhiteSpace(item.EquippedHeroId)
            && !string.Equals(item.EquippedHeroId, heroId, StringComparison.Ordinal))
        {
            return Result.Fail("이미 다른 유닛에 장착된 아이템입니다.");
        }

        item.EquippedHeroId = heroId;
        if (!hero.EquippedItemIds.Contains(itemInstanceId))
        {
            hero.EquippedItemIds.Add(itemInstanceId);
        }

        SyncHeroBuildState(hero);
        return Result.Success();
    }

    public Result UnequipItem(string heroId, string itemInstanceId)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("장비 해제는 Town에서만 가능합니다.");
        }

        if (!TryGetHero(heroId, out var hero))
        {
            return Result.Fail("유닛을 찾을 수 없습니다.");
        }

        var item = Profile.Inventory.FirstOrDefault(
            i => string.Equals(i.ItemInstanceId, itemInstanceId, StringComparison.Ordinal));
        if (item == null)
        {
            return Result.Fail($"아이템 '{itemInstanceId}'을 찾을 수 없습니다.");
        }

        item.EquippedHeroId = string.Empty;
        hero.EquippedItemIds.RemoveAll(id => string.Equals(id, itemInstanceId, StringComparison.Ordinal));
        SyncHeroBuildState(hero);
        return Result.Success();
    }

    public Result RefitItem(string itemInstanceId, int affixSlotIndex)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("Refit은 Town에서만 가능합니다.");
        }

        var item = Profile.Inventory.FirstOrDefault(
            i => string.Equals(i.ItemInstanceId, itemInstanceId, StringComparison.Ordinal));
        if (item == null)
        {
            return Result.Fail($"아이템 '{itemInstanceId}'을 찾을 수 없습니다.");
        }

        var echoCost = MetaBalanceDefaults.RefitEchoCost;
        if (Profile.Currencies.Echo < echoCost)
        {
            return Result.Fail($"Echo가 부족합니다. Refit에는 {echoCost} Echo가 필요합니다.");
        }

        var slice = _combatContentLookup.GetFirstPlayableSlice();
        var availableAffixes = slice?.AffixIds ?? Array.Empty<string>();
        var seed = BuildStableSeed(itemInstanceId, affixSlotIndex + Profile.Currencies.Echo);
        var result = RefitService.Refit(item.AffixIds, affixSlotIndex, availableAffixes, seed);
        if (result == null)
        {
            return Result.Fail("Refit 후보 affix가 없습니다.");
        }

        Profile.Currencies.Echo -= result.EchoCost;
        item.AffixIds[affixSlotIndex] = result.NewAffixId;

        if (!string.IsNullOrWhiteSpace(item.EquippedHeroId) && TryGetHero(item.EquippedHeroId, out var equipHero))
        {
            SyncHeroBuildState(equipHero);
        }

        return Result.Success();
    }

    public Result UnlockPermanentAugmentCandidate(string augmentId)
    {
        if (string.IsNullOrWhiteSpace(augmentId))
        {
            return Result.Fail("augment ID가 비어 있습니다.");
        }

        if (!_combatContentLookup.TryGetAugmentDefinition(augmentId, out var augment) || !augment.IsPermanent)
        {
            return Result.Fail("영구 증강 후보를 찾을 수 없습니다.");
        }

        if (!Profile.UnlockedPermanentAugmentIds.Contains(augmentId, StringComparer.Ordinal))
        {
            Profile.UnlockedPermanentAugmentIds.Add(augmentId);
        }

        NormalizePermanentAugmentState();
        return Result.Success();
    }

    public Result EquipPermanentAugment(string augmentId)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("영구 증강 장착은 Town에서만 가능합니다.");
        }

        if (string.IsNullOrWhiteSpace(augmentId))
        {
            return Result.Fail("augment ID가 비어 있습니다.");
        }

        if (!_combatContentLookup.TryGetAugmentDefinition(augmentId, out var augment) || !augment.IsPermanent)
        {
            return Result.Fail("영구 증강 후보를 찾을 수 없습니다.");
        }

        if (!Profile.UnlockedPermanentAugmentIds.Contains(augmentId, StringComparer.Ordinal))
        {
            return Result.Fail("해금되지 않은 영구 증강입니다.");
        }

        var blueprintId = string.IsNullOrWhiteSpace(Profile.ActiveBlueprintId)
            ? "blueprint.default"
            : Profile.ActiveBlueprintId;
        var record = Profile.PermanentAugmentLoadouts.FirstOrDefault(
            r => string.Equals(r.BlueprintId, blueprintId, StringComparison.Ordinal));
        if (record == null)
        {
            record = new PermanentAugmentLoadoutRecord { BlueprintId = blueprintId };
            Profile.PermanentAugmentLoadouts.Add(record);
        }

        var currentlyEquippedId = record.EquippedAugmentIds.FirstOrDefault();
        if (string.Equals(currentlyEquippedId, augmentId, StringComparison.Ordinal))
        {
            return Result.Fail("이미 장착된 영구 증강입니다.");
        }

        record.EquippedAugmentIds = new List<string> { augmentId };
        PermanentAugmentSlotCount = MetaBalanceDefaults.MaxPermanentAugmentSlots;
        SyncActiveRunIfPresent();
        return Result.Success();
    }

    public Result UnequipPermanentAugment(string augmentId)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("영구 증강 해제는 Town에서만 가능합니다.");
        }

        var blueprintId = string.IsNullOrWhiteSpace(Profile.ActiveBlueprintId)
            ? "blueprint.default"
            : Profile.ActiveBlueprintId;
        var record = Profile.PermanentAugmentLoadouts.FirstOrDefault(
            r => string.Equals(r.BlueprintId, blueprintId, StringComparison.Ordinal));
        if (record == null)
        {
            return Result.Fail("영구 증강 로드아웃이 없습니다.");
        }

        var removed = record.EquippedAugmentIds.RemoveAll(
            id => string.Equals(id, augmentId, StringComparison.Ordinal));
        if (removed == 0)
        {
            return Result.Fail($"영구 증강 '{augmentId}'이(가) 장착되어 있지 않습니다.");
        }

        record.EquippedAugmentIds = record.EquippedAugmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Take(MetaBalanceDefaults.MaxPermanentAugmentSlots)
            .ToList();
        SyncActiveRunIfPresent();
        return Result.Success();
    }

    public Result SelectPassiveBoard(string heroId, string boardId)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("패시브 보드 선택은 Town에서만 가능합니다.");
        }

        if (!TryGetHero(heroId, out _))
        {
            return Result.Fail("유닛을 찾을 수 없습니다.");
        }

        if (string.IsNullOrWhiteSpace(boardId)
            || !_combatContentLookup.TryGetPassiveBoardDefinition(boardId, out _))
        {
            return Result.Fail("패시브 보드를 찾을 수 없습니다.");
        }

        var loadout = Profile.HeroLoadouts.FirstOrDefault(
            r => string.Equals(r.HeroId, heroId, StringComparison.Ordinal));
        if (loadout == null)
        {
            loadout = new HeroLoadoutRecord { HeroId = heroId };
            Profile.HeroLoadouts.Add(loadout);
        }

        loadout.PassiveBoardId = boardId;
        loadout.SelectedPassiveNodeIds = new List<string>();

        var selection = Profile.PassiveSelections.FirstOrDefault(
            s => string.Equals(s.HeroId, heroId, StringComparison.Ordinal));
        if (selection == null)
        {
            selection = new PassiveSelectionRecord { HeroId = heroId };
            Profile.PassiveSelections.Add(selection);
        }

        selection.BoardId = boardId;
        selection.SelectedNodeIds = new List<string>();
        SyncActiveRunIfPresent();
        return Result.Success();
    }

    public Result TogglePassiveNode(string heroId, string nodeId)
    {
        if (!IsTownEconomyPhase())
        {
            return Result.Fail("패시브 노드 선택은 Town에서만 가능합니다.");
        }

        if (!TryGetHero(heroId, out _))
        {
            return Result.Fail("유닛을 찾을 수 없습니다.");
        }

        var loadout = Profile.HeroLoadouts.FirstOrDefault(
            r => string.Equals(r.HeroId, heroId, StringComparison.Ordinal));
        if (loadout == null || string.IsNullOrWhiteSpace(loadout.PassiveBoardId))
        {
            return Result.Fail("유닛의 로드아웃이 없습니다. 먼저 패시브 보드를 선택하세요.");
        }

        var nodesById = BuildPassiveBoardNodeDictionary(loadout.PassiveBoardId);
        if (nodesById.Count == 0)
        {
            return Result.Fail("현재 패시브 보드의 노드 정의를 찾을 수 없습니다.");
        }

        var result = PassiveBoardSelectionValidator.Toggle(
            loadout.PassiveBoardId,
            loadout.SelectedPassiveNodeIds ?? new List<string>(),
            nodeId,
            nodesById);
        if (!result.IsValid)
        {
            return Result.Fail(result.Error);
        }

        loadout.SelectedPassiveNodeIds = result.NormalizedNodeIds.ToList();

        var selection = Profile.PassiveSelections.FirstOrDefault(
            s => string.Equals(s.HeroId, heroId, StringComparison.Ordinal));
        if (selection == null)
        {
            selection = new PassiveSelectionRecord
            {
                HeroId = heroId,
                BoardId = loadout.PassiveBoardId,
            };
            Profile.PassiveSelections.Add(selection);
        }

        selection.BoardId = loadout.PassiveBoardId;
        selection.SelectedNodeIds = loadout.SelectedPassiveNodeIds.ToList();
        SyncActiveRunIfPresent();
        return Result.Success();
    }

    public bool ToggleExpeditionHero(string heroId)
    {
        if (_expeditionSquadHeroIds.Contains(heroId))
        {
            _expeditionSquadHeroIds.Remove(heroId);
            ClearDeploymentForHero(heroId);
            EnsureBattleDeployReady();
            CaptureBlueprintState();
            SyncActiveRunIfPresent();
            return true;
        }

        if (_expeditionSquadHeroIds.Count >= MetaBalanceDefaults.ExpeditionSquadCap)
        {
            return false;
        }

        _expeditionSquadHeroIds.Add(heroId);
        EnsureBattleDeployReady();
        CaptureBlueprintState();
        SyncActiveRunIfPresent();
        return true;
    }

    public void EnsureBattleDeployReady()
    {
        EnsureDefaultSquad();
        if (IsQuickBattleSmokeActive
            && QuickBattleConfig != null
            && !QuickBattleConfig.UseScenarioAuthoring
            && QuickBattleConfig.AllySlots.Count > 0)
        {
            ApplyQuickBattleAllySlotOverrides(QuickBattleConfig);
            return;
        }

        EnsureDefaultDeploymentAssignments();
    }

    internal void ApplyQuickBattleAllySlotOverrides(CombatSandboxConfig config)
    {
        EnsureDefaultSquad();
        EnsureAssignmentMapInitialized();

        var availableHeroIds = Profile.Heroes
            .Where(hero => !string.IsNullOrWhiteSpace(hero.HeroId))
            .Select(hero => hero.HeroId)
            .ToHashSet(StringComparer.Ordinal);
        var configuredHeroIds = config.AllySlots
            .Where(slot => slot != null && !string.IsNullOrWhiteSpace(slot.HeroId))
            .Select(slot => slot.HeroId)
            .Where(availableHeroIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .Take(MetaBalanceDefaults.ExpeditionSquadCap)
            .ToList();

        if (configuredHeroIds.Count == 0)
        {
            EnsureDefaultDeploymentAssignments();
            return;
        }

        _expeditionSquadHeroIds.Clear();
        _expeditionSquadHeroIds.AddRange(configuredHeroIds);

        foreach (var anchor in DeploymentAnchorOrder)
        {
            _deploymentAssignments[anchor] = null;
        }

        var occupiedAnchors = new HashSet<DeploymentAnchorId>();
        var assignedHeroes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in config.AllySlots)
        {
            if (slot == null || string.IsNullOrWhiteSpace(slot.HeroId) || !availableHeroIds.Contains(slot.HeroId))
            {
                continue;
            }

            if (!assignedHeroes.Add(slot.HeroId) || !occupiedAnchors.Add(slot.Anchor))
            {
                continue;
            }

            _deploymentAssignments[slot.Anchor] = slot.HeroId;
        }

        CaptureBlueprintState();
        SyncActiveRunIfPresent();
    }

    public void PromoteToBattleDeploy(string heroId)
    {
        if (!_expeditionSquadHeroIds.Contains(heroId))
        {
            return;
        }

        var preferredAnchor = ResolvePreferredAnchor(heroId);
        AssignHeroToAnchor(preferredAnchor, heroId);
    }

    public string? GetAssignedHeroId(DeploymentAnchorId anchor)
    {
        return _deploymentAssignments.TryGetValue(anchor, out var heroId) ? heroId : null;
    }

    public bool AssignHeroToAnchor(DeploymentAnchorId anchor, string? heroId)
    {
        EnsureDefaultSquad();
        EnsureAssignmentMapInitialized();

        heroId = string.IsNullOrWhiteSpace(heroId) ? null : heroId;
        if (heroId != null && !_expeditionSquadHeroIds.Contains(heroId))
        {
            return false;
        }

        var currentHero = GetAssignedHeroId(anchor);
        var occupiedBefore = BattleDeployHeroIds.Count;
        var candidateAlreadyAssigned = heroId != null && BattleDeployHeroIds.Contains(heroId);
        var occupiedAfter = occupiedBefore
            - (string.IsNullOrWhiteSpace(currentHero) ? 0 : 1)
            + (heroId == null || candidateAlreadyAssigned ? 0 : 1);
        if (occupiedAfter > MetaBalanceDefaults.BattleDeployCap)
        {
            return false;
        }

        if (heroId != null)
        {
            foreach (var existingAnchor in DeploymentAnchorOrder)
            {
                if (existingAnchor == anchor)
                {
                    continue;
                }

                if (_deploymentAssignments.TryGetValue(existingAnchor, out var existingHero) && existingHero == heroId)
                {
                    _deploymentAssignments[existingAnchor] = null;
                }
            }
        }

        _deploymentAssignments[anchor] = heroId;
        CaptureBlueprintState();
        SyncActiveRunIfPresent();
        return true;
    }

    public bool CycleDeploymentAssignment(DeploymentAnchorId anchor)
    {
        EnsureBattleDeployReady();

        var candidates = new List<string?> { null };
        candidates.AddRange(_expeditionSquadHeroIds);

        var current = GetAssignedHeroId(anchor);
        var startIndex = candidates.FindIndex(candidate => candidate == current);
        startIndex = startIndex < 0 ? 0 : startIndex;

        for (var offset = 1; offset <= candidates.Count; offset++)
        {
            var candidate = candidates[(startIndex + offset) % candidates.Count];
            if (AssignHeroToAnchor(anchor, candidate))
            {
                return true;
            }
        }

        return false;
    }

    public void CycleTeamPosture()
    {
        var values = (TeamPostureType[])Enum.GetValues(typeof(TeamPostureType));
        var currentIndex = Array.IndexOf(values, SelectedTeamPosture);
        SelectedTeamPosture = values[(currentIndex + 1) % values.Length];
        CaptureBlueprintState();
        SyncActiveRunIfPresent();
    }

    public void SetTeamPosture(TeamPostureType posture)
    {
        SelectedTeamPosture = posture;
        CaptureBlueprintState();
        SyncActiveRunIfPresent();
    }

    public void SetTeamTactic(string teamTacticId)
    {
        SelectedTeamTacticId = teamTacticId ?? string.Empty;
        CaptureBlueprintState();
        SyncActiveRunIfPresent();
    }

    public IEnumerable<(DeploymentAnchorId Anchor, string? HeroId)> EnumerateDeploymentAssignments()
    {
        EnsureBattleDeployReady();
        foreach (var anchor in DeploymentAnchorOrder)
        {
            yield return (anchor, GetAssignedHeroId(anchor));
        }
    }

    public IReadOnlyList<BattleParticipantSpec> BuildBattleParticipants()
    {
        EnsureBattleDeployReady();

        var temporaryAugments = Expedition.TemporaryAugmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var inventoryByInstanceId = Profile.Inventory
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemInstanceId))
            .GroupBy(item => item.ItemInstanceId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return EnumerateDeploymentAssignments()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.HeroId))
            .Select(entry =>
            {
                var hero = Profile.Heroes.First(record => record.HeroId == entry.HeroId);
                return new BattleParticipantSpec(
                    hero.HeroId,
                    hero.Name,
                    hero.ArchetypeId,
                    entry.Anchor,
                    hero.PositiveTraitId,
                    hero.NegativeTraitId,
                    BuildEquippedItemSpecs(hero, inventoryByInstanceId),
                    temporaryAugments,
                    SelectedTeamPosture,
                    ResolveRoleTag(hero.ClassId, entry.Anchor),
                    "opening:standard",
                    hero.CharacterId,
                    ResolveBlueprintRoleInstructionId(hero.HeroId, hero.ClassId, entry.Anchor));
            })
            .ToList();
    }

    public BattleLoadoutSnapshot BuildBattleLoadoutSnapshot()
    {
        EnsureBattleDeployReady();

        if (!_combatContentLookup.TryGetCombatSnapshot(out var snapshot, out var error))
        {
            throw new InvalidOperationException(error);
        }

        if (TryBuildQuickBattleCompiledScenario(out var quickBattleScenario, out error))
        {
            var quickBattleBlueprint = quickBattleScenario.LeftTeam.Blueprint;
            var quickBattleOverlay = quickBattleScenario.LeftTeam.Overlay with
            {
                CurrentNodeIndex = CurrentExpeditionNodeIndex,
                SiteNodeIndex = CurrentExpeditionNodeIndex,
            };
            var quickBattleRun = ActiveRun ?? RunStateService.StartRun("quick-battle", quickBattleBlueprint, true);

            LastCompiledBattleSnapshot = quickBattleScenario.LeftTeam.Snapshot;
            _compiledQuickBattleScenario = quickBattleScenario;
            ActiveRun = quickBattleRun with
            {
                Blueprint = quickBattleBlueprint,
                Overlay = quickBattleOverlay,
                BattleDeployHeroIds = quickBattleScenario.LeftTeam.Snapshot.BattleDeployHeroIds.ToList(),
            };
            if (TryBuildBattleContext(snapshot, ActiveRun!, out var quickBattleContext, out _))
            {
                ActiveRun = RunStateService.SetBattleContext(ActiveRun!, quickBattleContext);
            }

            ActiveRun = RunStateService.SyncBlueprint(
                ActiveRun!,
                quickBattleBlueprint,
                quickBattleScenario.LeftTeam.Snapshot.CompileHash,
                Array.Empty<string>());
            SyncActiveRunRecord();
            SyncExpeditionState();
            return quickBattleScenario.LeftTeam.Snapshot;
        }

        var blueprint = CaptureBlueprintState();
        var activeRun = ActiveRun ?? RunStateService.StartRun(IsQuickBattleSmokeActive ? "quick-battle" : GetExpeditionRunId(), blueprint, IsQuickBattleSmokeActive);
        var overlay = activeRun.Overlay with
        {
            CurrentNodeIndex = CurrentExpeditionNodeIndex,
            SiteNodeIndex = CurrentExpeditionNodeIndex,
            TemporaryAugmentIds = Expedition.TemporaryAugmentIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList(),
            PendingRewardIds = _pendingRewardChoices.Select(choice => choice.PayloadId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList(),
        };
        var compiled = _loadoutCompiler.Compile(
            ToHeroRecords(Profile).ToList(),
            ToHeroLoadoutStates(Profile),
            ToHeroProgressionStates(Profile),
            ToItemInstanceStates(Profile),
            ToSkillInstanceStates(Profile),
            ToPassiveSelections(Profile),
            ToPermanentAugmentLoadout(Profile, blueprint.BlueprintId),
            blueprint,
            overlay,
            snapshot);

        LastCompiledBattleSnapshot = compiled;
        ActiveRun = activeRun with { Overlay = overlay };
        if (TryBuildBattleContext(snapshot, ActiveRun!, out var battleContext, out _))
        {
            ActiveRun = RunStateService.SetBattleContext(ActiveRun!, battleContext);
        }

        ActiveRun = RunStateService.SyncBlueprint(
            ActiveRun!,
            blueprint,
            compiled.CompileHash,
            _pendingRewardChoices.Select(choice => choice.PayloadId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList());
        SyncActiveRunRecord();
        SyncExpeditionState();
        return compiled;
    }

    public bool TryResolveCurrentEncounter(out ResolvedEncounterContext context, out string error)
    {
        context = null!;
        error = string.Empty;

        if (!_combatContentLookup.TryGetCombatSnapshot(out var snapshot, out error))
        {
            return false;
        }

        if (TryBuildQuickBattleCompiledScenario(out var quickBattleScenario, out error))
        {
            var quickBattleRun = ActiveRun ?? RunStateService.StartRun("quick-battle", quickBattleScenario.LeftTeam.Blueprint, true);
            var quickBattleOverlay = quickBattleScenario.LeftTeam.Overlay with
            {
                CurrentNodeIndex = CurrentExpeditionNodeIndex,
                SiteNodeIndex = CurrentExpeditionNodeIndex,
            };
            var quickBattleContext = BuildQuickBattleContext(quickBattleScenario, quickBattleRun.RunId);
            ActiveRun = RunStateService.SetBattleContext(
                quickBattleRun with
                {
                    Blueprint = quickBattleScenario.LeftTeam.Blueprint,
                    Overlay = quickBattleOverlay,
                    BattleDeployHeroIds = quickBattleScenario.LeftTeam.Snapshot.BattleDeployHeroIds.ToList(),
                },
                quickBattleContext);
            SyncActiveRunRecord();
            context = new ResolvedEncounterContext(
                quickBattleContext,
                quickBattleScenario.RightTeam.Snapshot.TeamTactic.Posture,
                quickBattleScenario.RightTeam.Snapshot.Allies);
            return true;
        }

        var run = ActiveRun ?? RunStateService.StartRun(IsQuickBattleSmokeActive ? "quick-battle" : GetExpeditionRunId(), CaptureBlueprintState(), IsQuickBattleSmokeActive);
        if (TryBuildBattleContext(snapshot, run, out var battleContext, out _))
        {
            var resolver = new EncounterResolutionService(snapshot);
            if (resolver.TryResolveEncounter(battleContext, out context, out error))
            {
                ActiveRun = RunStateService.SetBattleContext(run, battleContext);
                SyncActiveRunRecord();
                return true;
            }
        }

        var debugPlan = BuildQuickBattleEncounterPlan();
        var buildResult = BattleSetupBuilder.Build(Array.Empty<BattleParticipantSpec>(), debugPlan, snapshot);
        if (!buildResult.IsSuccess)
        {
            error = buildResult.Error ?? "Failed to build debug smoke encounter.";
            return false;
        }

        var debugContext = new EncounterResolutionService(snapshot).BuildDebugSmokeContext(run, CurrentExpeditionNodeIndex);
        ActiveRun = RunStateService.SetBattleContext(run, debugContext);
        SyncActiveRunRecord();
        context = new ResolvedEncounterContext(debugContext, debugPlan.EnemyPosture, buildResult.Enemies);
        return true;
    }

    private BattleEncounterPlan BuildQuickBattleEncounterPlan()
    {
        var config = QuickBattleConfig;
        if (config == null || config.EnemySlots.Count == 0)
        {
            return BattleEncounterPlans.CreateObserverSmokePlan();
        }

        return new BattleEncounterPlan(
            config.EnemySlots.Select(slot => new BattleParticipantSpec(
                string.IsNullOrWhiteSpace(slot.ParticipantId) ? $"enemy.{ResolveSandboxArchetypeId(slot)}.{slot.Anchor}" : slot.ParticipantId,
                string.IsNullOrWhiteSpace(slot.DisplayName) ? ResolveSandboxCharacterId(slot) : slot.DisplayName,
                ResolveSandboxArchetypeId(slot),
                slot.Anchor,
                slot.PositiveTraitId,
                slot.NegativeTraitId,
                Array.Empty<BattleEquippedItemSpec>(),
                slot.TemporaryAugmentIds,
                config.EnemyPosture,
                ResolveSandboxRoleTag(slot),
                "opening:standard",
                ResolveSandboxCharacterId(slot),
                ResolveSandboxRoleInstructionId(slot)))
            .ToList(),
            config.EnemyPosture);
    }

    public void RecordBattleAudit(BattleReplayBundle replay)
    {
        Profile.MatchHeaders.Add(new MatchRecordHeader
        {
            MatchId = replay.Header.MatchId,
            RunId = ActiveRun?.RunId ?? string.Empty,
            ContentVersion = replay.Header.ContentVersion,
            SimVersion = replay.Header.SimVersion,
            Seed = replay.Header.Seed,
            PlayerSnapshotHash = replay.Header.PlayerSnapshotHash,
            EnemySnapshotHash = replay.Header.EnemySnapshotHash,
            StartedAtUtc = replay.Header.StartedAtUtc,
            CompletedAtUtc = replay.Header.CompletedAtUtc,
            Winner = replay.Header.Winner.ToString(),
            FinalStateHash = replay.Header.FinalStateHash,
        });
        Profile.MatchBlobs.Add(new MatchRecordBlob
        {
            MatchId = replay.Header.MatchId,
            CompileVersion = replay.Input.CompileVersion,
            CompileHash = replay.Input.CompileHash,
            InputDigest = $"{replay.Input.TeamPosture}|{replay.Input.Allies.Count}|{replay.Input.Enemies.Count}",
            BattleSummaryDigest = replay.BattleSummary == null
                ? string.Empty
                : $"{replay.BattleSummary.WinnerSideIndex}|{replay.BattleSummary.BattleDurationSeconds:0.###}|{replay.BattleSummary.UnexplainedDamageRatio:0.###}|{replay.BattleSummary.MajorEventCollisionRate:0.###}",
            ReadabilityDigest = replay.Readability == null
                ? string.Empty
                : $"{replay.Readability.UnexplainedDamageRatio:0.###}|{replay.Readability.UnexplainedHealingRatio:0.###}|{string.Join(",", replay.Readability.Violations.Select(violation => violation.ToString()))}",
            EventStream = replay.EventStream.Select(@event =>
                $"{@event.StepIndex}|{@event.ActorId.Value}|{@event.ActionType}|{@event.LogCode}|{@event.TargetId?.Value}|{@event.Value:0.###}|{@event.EventKind}|{@event.PayloadId}|{@event.SecondaryValue:0.###}|{@event.Note}").ToList(),
            KeyframeDigests = replay.Keyframes.Select(frame =>
                $"{frame.StepIndex}|{frame.TimeSeconds:0.###}|{frame.StateHash}").ToList(),
            TelemetryEvents = replay.TelemetryEvents?
                .Select(record => $"{record.Domain}|{record.EventKind}|{record.TimeSeconds:0.###}|{record.Explain?.SourceContentId}|{record.Explain?.SourceDisplayName}|{record.StringValueA}|{record.ValueA:0.###}|{record.IntValueA}")
                .ToList()
                ?? new List<string>(),
            ArtifactPaths = new List<string>
            {
                replay.BattleSummary != null ? "Logs/loop-d-balance" : string.Empty,
                replay.Readability != null ? "Logs/loop-d-balance/readability_watchlist.json" : string.Empty,
            }.Where(path => !string.IsNullOrWhiteSpace(path)).ToList(),
        });

        if (ActiveRun != null)
        {
            if (!string.IsNullOrWhiteSpace(ActiveRun.Overlay.LastCompileHash)
                && !string.Equals(ActiveRun.Overlay.LastCompileHash, replay.Input.CompileHash, StringComparison.Ordinal))
            {
                Profile.SuspicionFlags.Add(new SuspicionFlagRecord
                {
                    FlagId = Guid.NewGuid().ToString("N"),
                    RunId = ActiveRun.RunId,
                    MatchId = replay.Header.MatchId,
                    Reason = "compile_hash_mismatch",
                    ExpectedHash = ActiveRun.Overlay.LastCompileHash,
                    ObservedHash = replay.Input.CompileHash,
                    CreatedAtUtc = DateTime.UtcNow.ToString("O"),
                });
            }

            ActiveRun = RunStateService.CompleteBattle(ActiveRun, replay.Header.MatchId);
            SyncActiveRunRecord();
        }
    }

    public void SaveDebugSnapshot(string note = "manual-debug-save")
    {
        Profile.RunSummaries.Add(new RunSummaryRecord
        {
            RunId = Guid.NewGuid().ToString("N"),
            ExpeditionId = note,
            Result = "debug-save",
            GoldEarned = 0,
            NodesCleared = CurrentExpeditionNodeIndex,
            CompletedAtUtc = DateTime.UtcNow.ToString("O")
        });
    }

    public void SetLastBattleResult(bool victory, string summary)
    {
        LastBattleVictory = victory;
        LastBattleSummary = SessionTextToken.Plain(summary);
        LastRewardApplicationSummary = SessionTextToken.Empty;
        LastPermanentUnlockSummary = SessionTextToken.Empty;
        _hasPendingRewardSettlement = true;
        if (ActiveRun != null)
        {
            ActiveRun = ActiveRun with { LastSettlementWasVictory = victory };
        }

        EnsureRewardChoices(reset: true);
        SyncActiveRunIfPresent();
    }

    public void MarkBattleResolved(bool victory, int stepCount, int eventCount)
    {
        var resolvedNode = GetSelectedExpeditionNode() ?? GetCurrentExpeditionNode();
        var shouldCreateRewardSettlement = !IsDirectCombatSandboxLane;
        LastBattleVictory = victory;
        LastRewardApplicationSummary = SessionTextToken.Empty;
        LastPermanentUnlockSummary = SessionTextToken.Empty;
        _lastAutomaticLootBundle = null;
        _hasPendingRewardSettlement = shouldCreateRewardSettlement;

        if (resolvedNode != null && !IsQuickBattleSmokeActive)
        {
            CurrentExpeditionNodeIndex = resolvedNode.Index;
            SelectedExpeditionNodeIndex = resolvedNode.Index;
            EnsureCampaignSelection();
            EnsureActiveRunNodeState(resolvedNode);
        }

        if (victory && ActiveRun != null && !string.IsNullOrWhiteSpace(ActiveRun.Overlay.RewardSourceId))
        {
            TryApplyAutomaticLoot();
        }

        if (victory && resolvedNode != null && !IsQuickBattleSmokeActive)
        {
            MarkNodeResolved(resolvedNode);
            LastExpeditionEffectMessage = ApplyExpeditionNodeEffect(resolvedNode);
            UpdateCampaignProgressForResolvedNode(resolvedNode);
            LastBattleSummary = LastExpeditionEffectMessage.HasValue
                ? new SessionTextToken(
                    GameLocalizationTables.UIReward,
                    "ui.reward.battle_summary.route_effect",
                    "{0} / {1} steps / {2} events\nRoute: {3}\nNode Effect: {4}",
                    SessionTextArg.Localized(
                        GameLocalizationTables.UIReward,
                        victory ? "ui.reward.result.victory" : "ui.reward.result.defeat",
                        victory ? "Victory" : "Defeat"),
                    SessionTextArg.Number(stepCount),
                    SessionTextArg.Number(eventCount),
                    SessionTextArg.Localized(GameLocalizationTables.UIExpedition, resolvedNode.LabelKey, resolvedNode.Id),
                    SessionTextArg.Token(LastExpeditionEffectMessage))
                : new SessionTextToken(
                    GameLocalizationTables.UIReward,
                    "ui.reward.battle_summary.route",
                    "{0} / {1} steps / {2} events\nRoute: {3}",
                    SessionTextArg.Localized(
                        GameLocalizationTables.UIReward,
                        victory ? "ui.reward.result.victory" : "ui.reward.result.defeat",
                        victory ? "Victory" : "Defeat"),
                    SessionTextArg.Number(stepCount),
                    SessionTextArg.Number(eventCount),
                    SessionTextArg.Localized(GameLocalizationTables.UIExpedition, resolvedNode.LabelKey, resolvedNode.Id));
            RestoreResolvedProgressMarkers(includeCurrentNode: true);
        }
        else
        {
            LastBattleSummary = BuildBattleSummaryToken(victory, stepCount, eventCount);
            if (!IsQuickBattleSmokeActive)
            {
                HasActiveExpeditionRun = false;
            }

            if (resolvedNode != null && !IsQuickBattleSmokeActive)
            {
                RestoreResolvedProgressMarkers(includeCurrentNode: false);
            }
        }

        if (ActiveRun != null)
        {
            ActiveRun = ActiveRun with
            {
                LastSettlementWasVictory = victory,
                Overlay = ActiveRun.Overlay with
                {
                    PendingRewardIds = shouldCreateRewardSettlement ? ActiveRun.Overlay.PendingRewardIds : Array.Empty<string>(),
                    RewardSourceId = shouldCreateRewardSettlement ? ActiveRun.Overlay.RewardSourceId : string.Empty,
                }
            };
        }

        if (shouldCreateRewardSettlement)
        {
            EnsureRewardChoices(reset: true);
        }
        else
        {
            _pendingRewardChoices.Clear();
        }

        SyncActiveRunIfPresent();
    }

    public bool ApplyRewardChoice(int index)
    {
        if (index < 0 || index >= _pendingRewardChoices.Count)
        {
            return false;
        }

        var choice = _pendingRewardChoices[index];
        var rewardSourceId = ActiveRun?.Overlay.RewardSourceId ?? string.Empty;
        if (HasRecordedRewardSettlement(rewardSourceId))
        {
            _pendingRewardChoices.Clear();
            LastRewardApplicationSummary = new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.status.recovered_choice",
                "Recovered previous reward settlement.");
            AppendRuntimeTelemetry(RuntimeOperationalTelemetry.CreateRewardSettlementDuplicatePrevented(
                ResolveTelemetryRunId(),
                rewardSourceId));
            SyncActiveRunIfPresent();
            return true;
        }

        var timestamp = DateTime.UtcNow.ToString("O");
        switch (choice.Kind)
        {
            case RewardChoiceKind.Gold:
                ApplyLedgerBackedReward(new RewardOption(choice.PayloadId, SM.Content.Definitions.RewardType.Gold, choice.GoldAmount, BuildRewardChoiceSummaryKey(choice)), BuildRewardChoiceSummaryToken(choice));
                break;
            case RewardChoiceKind.Item:
                Profile.Inventory.Add(new InventoryItemRecord
                {
                    ItemInstanceId = $"{choice.PayloadId}-{Guid.NewGuid():N}",
                    ItemBaseId = choice.PayloadId,
                    EquippedHeroId = string.Empty,
                    AffixIds = new List<string>()
                });
                Profile.InventoryLedger.Add(new InventoryLedgerEntryRecord
                {
                    EntryId = Guid.NewGuid().ToString("N"),
                    RunId = ActiveRun?.RunId ?? string.Empty,
                    ItemInstanceId = Profile.Inventory.Last().ItemInstanceId,
                    ItemBaseId = choice.PayloadId,
                    ChangeKind = "reward_item",
                    Amount = 1,
                    CreatedAtUtc = timestamp,
                    Summary = BuildRewardChoiceSummaryKey(choice),
                    SourceId = ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
                    SourceKind = ResolveRewardSourceKind(ActiveRun?.Overlay.RewardSourceId, isSettlementChoice: true),
                });
                Profile.RewardLedger.Add(new RewardLedgerEntryRecord
                {
                    EntryId = Guid.NewGuid().ToString("N"),
                    RunId = ActiveRun?.RunId ?? string.Empty,
                    RewardId = choice.PayloadId,
                    RewardType = SM.Content.Definitions.RewardType.Item.ToString(),
                    Amount = 1,
                    CreatedAtUtc = timestamp,
                    Summary = BuildRewardChoiceSummaryKey(choice),
                    SourceId = ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
                    SourceKind = ResolveRewardSourceKind(ActiveRun?.Overlay.RewardSourceId, isSettlementChoice: true),
                });
                LastRewardApplicationSummary = BuildRewardChoiceSummaryToken(choice);
                break;
            case RewardChoiceKind.TemporaryAugment:
                ApplyLedgerBackedReward(new RewardOption(choice.PayloadId, SM.Content.Definitions.RewardType.TemporaryAugment, 1, BuildRewardChoiceSummaryKey(choice)), BuildRewardChoiceSummaryToken(choice));
                break;
            case RewardChoiceKind.Echo:
                ApplyLedgerBackedReward(new RewardOption(choice.PayloadId, SM.Content.Definitions.RewardType.Echo, choice.EchoAmount, BuildRewardChoiceSummaryKey(choice)), BuildRewardChoiceSummaryToken(choice));
                break;
            case RewardChoiceKind.PermanentAugmentSlot:
                return false;
        }

        Profile.RunSummaries.Add(new RunSummaryRecord
        {
            RunId = Guid.NewGuid().ToString("N"),
            ExpeditionId = IsQuickBattleSmokeActive ? "quick-battle" : $"node-{CurrentExpeditionNodeIndex}",
            Result = LastBattleVictory ? "victory" : "defeat",
            GoldEarned = choice.Kind == RewardChoiceKind.Gold ? choice.GoldAmount : 0,
            NodesCleared = CurrentExpeditionNodeIndex + 1,
            CompletedAtUtc = DateTime.UtcNow.ToString("O"),
            ChapterId = ActiveRun?.Overlay.ChapterId ?? Profile.CampaignProgress.SelectedChapterId,
            SiteId = ActiveRun?.Overlay.SiteId ?? Profile.CampaignProgress.SelectedSiteId,
        });

        AppendRuntimeTelemetry(RuntimeOperationalTelemetry.CreateRewardOptionChosen(
            ResolveTelemetryRunId(),
            rewardSourceId,
            choice.PayloadId,
            Profile.Currencies.Gold,
            Profile.Currencies.Echo));
        AppendRuntimeTelemetry(BuildEconomySnapshot("reward_choice_applied"));
        _pendingRewardChoices.Clear();
        SyncActiveRunIfPresent();
        return true;
    }

    public string PreviewPermanentUnlockFromTemporaryAugment(string augmentId)
    {
        if (string.IsNullOrWhiteSpace(augmentId)
            || ActiveRun == null
            || !string.IsNullOrWhiteSpace(ActiveRun.Overlay.FirstSelectedTemporaryAugmentId))
        {
            return string.Empty;
        }

        return ResolvePendingPermanentUnlockId(augmentId);
    }

    private void EnsureRecruitOffers()
    {
        if (_recruitOffers.Count > 0)
        {
            return;
        }

        var snapshot = _combatContentLookup.Snapshot;
        if (snapshot.Archetypes.Count == 0)
        {
            return;
        }

        var result = RecruitPackGenerator.GeneratePack(
            snapshot.Archetypes,
            snapshot,
            ToHeroRecords(Profile).ToList(),
            ActiveRun?.Overlay.TemporaryAugmentIds ?? Array.Empty<string>(),
            ToPermanentAugmentLoadout(Profile, string.IsNullOrWhiteSpace(Profile.ActiveBlueprintId) ? "blueprint.default" : Profile.ActiveBlueprintId).EquippedAugmentIds,
            _recruitPityState.Clone(),
            _recruitPhaseState.Clone(),
            BuildStableSeed("recruit-pack", _recruitOfferGeneration + Profile.Heroes.Count));
        _recruitOffers.Clear();
        _recruitOffers.AddRange(result.Offers);
        _recruitPityState = result.UpdatedPity;
        _recruitPhaseState = result.UpdatedPhase;
        AppendRuntimeTelemetry(MetaTelemetryRecorder.CreateRecruitPackGenerated(
            ResolveTelemetryRunId(),
            BuildStableSeed("recruit-pack", _recruitOfferGeneration + Profile.Heroes.Count),
            result.Offers.Count,
            _recruitPhaseState));
        SyncRecruitState();
    }

    private void ResetRecruitPhaseForTownEntry()
    {
        _recruitPhaseState = new RecruitPhaseState();
        _recruitOfferGeneration = 0;
        _recruitOffers.Clear();
        EnsureRecruitOffers();
        SyncRecruitState();
    }

    private TeamPlanProfile BuildTeamPlanProfile()
    {
        var snapshot = _combatContentLookup.Snapshot;
        var permanentAugments = ToPermanentAugmentLoadout(
                Profile,
                string.IsNullOrWhiteSpace(Profile.ActiveBlueprintId) ? "blueprint.default" : Profile.ActiveBlueprintId)
            .EquippedAugmentIds;
        return TeamPlanEvaluator.Evaluate(
            ToHeroRecords(Profile).ToList(),
            snapshot.Archetypes,
            snapshot,
            ActiveRun?.Overlay.TemporaryAugmentIds ?? Array.Empty<string>(),
            permanentAugments);
    }

    private bool IsTownEconomyPhase()
    {
        return string.Equals(CurrentSceneName, SceneNames.Town, StringComparison.Ordinal);
    }

    private bool TryGrantRecruitPreview(
        RecruitUnitPreview preview,
        RecruitOfferSource source,
        out DuplicateConversionResult? duplicateResult,
        out string error)
    {
        duplicateResult = null;
        error = string.Empty;
        if (!_combatContentLookup.TryGetArchetype(preview.UnitBlueprintId, out var archetype))
        {
            error = $"Archetype '{preview.UnitBlueprintId}'를 찾을 수 없습니다.";
            return false;
        }

        if (DuplicateResolver.TryResolveDuplicate(
                Profile.Heroes.Any(hero => string.Equals(hero.ArchetypeId, preview.UnitBlueprintId, StringComparison.Ordinal)),
                preview.Metadata.Tier,
                RecruitmentBalanceCatalog.DefaultDuplicateEchoValues,
                out var duplicate))
        {
            Profile.Currencies.Echo += duplicate.EchoGranted;
            duplicateResult = duplicate;
            _lastDuplicateConversion = duplicate;
            AppendRuntimeTelemetry(MetaTelemetryRecorder.CreateDuplicateConverted(
                ResolveTelemetryRunId(),
                preview,
                duplicate));
            SyncRecruitState();
            return true;
        }

        var heroId = $"hero-{Guid.NewGuid():N}";
        Profile.Heroes.Add(new HeroInstanceRecord
        {
            HeroId = heroId,
            Name = ResolveArchetypeDisplayName(archetype),
            ArchetypeId = preview.UnitBlueprintId,
            RaceId = archetype.Race.Id,
            ClassId = archetype.Class.Id,
            PositiveTraitId = _combatContentLookup.NormalizePositiveTraitId(preview.UnitBlueprintId, string.Empty, Profile.Heroes.Count),
            NegativeTraitId = _combatContentLookup.NormalizeNegativeTraitId(preview.UnitBlueprintId, string.Empty, Profile.Heroes.Count + 1),
            FlexActiveId = preview.FlexActiveId,
            FlexPassiveId = preview.FlexPassiveId,
            RecruitTier = preview.Metadata.Tier,
            RecruitSource = source,
            RetrainState = new UnitRetrainState(),
            EconomyFootprint = new UnitEconomyFootprint
            {
                RecruitGoldPaid = source == RecruitOfferSource.RecruitPhase ? preview.Metadata.GoldCost : 0,
            },
            EquippedItemIds = new List<string>(),
        });

        Roster = new RosterState(ToHeroRecords(Profile));
        EnsureProfileBuildState();
        _lastDuplicateConversion = null;
        SyncActiveRunIfPresent();
        return true;
    }

    private bool TryGetHero(string heroId, out HeroInstanceRecord hero)
    {
        hero = Profile.Heroes.FirstOrDefault(entry => entry.HeroId == heroId)!;
        return hero != null;
    }

    private static int BuildStableSeed(string value, int salt)
    {
        return Math.Abs(HashCode.Combine(value, salt));
    }

    public void ClearRuntimeTelemetry()
    {
        _runtimeTelemetryEvents.Clear();
    }

    private void AppendRuntimeTelemetry(TelemetryEventRecord record)
    {
        if (record == null)
        {
            return;
        }

        record.TimeSeconds = _runtimeTelemetryEvents.Count;
        _runtimeTelemetryEvents.Add(record);
    }

    internal void RecordOperationalTelemetry(TelemetryEventRecord record)
    {
        AppendRuntimeTelemetry(record);
    }

    private string ResolveTelemetryRunId()
    {
        return ActiveRun?.RunId
               ?? Profile.ActiveRun?.RunId
               ?? (IsQuickBattleSmokeActive ? "quick-battle" : GetExpeditionRunId());
    }

    private static string ResolveHeroFlexActiveId(HeroInstanceRecord hero, CombatArchetypeTemplate archetype)
    {
        return string.IsNullOrWhiteSpace(hero.FlexActiveId)
            ? archetype.FlexActive?.Id ?? string.Empty
            : hero.FlexActiveId;
    }

    private static string ResolveHeroFlexPassiveId(HeroInstanceRecord hero, CombatArchetypeTemplate archetype)
    {
        return string.IsNullOrWhiteSpace(hero.FlexPassiveId)
            ? archetype.FlexPassive?.Id ?? string.Empty
            : hero.FlexPassiveId;
    }

    private void SyncHeroBuildState(HeroInstanceRecord hero)
    {
        var loadout = Profile.HeroLoadouts.FirstOrDefault(record => record.HeroId == hero.HeroId);
        if (loadout == null)
        {
            Profile.HeroLoadouts.Add(new HeroLoadoutRecord
            {
                HeroId = hero.HeroId,
                EquippedItemInstanceIds = hero.EquippedItemIds.ToList(),
            });
        }
        else
        {
            loadout.EquippedItemInstanceIds = hero.EquippedItemIds.ToList();
        }
    }

    private void UnequipHeroItems(string heroId)
    {
        foreach (var inventoryItem in Profile.Inventory.Where(item => string.Equals(item.EquippedHeroId, heroId, StringComparison.Ordinal)))
        {
            inventoryItem.EquippedHeroId = string.Empty;
        }

        var hero = Profile.Heroes.FirstOrDefault(entry => entry.HeroId == heroId);
        if (hero != null)
        {
            hero.EquippedItemIds = new List<string>();
        }
    }

    private void RemoveHeroFromRoster(string heroId)
    {
        var removedLoadout = Profile.HeroLoadouts.FirstOrDefault(record => string.Equals(record.HeroId, heroId, StringComparison.Ordinal));
        if (removedLoadout != null)
        {
            var removedSkillIds = removedLoadout.EquippedSkillInstanceIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            Profile.SkillInstances.RemoveAll(record => removedSkillIds.Contains(record.SkillInstanceId));
        }

        Profile.Heroes.RemoveAll(hero => string.Equals(hero.HeroId, heroId, StringComparison.Ordinal));
        Profile.HeroLoadouts.RemoveAll(record => string.Equals(record.HeroId, heroId, StringComparison.Ordinal));
        Profile.HeroProgressions.RemoveAll(record => string.Equals(record.HeroId, heroId, StringComparison.Ordinal));
        Profile.PassiveSelections.RemoveAll(record => string.Equals(record.HeroId, heroId, StringComparison.Ordinal));
        _expeditionSquadHeroIds.RemoveAll(id => string.Equals(id, heroId, StringComparison.Ordinal));
        ClearDeploymentForHero(heroId);
        EnsureBattleDeployReady();
    }

    private void EnsureDefaultSquad()
    {
        if (_expeditionSquadHeroIds.Count > 0)
        {
            return;
        }

        foreach (var hero in Profile.Heroes.Take(MetaBalanceDefaults.ExpeditionSquadCap))
        {
            _expeditionSquadHeroIds.Add(hero.HeroId);
        }
    }

    private void EnsureAssignmentMapInitialized()
    {
        foreach (var anchor in DeploymentAnchorOrder)
        {
            if (!_deploymentAssignments.ContainsKey(anchor))
            {
                _deploymentAssignments[anchor] = null;
            }
        }
    }

    private void ResetDeploymentAssignments()
    {
        _deploymentAssignments.Clear();
        EnsureAssignmentMapInitialized();
    }

    private void EnsureDefaultDeploymentAssignments()
    {
        EnsureAssignmentMapInitialized();

        foreach (var anchor in DeploymentAnchorOrder)
        {
            if (_deploymentAssignments.TryGetValue(anchor, out var heroId) && !string.IsNullOrWhiteSpace(heroId) && !_expeditionSquadHeroIds.Contains(heroId))
            {
                _deploymentAssignments[anchor] = null;
            }
        }

        foreach (var heroId in BattleDeployHeroIds.Where(heroId => !_expeditionSquadHeroIds.Contains(heroId)).ToList())
        {
            ClearDeploymentForHero(heroId);
        }

        foreach (var heroId in _expeditionSquadHeroIds.Take(MetaBalanceDefaults.BattleDeployCap))
        {
            if (BattleDeployHeroIds.Contains(heroId))
            {
                continue;
            }

            AssignHeroToAnchor(ResolvePreferredAnchor(heroId), heroId);
            if (BattleDeployHeroIds.Count >= MetaBalanceDefaults.BattleDeployCap)
            {
                break;
            }
        }
    }

    private void ClearDeploymentForHero(string heroId)
    {
        foreach (var anchor in DeploymentAnchorOrder)
        {
            if (_deploymentAssignments.TryGetValue(anchor, out var assignedHero) && assignedHero == heroId)
            {
                _deploymentAssignments[anchor] = null;
            }
        }
    }

    private DeploymentAnchorId ResolvePreferredAnchor(string heroId)
    {
        var hero = Profile.Heroes.FirstOrDefault(entry => entry.HeroId == heroId);
        var preferredOrder = hero?.ClassId switch
        {
            "vanguard" => new[]
            {
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontBottom,
                DeploymentAnchorId.BackCenter,
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackBottom
            },
            "duelist" => new[]
            {
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontBottom,
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackBottom,
                DeploymentAnchorId.BackCenter
            },
            "ranger" => new[]
            {
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackCenter,
                DeploymentAnchorId.BackBottom,
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontBottom
            },
            "mystic" => new[]
            {
                DeploymentAnchorId.BackCenter,
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackBottom,
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontBottom
            },
            _ => DeploymentAnchorOrder
        };

        foreach (var anchor in preferredOrder)
        {
            if (string.IsNullOrWhiteSpace(GetAssignedHeroId(anchor)))
            {
                return anchor;
            }
        }

        return preferredOrder[0];
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
            case SM.Content.Definitions.RewardType.Gold:
                Profile.Currencies.Gold += entry.Amount;
                summaryParts.Add($"gold +{entry.Amount}");
                break;
            case SM.Content.Definitions.RewardType.Echo:
                Profile.Currencies.Echo += entry.Amount;
                summaryParts.Add($"echo +{entry.Amount}");
                break;
            case SM.Content.Definitions.RewardType.TemporaryAugment:
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
            case SM.Content.Definitions.RewardType.Item:
            case SM.Content.Definitions.RewardType.SkillManual:
            case SM.Content.Definitions.RewardType.SkillShard:
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

    private void SeedDemoProfile()
    {
        Profile.DisplayName = "Demo Player";
        Profile.Currencies = new CurrencyRecord { Gold = 12, Echo = 45 };
        Profile.UnlockedPermanentAugmentIds = new List<string>();
        Profile.Inventory = new List<InventoryItemRecord>();
        Profile.Heroes.Clear();

        var archetypeIds = _combatContentLookup.GetCanonicalArchetypeIds();
        var itemIds = _combatContentLookup.GetCanonicalItemIds();
        var affixIds = _combatContentLookup.GetCanonicalAffixIds();
        for (var i = 0; i < Math.Min(MetaBalanceDefaults.ExpeditionSquadCap, archetypeIds.Count); i++)
        {
            var archetypeId = archetypeIds[i];
            _combatContentLookup.TryGetArchetype(archetypeId, out var archetype);
            var heroId = $"hero-{i + 1}";
            var equippedItems = new List<string>();
            if (itemIds.Count > 0 && i < 4)
            {
                var itemInstanceId = $"demo-item-{i + 1}";
                Profile.Inventory.Add(new InventoryItemRecord
                {
                    ItemInstanceId = itemInstanceId,
                    ItemBaseId = itemIds[i % itemIds.Count],
                    EquippedHeroId = heroId,
                    AffixIds = affixIds.Count == 0
                        ? new List<string>()
                        : new List<string> { affixIds[i % affixIds.Count] }
                });
                equippedItems.Add(itemInstanceId);
            }

            Profile.Heroes.Add(new HeroInstanceRecord
            {
                HeroId = heroId,
                Name = archetype != null ? ResolveArchetypeDisplayName(archetype) : $"Hero {i + 1}",
                ArchetypeId = archetypeId,
                RaceId = archetype?.Race.Id ?? string.Empty,
                ClassId = archetype?.Class.Id ?? string.Empty,
                PositiveTraitId = _combatContentLookup.NormalizePositiveTraitId(archetypeId, string.Empty, i),
                NegativeTraitId = _combatContentLookup.NormalizeNegativeTraitId(archetypeId, string.Empty, i + 1),
                FlexActiveId = archetype?.Loadout?.FlexActive?.Id ?? string.Empty,
                FlexPassiveId = archetype?.Loadout?.FlexPassive?.Id ?? string.Empty,
                RecruitTier = archetype?.RecruitTier ?? RecruitTier.Common,
                RecruitSource = RecruitOfferSource.DirectGrant,
                RetrainState = new UnitRetrainState(),
                EconomyFootprint = new UnitEconomyFootprint(),
                EquippedItemIds = equippedItems
            });
        }
    }

    private IReadOnlyList<BattleEquippedItemSpec> BuildEquippedItemSpecs(
        HeroInstanceRecord hero,
        IReadOnlyDictionary<string, InventoryItemRecord> inventoryByInstanceId)
    {
        var instanceIds = new HashSet<string>(hero.EquippedItemIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        foreach (var inventoryItem in Profile.Inventory.Where(item => item.EquippedHeroId == hero.HeroId))
        {
            instanceIds.Add(inventoryItem.ItemInstanceId);
        }

        return instanceIds
            .Where(inventoryByInstanceId.ContainsKey)
            .Select(id => inventoryByInstanceId[id])
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemBaseId))
            .Select(item => new BattleEquippedItemSpec(
                item.ItemBaseId,
                item.AffixIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList()))
            .ToList();
    }

    private void NormalizeProfileContentIds()
    {
        NormalizeHeroContentIds();
        NormalizeInventoryContentIds();
        NormalizeExpeditionContentIds();
        NormalizeEquippedItemReferences();
        NormalizePermanentAugmentUnlockIds();
        NormalizeBuildStateRecords();
    }

    private void NormalizeHeroContentIds()
    {
        for (var i = 0; i < Profile.Heroes.Count; i++)
        {
            var hero = Profile.Heroes[i];
            hero.EquippedItemIds ??= new List<string>();
            hero.RetrainState ??= new UnitRetrainState();
            hero.EconomyFootprint ??= new UnitEconomyFootprint();
            hero.ArchetypeId = _combatContentLookup.NormalizeArchetypeId(hero.ArchetypeId, hero.RaceId, hero.ClassId, i);
            if (_combatContentLookup.TryGetArchetype(hero.ArchetypeId, out var archetype))
            {
                hero.RaceId = archetype.Race.Id;
                hero.ClassId = archetype.Class.Id;
                hero.FlexActiveId = string.IsNullOrWhiteSpace(hero.FlexActiveId)
                    ? archetype.Loadout?.FlexActive?.Id ?? string.Empty
                    : hero.FlexActiveId;
                hero.FlexPassiveId = string.IsNullOrWhiteSpace(hero.FlexPassiveId)
                    ? archetype.Loadout?.FlexPassive?.Id ?? string.Empty
                    : hero.FlexPassiveId;
                hero.RecruitTier = archetype.RecruitTier;
            }

            hero.CharacterId = NormalizeCharacterId(hero.CharacterId, hero.ArchetypeId);
            if (_combatContentLookup.TryGetCharacterDefinition(hero.CharacterId, out var character))
            {
                if (character.Race != null)
                {
                    hero.RaceId = character.Race.Id;
                }

                if (character.Class != null)
                {
                    hero.ClassId = character.Class.Id;
                }
            }

            hero.PositiveTraitId = _combatContentLookup.NormalizePositiveTraitId(hero.ArchetypeId, hero.PositiveTraitId, i);
            hero.NegativeTraitId = _combatContentLookup.NormalizeNegativeTraitId(hero.ArchetypeId, hero.NegativeTraitId, i + 1);
            hero.EquippedItemIds = hero.EquippedItemIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }

    private void NormalizeInventoryContentIds()
    {
        for (var i = 0; i < Profile.Inventory.Count; i++)
        {
            var item = Profile.Inventory[i];
            item.AffixIds ??= new List<string>();
            if (string.IsNullOrWhiteSpace(item.ItemInstanceId))
            {
                item.ItemInstanceId = $"inventory-{Guid.NewGuid():N}";
            }

            item.ItemBaseId = _combatContentLookup.NormalizeItemBaseId(item.ItemBaseId, i);
            item.AffixIds = item.AffixIds
                .Select((affixId, affixIndex) => _combatContentLookup.NormalizeAffixId(affixId, i + affixIndex))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }

    private void NormalizeExpeditionContentIds()
    {
        var normalizedAugments = Expedition.TemporaryAugmentIds
                .Select((augmentId, index) => _combatContentLookup.NormalizeTemporaryAugmentId(augmentId, index))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        Expedition = new ExpeditionState(Expedition.CurrentNodeIndex, normalizedAugments);
    }

    private void NormalizeEquippedItemReferences()
    {
        var inventoryById = Profile.Inventory
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemInstanceId))
            .GroupBy(item => item.ItemInstanceId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var heroIds = Profile.Heroes.Select(hero => hero.HeroId).ToHashSet(StringComparer.Ordinal);

        foreach (var inventoryItem in Profile.Inventory)
        {
            if (!string.IsNullOrWhiteSpace(inventoryItem.EquippedHeroId) && !heroIds.Contains(inventoryItem.EquippedHeroId))
            {
                inventoryItem.EquippedHeroId = string.Empty;
            }
        }

        foreach (var hero in Profile.Heroes)
        {
            var equippedIds = new HashSet<string>(hero.EquippedItemIds.Where(inventoryById.ContainsKey), StringComparer.Ordinal);
            foreach (var inventoryItem in Profile.Inventory.Where(item => item.EquippedHeroId == hero.HeroId))
            {
                equippedIds.Add(inventoryItem.ItemInstanceId);
            }

            hero.EquippedItemIds = equippedIds.ToList();
            foreach (var equippedId in equippedIds)
            {
                inventoryById[equippedId].EquippedHeroId = hero.HeroId;
            }
        }
    }

    private string ResolveRewardItemId(int index)
    {
        return _combatContentLookup.NormalizeItemBaseId(string.Empty, index);
    }

    private static string ResolveArchetypeDisplayName(UnitArchetypeDefinition archetype)
    {
        if (!string.IsNullOrWhiteSpace(archetype.LegacyDisplayName))
        {
            return archetype.LegacyDisplayName;
        }

        if (!string.IsNullOrWhiteSpace(archetype.NameKey))
        {
            return archetype.NameKey;
        }

        return archetype.Id;
    }

    private string ResolveRewardAugmentId(int index, params string[] preferredAugmentIds)
    {
        foreach (var preferredAugmentId in preferredAugmentIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (_combatContentLookup.TryGetAugmentDefinition(preferredAugmentId, out var augment) && !augment.IsPermanent)
            {
                return preferredAugmentId;
            }
        }

        return _combatContentLookup.NormalizeTemporaryAugmentId(string.Empty, index);
    }

    private string ResolvePendingPermanentUnlockId(string temporaryAugmentId)
    {
        var definitions = BuildPermanentProgressionAugmentDefinitions(temporaryAugmentId);
        var resolution = PermanentAugmentProgressionService.ResolvePendingUnlock(
            temporaryAugmentId,
            definitions,
            Profile.UnlockedPermanentAugmentIds);
        return resolution.HasUnlock ? resolution.UnlockAugmentId : string.Empty;
    }

    private IReadOnlyList<AugmentDefinition> BuildPermanentProgressionAugmentDefinitions(params string[] explicitAugmentIds)
    {
        var augmentIds = explicitAugmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Concat(_combatContentLookup.GetCanonicalTemporaryAugmentIds())
            .Concat(_combatContentLookup.GetCanonicalPermanentAugmentIds())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal);
        var definitions = new List<AugmentDefinition>();
        foreach (var augmentId in augmentIds)
        {
            if (_combatContentLookup.TryGetAugmentDefinition(augmentId, out var augment))
            {
                definitions.Add(augment);
            }
        }

        return definitions;
    }

    private void TrackPermanentAugmentProgression(string temporaryAugmentId)
    {
        if (ActiveRun == null || string.IsNullOrWhiteSpace(temporaryAugmentId))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(ActiveRun.Overlay.FirstSelectedTemporaryAugmentId))
        {
            return;
        }

        ActiveRun = ActiveRun with
        {
            Overlay = ActiveRun.Overlay with
            {
                FirstSelectedTemporaryAugmentId = temporaryAugmentId,
                PendingPermanentUnlockId = ResolvePendingPermanentUnlockId(temporaryAugmentId),
            }
        };
    }

    private void ConsumePendingPermanentUnlock()
    {
        LastPermanentUnlockSummary = SessionTextToken.Empty;
        if (ActiveRun == null || string.IsNullOrWhiteSpace(ActiveRun.Overlay.PendingPermanentUnlockId))
        {
            return;
        }

        var unlockAugmentId = ActiveRun.Overlay.PendingPermanentUnlockId;
        var unlockResult = UnlockPermanentAugmentCandidate(unlockAugmentId);
        if (!unlockResult.IsSuccess)
        {
            ActiveRun = ActiveRun with
            {
                Overlay = ActiveRun.Overlay with
                {
                    PendingPermanentUnlockId = string.Empty,
                }
            };
            SyncActiveRunRecord();
            return;
        }

        LastPermanentUnlockSummary = new SessionTextToken(
            GameLocalizationTables.UIReward,
            "ui.reward.summary.permanent_unlock",
            "Permanent candidate unlocked: {0}",
            SessionTextArg.AugmentName(unlockAugmentId));
        ActiveRun = ActiveRun with
        {
            Overlay = ActiveRun.Overlay with
            {
                PendingPermanentUnlockId = string.Empty,
            }
        };
        SyncActiveRunRecord();
    }

    private IReadOnlyDictionary<string, PassiveNodeDefinition> BuildPassiveBoardNodeDictionary(string boardId)
    {
        if (string.IsNullOrWhiteSpace(boardId)
            || !_combatContentLookup.TryGetPassiveBoardDefinition(boardId, out var board))
        {
            return new Dictionary<string, PassiveNodeDefinition>(StringComparer.Ordinal);
        }

        return board.Nodes
            .Where(node => node != null
                           && !string.IsNullOrWhiteSpace(node.Id)
                           && string.Equals(node.BoardId, boardId, StringComparison.Ordinal))
            .ToDictionary(node => node.Id, node => node, StringComparer.Ordinal);
    }

    private static bool IsLegacyPermanentSlotToken(string id)
    {
        return id.StartsWith("perm-slot", StringComparison.Ordinal)
               || id.StartsWith("perm-slot.", StringComparison.Ordinal);
    }

    private static IEnumerable<HeroRecord> ToHeroRecords(SaveProfile profile)
    {
        foreach (var hero in profile.Heroes)
        {
            yield return new HeroRecord(
                hero.HeroId,
                hero.Name,
                hero.ArchetypeId,
                hero.RaceId,
                hero.ClassId,
                hero.PositiveTraitId,
                hero.NegativeTraitId,
                hero.FlexActiveId,
                hero.FlexPassiveId,
                hero.RecruitTier,
                hero.RecruitSource,
                hero.RetrainState?.Clone() ?? new UnitRetrainState(),
                hero.EconomyFootprint?.Clone() ?? new UnitEconomyFootprint(),
                hero.CharacterId);
        }
    }

    private void EnsureProfileBuildState()
    {
        foreach (var hero in Profile.Heroes)
        {
            if (Profile.HeroLoadouts.All(record => record.HeroId != hero.HeroId))
            {
                Profile.HeroLoadouts.Add(new HeroLoadoutRecord
                {
                    HeroId = hero.HeroId,
                    EquippedItemInstanceIds = hero.EquippedItemIds.ToList(),
                });
            }

            if (Profile.HeroProgressions.All(record => record.HeroId != hero.HeroId))
            {
                Profile.HeroProgressions.Add(new HeroProgressionRecord { HeroId = hero.HeroId, Level = 1 });
            }

            if (Profile.PassiveSelections.All(record => record.HeroId != hero.HeroId))
            {
                Profile.PassiveSelections.Add(new PassiveSelectionRecord { HeroId = hero.HeroId });
            }
        }

        if (Profile.PermanentAugmentLoadouts.All(record => record.BlueprintId != Profile.ActiveBlueprintId))
        {
            Profile.PermanentAugmentLoadouts.Add(new PermanentAugmentLoadoutRecord
            {
                BlueprintId = Profile.ActiveBlueprintId,
                EquippedAugmentIds = new List<string>()
            });
        }

        if (Profile.SquadBlueprints.All(record => record.BlueprintId != Profile.ActiveBlueprintId))
        {
            CaptureBlueprintState();
        }
    }

    private void NormalizeBuildStateRecords()
    {
        Profile.ActiveRun ??= new ActiveRunRecord();
        Profile.ActiveRun.RecruitPhase ??= new RecruitPhaseState();
        Profile.ActiveRun.RecruitPity ??= new RecruitPityState();

        foreach (var loadout in Profile.HeroLoadouts)
        {
            loadout.EquippedItemInstanceIds ??= new List<string>();
            loadout.EquippedSkillInstanceIds ??= new List<string>();
            loadout.SelectedPassiveNodeIds ??= new List<string>();
            loadout.EquippedPermanentAugmentIds ??= new List<string>();
        }

        foreach (var progression in Profile.HeroProgressions)
        {
            progression.UnlockedPassiveNodeIds ??= new List<string>();
            progression.UnlockedSkillIds ??= new List<string>();
        }

        foreach (var skillInstance in Profile.SkillInstances)
        {
            skillInstance.CompileTags ??= new List<string>();
        }

        foreach (var selection in Profile.PassiveSelections)
        {
            selection.SelectedNodeIds ??= new List<string>();
        }

        foreach (var loadout in Profile.PermanentAugmentLoadouts)
        {
            loadout.EquippedAugmentIds ??= new List<string>();
        }

        foreach (var blueprint in Profile.SquadBlueprints)
        {
            blueprint.DeploymentAssignments ??= new Dictionary<string, string>();
            blueprint.ExpeditionSquadHeroIds ??= new List<string>();
            blueprint.HeroRoleIds ??= new Dictionary<string, string>();
        }

        NormalizePermanentAugmentState();
        NormalizePassiveBoardStates();
    }

    private void NormalizePermanentAugmentUnlockIds()
    {
        Profile.UnlockedPermanentAugmentIds = Profile.UnlockedPermanentAugmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Where(id => !IsLegacyPermanentSlotToken(id))
            .Where(id => !_combatContentLookup.TryGetAugmentDefinition(id, out var augment) || augment.IsPermanent)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private void NormalizePermanentAugmentState()
    {
        foreach (var loadout in Profile.PermanentAugmentLoadouts)
        {
            loadout.EquippedAugmentIds = loadout.EquippedAugmentIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Where(id => !IsLegacyPermanentSlotToken(id))
                .Where(id => !_combatContentLookup.TryGetAugmentDefinition(id, out var augment) || augment.IsPermanent)
                .Distinct(StringComparer.Ordinal)
                .Take(MetaBalanceDefaults.MaxPermanentAugmentSlots)
                .ToList();

            foreach (var equippedAugmentId in loadout.EquippedAugmentIds)
            {
                if (!Profile.UnlockedPermanentAugmentIds.Contains(equippedAugmentId, StringComparer.Ordinal))
                {
                    Profile.UnlockedPermanentAugmentIds.Add(equippedAugmentId);
                }
            }
        }

        PermanentAugmentSlotCount = MetaBalanceDefaults.MaxPermanentAugmentSlots;
    }

    private void NormalizePassiveBoardStates()
    {
        foreach (var loadout in Profile.HeroLoadouts)
        {
            loadout.SelectedPassiveNodeIds ??= new List<string>();
            if (string.IsNullOrWhiteSpace(loadout.PassiveBoardId)
                || !_combatContentLookup.TryGetPassiveBoardDefinition(loadout.PassiveBoardId, out _))
            {
                loadout.PassiveBoardId = string.Empty;
                loadout.SelectedPassiveNodeIds = new List<string>();
                continue;
            }

            var nodesById = BuildPassiveBoardNodeDictionary(loadout.PassiveBoardId);
            var normalized = PassiveBoardSelectionValidator.Normalize(
                loadout.PassiveBoardId,
                loadout.SelectedPassiveNodeIds,
                nodesById);
            loadout.SelectedPassiveNodeIds = normalized.NormalizedNodeIds.ToList();
        }

        foreach (var selection in Profile.PassiveSelections)
        {
            selection.SelectedNodeIds ??= new List<string>();
            var loadout = Profile.HeroLoadouts.FirstOrDefault(record =>
                string.Equals(record.HeroId, selection.HeroId, StringComparison.Ordinal));
            if (loadout == null)
            {
                selection.BoardId = string.Empty;
                selection.SelectedNodeIds = new List<string>();
                continue;
            }

            selection.BoardId = loadout.PassiveBoardId;
            selection.SelectedNodeIds = loadout.SelectedPassiveNodeIds.ToList();
        }
    }

    private void RestoreActiveRunFromProfile()
    {
        RestoreRecruitStates();
        if (Profile.ActiveRun == null || string.IsNullOrWhiteSpace(Profile.ActiveRun.RunId))
        {
            ActiveRun = null;
            return;
        }

        var blueprint = TryGetBlueprintState(Profile.ActiveRun.BlueprintId) ?? CaptureBlueprintState();
        ActiveRun = new ActiveRunState(
            Profile.ActiveRun.RunId,
            Profile.ActiveRun.ExpeditionId,
            blueprint,
            new RunOverlayState(
                Profile.ActiveRun.CurrentNodeIndex,
                Profile.ActiveRun.TemporaryAugmentIds,
                Profile.ActiveRun.PendingRewardIds,
                Profile.ActiveRun.CompileVersion,
                Profile.ActiveRun.CompileHash,
                Profile.ActiveRun.RecruitPhase?.Clone() ?? new RecruitPhaseState(),
                Profile.ActiveRun.RecruitPity?.Clone() ?? new RecruitPityState(),
                Profile.ActiveRun.ChapterId,
                Profile.ActiveRun.SiteId,
                Profile.ActiveRun.SiteNodeIndex,
                Profile.ActiveRun.EncounterId,
                Profile.ActiveRun.BattleSeed,
                Profile.ActiveRun.BattleContextHash,
                Profile.ActiveRun.RewardSourceId,
                Profile.ActiveRun.FirstSelectedTemporaryAugmentId,
                Profile.ActiveRun.PendingPermanentUnlockId),
            Profile.ActiveRun.BattleDeployHeroIds,
            Profile.ActiveRun.IsQuickBattle,
            string.IsNullOrWhiteSpace(Profile.ActiveRun.LastBattleMatchId) ? null : Profile.ActiveRun.LastBattleMatchId,
            Profile.ActiveRun.LastSettlementWasVictory,
            Profile.ActiveRun.StoryCleared,
            Profile.ActiveRun.EndlessUnlocked);
        LastBattleVictory = ActiveRun.LastSettlementWasVictory;
        IsQuickBattleSmokeActive = ActiveRun.IsQuickBattle;
        QuickBattleLaneKind = ActiveRun.IsQuickBattle
            ? CombatSandboxLaneKind.TownIntegrationSmoke
            : CombatSandboxLaneKind.None;
        HasActiveExpeditionRun = !ActiveRun.IsQuickBattle;
        CurrentExpeditionNodeIndex = ActiveRun.Overlay.CurrentNodeIndex;

        var resumedRewardSourceId = ActiveRun.Overlay.RewardSourceId;
        if (TryResumeRecoveredRewardSettlement())
        {
            AppendRuntimeTelemetry(RuntimeOperationalTelemetry.CreateRewardSettlementResumed(
                ResolveTelemetryRunId(),
                resumedRewardSourceId));
        }

        _hasPendingRewardSettlement = ActiveRun?.Overlay.PendingRewardIds.Count > 0;
        SelectedTeamPosture = blueprint.TeamPosture;
        SelectedTeamTacticId = blueprint.TeamTacticId;
        RestoreResolvedProgressMarkers(includeCurrentNode: _hasPendingRewardSettlement);
        if (_hasPendingRewardSettlement)
        {
            SelectedExpeditionNodeIndex = CurrentExpeditionNodeIndex;
        }
        else
        {
            AutoSelectNextExpeditionNode();
        }
    }

    private bool TryResumeRecoveredRewardSettlement()
    {
        if (ActiveRun == null
            || string.IsNullOrWhiteSpace(ActiveRun.Overlay.RewardSourceId)
            || ActiveRun.Overlay.PendingRewardIds.Count > 0
            || !HasRecordedRewardSettlement(ActiveRun.Overlay.RewardSourceId))
        {
            return false;
        }

        _hasPendingRewardSettlement = true;
        LastRewardApplicationSummary = new SessionTextToken(
            GameLocalizationTables.UIReward,
            "ui.reward.status.recovered_choice",
            "Recovered previous reward settlement.");
        FinalizeRewardSettlement();
        return true;
    }

    private bool HasRecordedRewardSettlement(string sourceId)
    {
        return !string.IsNullOrWhiteSpace(sourceId)
               && Profile.RewardLedger.Any(entry =>
                   string.Equals(entry.SourceId, sourceId, StringComparison.Ordinal)
                   && entry.SourceKind.EndsWith(":reward_choice", StringComparison.Ordinal));
    }

    private TelemetryEventRecord BuildEconomySnapshot(string label)
    {
        return RuntimeOperationalTelemetry.CreateEconomySnapshot(
            ResolveTelemetryRunId(),
            label,
            Profile.Currencies.Gold,
            Profile.Currencies.Echo,
            _pendingRewardChoices.Count,
            !LastBattleVictory);
    }

    private void SyncActiveRunIfPresent()
    {
        if (ActiveRun == null)
        {
            return;
        }

        var compileHash = LastCompiledBattleSnapshot?.CompileHash ?? ActiveRun.Overlay.LastCompileHash;
        var blueprint = IsQuickBattleSmokeActive && _compiledQuickBattleScenario != null
            ? _compiledQuickBattleScenario.LeftTeam.Blueprint
            : CaptureBlueprintState();
        ActiveRun = RunStateService.SyncBlueprint(
            ActiveRun,
            blueprint,
            compileHash,
            _pendingRewardChoices.Select(choice => choice.PayloadId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList());
        SyncActiveRunRecord();
        SyncExpeditionState();
    }

    private void SyncActiveRunRecord()
    {
        if (ActiveRun == null)
        {
            Profile.ActiveRun = new ActiveRunRecord
            {
                RecruitPhase = _recruitPhaseState.Clone(),
                RecruitPity = _recruitPityState.Clone(),
            };
            return;
        }

        Profile.ActiveRun = new ActiveRunRecord
        {
            RunId = ActiveRun.RunId,
            ExpeditionId = ActiveRun.ExpeditionId,
            BlueprintId = ActiveRun.Blueprint.BlueprintId,
            IsQuickBattle = ActiveRun.IsQuickBattle,
            CurrentNodeIndex = ActiveRun.Overlay.CurrentNodeIndex,
            TemporaryAugmentIds = ActiveRun.Overlay.TemporaryAugmentIds.ToList(),
            PendingRewardIds = ActiveRun.Overlay.PendingRewardIds.ToList(),
            BattleDeployHeroIds = ActiveRun.BattleDeployHeroIds.ToList(),
            RecruitPhase = _recruitPhaseState.Clone(),
            RecruitPity = _recruitPityState.Clone(),
            CompileVersion = ActiveRun.Overlay.CompileVersion,
            CompileHash = ActiveRun.Overlay.LastCompileHash,
            LastBattleMatchId = ActiveRun.LastBattleMatchId ?? string.Empty,
            LastSettlementWasVictory = ActiveRun.LastSettlementWasVictory,
            ChapterId = ActiveRun.Overlay.ChapterId,
            SiteId = ActiveRun.Overlay.SiteId,
            SiteNodeIndex = ActiveRun.Overlay.SiteNodeIndex,
            EncounterId = ActiveRun.Overlay.EncounterId,
            BattleSeed = ActiveRun.Overlay.BattleSeed,
            BattleContextHash = ActiveRun.Overlay.BattleContextHash,
            RewardSourceId = ActiveRun.Overlay.RewardSourceId,
            FirstSelectedTemporaryAugmentId = ActiveRun.Overlay.FirstSelectedTemporaryAugmentId,
            PendingPermanentUnlockId = ActiveRun.Overlay.PendingPermanentUnlockId,
            StoryCleared = ActiveRun.StoryCleared,
            EndlessUnlocked = ActiveRun.EndlessUnlocked,
        };
    }

    private void RestoreRecruitStates()
    {
        _recruitPhaseState = Profile.ActiveRun?.RecruitPhase?.Clone() ?? new RecruitPhaseState();
        _recruitPityState = Profile.ActiveRun?.RecruitPity?.Clone() ?? new RecruitPityState();
    }

    private void SyncRecruitState()
    {
        if (ActiveRun != null)
        {
            ActiveRun = ActiveRun with
            {
                Overlay = ActiveRun.Overlay with
                {
                    RecruitPhase = _recruitPhaseState.Clone(),
                    RecruitPity = _recruitPityState.Clone(),
                }
            };
        }

        SyncActiveRunRecord();
    }

    private SquadBlueprintState CaptureBlueprintState()
    {
        EnsureAssignmentMapInitialized();

        var deploymentAssignments = DeploymentAnchorOrder
            .Where(anchor => _deploymentAssignments.TryGetValue(anchor, out var heroId) && !string.IsNullOrWhiteSpace(heroId))
            .ToDictionary(anchor => anchor, anchor => _deploymentAssignments[anchor]!, EqualityComparer<DeploymentAnchorId>.Default);

        var blueprint = new SquadBlueprintState(
            string.IsNullOrWhiteSpace(Profile.ActiveBlueprintId) ? "blueprint.default" : Profile.ActiveBlueprintId,
            "Default Build",
            SelectedTeamPosture,
            SelectedTeamTacticId,
            deploymentAssignments,
            _expeditionSquadHeroIds.ToList(),
            Profile.Heroes.ToDictionary(hero => hero.HeroId, hero => ResolveBlueprintRoleInstructionId(hero.HeroId, hero.ClassId, ResolvePreferredAnchor(hero.HeroId)), StringComparer.Ordinal));

        var record = Profile.SquadBlueprints.FirstOrDefault(existing => existing.BlueprintId == blueprint.BlueprintId);
        if (record == null)
        {
            record = new SquadBlueprintRecord { BlueprintId = blueprint.BlueprintId };
            Profile.SquadBlueprints.Add(record);
        }

        record.DisplayName = blueprint.DisplayName;
        record.TeamPosture = blueprint.TeamPosture.ToString();
        record.TeamTacticId = blueprint.TeamTacticId;
        record.DeploymentAssignments = blueprint.DeploymentAssignments.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value, StringComparer.Ordinal);
        record.ExpeditionSquadHeroIds = blueprint.ExpeditionSquadHeroIds.ToList();
        record.HeroRoleIds = blueprint.HeroRoleIds.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        Profile.ActiveBlueprintId = blueprint.BlueprintId;
        return blueprint;
    }

    private SquadBlueprintState? TryGetBlueprintState(string blueprintId)
    {
        var record = Profile.SquadBlueprints.FirstOrDefault(existing => existing.BlueprintId == blueprintId);
        if (record == null)
        {
            return null;
        }

        if (!Enum.TryParse<TeamPostureType>(record.TeamPosture, out var posture))
        {
            posture = SelectedTeamPosture;
        }

        return new SquadBlueprintState(
            record.BlueprintId,
            string.IsNullOrWhiteSpace(record.DisplayName) ? "Default Build" : record.DisplayName,
            posture,
            record.TeamTacticId ?? string.Empty,
            record.DeploymentAssignments
                .Where(pair => Enum.TryParse<DeploymentAnchorId>(pair.Key, out _))
                .ToDictionary(pair => Enum.Parse<DeploymentAnchorId>(pair.Key), pair => pair.Value),
            record.ExpeditionSquadHeroIds,
            record.HeroRoleIds);
    }

    private static IReadOnlyDictionary<string, HeroLoadoutState> ToHeroLoadoutStates(SaveProfile profile)
    {
        return profile.HeroLoadouts.ToDictionary(
            record => record.HeroId,
            record => new HeroLoadoutState(
                record.HeroId,
                record.EquippedItemInstanceIds,
                record.EquippedSkillInstanceIds,
                record.PassiveBoardId,
                record.SelectedPassiveNodeIds,
                record.EquippedPermanentAugmentIds),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, HeroProgressionState> ToHeroProgressionStates(SaveProfile profile)
    {
        return profile.HeroProgressions.ToDictionary(
            record => record.HeroId,
            record => new HeroProgressionState(
                record.HeroId,
                record.Level,
                record.Experience,
                record.UnlockedPassiveNodeIds,
                record.UnlockedSkillIds),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, ItemInstanceState> ToItemInstanceStates(SaveProfile profile)
    {
        return profile.Inventory.ToDictionary(
            record => record.ItemInstanceId,
            record => new ItemInstanceState(
                record.ItemInstanceId,
                record.ItemBaseId,
                record.AffixIds,
                record.EquippedHeroId),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, SkillInstanceState> ToSkillInstanceStates(SaveProfile profile)
    {
        return profile.SkillInstances.ToDictionary(
            record => record.SkillInstanceId,
            record => new SkillInstanceState(
                record.SkillInstanceId,
                record.SkillId,
                CompiledSkillSlots.Normalize(record.SlotKind),
                record.CompileTags,
                record.ResolvedSlotKind),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, PassiveBoardSelectionState> ToPassiveSelections(SaveProfile profile)
    {
        return profile.PassiveSelections.ToDictionary(
            record => record.HeroId,
            record => new PassiveBoardSelectionState(
                record.HeroId,
                record.BoardId,
                record.SelectedNodeIds),
            StringComparer.Ordinal);
    }

    private static PermanentAugmentLoadoutState ToPermanentAugmentLoadout(SaveProfile profile, string blueprintId)
    {
        var record = profile.PermanentAugmentLoadouts.FirstOrDefault(existing => existing.BlueprintId == blueprintId)
            ?? new PermanentAugmentLoadoutRecord { BlueprintId = blueprintId };
        return new PermanentAugmentLoadoutState(record.BlueprintId, record.EquippedAugmentIds);
    }

    private void ApplyLedgerBackedReward(RewardOption option, SessionTextToken summaryToken)
    {
        if (ActiveRun == null)
        {
            ActiveRun = RunStateService.StartRun(IsQuickBattleSmokeActive ? "quick-battle" : GetExpeditionRunId(), CaptureBlueprintState(), IsQuickBattleSmokeActive);
        }

        var currencyState = new CurrencyState(
            Profile.Currencies.Gold,
            Profile.Currencies.Echo,
            Profile.Currencies.TraitRerollCurrency,
            Profile.Currencies.TraitLockToken,
            Profile.Currencies.TraitPurgeToken,
            Profile.Currencies.EmberDust,
            Profile.Currencies.EchoCrystal,
            Profile.Currencies.BossSigil);
        var result = RewardLedgerService.ApplyReward(currencyState, ActiveRun!, option);
        Profile.Currencies.Gold = currencyState.Gold;
        Profile.Currencies.Echo = currencyState.Echo;
        Profile.Currencies.TraitRerollCurrency = currencyState.TraitRerollCurrency;
        Profile.Currencies.TraitLockToken = currencyState.TraitLockToken;
        Profile.Currencies.TraitPurgeToken = currencyState.TraitPurgeToken;
        Profile.Currencies.EmberDust = currencyState.EmberDust;
        Profile.Currencies.EchoCrystal = currencyState.EchoCrystal;
        Profile.Currencies.BossSigil = currencyState.BossSigil;
        Profile.RewardLedger.Add(new RewardLedgerEntryRecord
        {
            EntryId = result.RewardEntry.EntryId,
            RunId = result.RewardEntry.RunId,
            RewardId = result.RewardEntry.RewardId,
            RewardType = result.RewardEntry.RewardType,
            Amount = result.RewardEntry.Amount,
            CreatedAtUtc = result.RewardEntry.CreatedAtUtc,
            Summary = result.RewardEntry.Summary,
            SourceId = ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
            SourceKind = ResolveRewardSourceKind(ActiveRun?.Overlay.RewardSourceId, isSettlementChoice: true),
        });
        if (result.InventoryEntry != null)
        {
            Profile.InventoryLedger.Add(new InventoryLedgerEntryRecord
            {
                EntryId = result.InventoryEntry.EntryId,
                RunId = result.InventoryEntry.RunId,
                ItemInstanceId = result.InventoryEntry.ItemInstanceId,
                ItemBaseId = result.InventoryEntry.ItemBaseId,
                ChangeKind = result.InventoryEntry.ChangeKind,
                Amount = result.InventoryEntry.Amount,
                CreatedAtUtc = result.InventoryEntry.CreatedAtUtc,
                Summary = result.InventoryEntry.Summary,
                SourceId = ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
                SourceKind = ResolveRewardSourceKind(ActiveRun?.Overlay.RewardSourceId, isSettlementChoice: true),
            });
        }

        ActiveRun = result.UpdatedRun;
        if (option.Type == SM.Content.Definitions.RewardType.TemporaryAugment)
        {
            TrackPermanentAugmentProgression(option.Id);
        }

        LastRewardApplicationSummary = summaryToken;
        SyncActiveRunRecord();
        SyncExpeditionState();
    }

    private static string ResolveRoleTag(string classId, DeploymentAnchorId anchor)
    {
        return classId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => anchor.IsFrontRow() ? "frontline" : "backline",
        };
    }

    private string NormalizeCharacterId(string characterId, string archetypeId)
    {
        if (!string.IsNullOrWhiteSpace(characterId))
        {
            return characterId;
        }

        return string.IsNullOrWhiteSpace(archetypeId) ? string.Empty : archetypeId;
    }

    private string ResolveBlueprintRoleInstructionId(string heroId, string classId, DeploymentAnchorId anchor)
    {
        if (IsQuickBattleSmokeActive && QuickBattleConfig != null && !QuickBattleConfig.UseScenarioAuthoring)
        {
            var overrideId = QuickBattleConfig.AllySlots
                .Where(slot => slot != null && string.Equals(slot.HeroId, heroId, StringComparison.Ordinal))
                .Select(slot => slot.RoleInstructionIdOverride)
                .FirstOrDefault(roleId => !string.IsNullOrWhiteSpace(roleId));
            if (!string.IsNullOrWhiteSpace(overrideId))
            {
                return overrideId;
            }
        }

        return ResolveDefaultRoleInstructionId(classId, anchor);
    }

    private string ResolveSandboxCharacterId(CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.CharacterId))
        {
            return slot.CharacterId;
        }

        if (!string.IsNullOrWhiteSpace(slot.ArchetypeIdOverride))
        {
            return slot.ArchetypeIdOverride;
        }

        return string.Empty;
    }

    private string ResolveSandboxArchetypeId(CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.ArchetypeIdOverride))
        {
            return slot.ArchetypeIdOverride;
        }

        var characterId = ResolveSandboxCharacterId(slot);
        if (_combatContentLookup.TryGetCharacterDefinition(characterId, out var character)
            && character.DefaultArchetype != null)
        {
            return character.DefaultArchetype.Id;
        }

        return characterId;
    }

    private string ResolveSandboxRoleInstructionId(CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.RoleInstructionId))
        {
            return slot.RoleInstructionId;
        }

        var characterId = ResolveSandboxCharacterId(slot);
        if (_combatContentLookup.TryGetCharacterDefinition(characterId, out var character)
            && character.DefaultRoleInstruction != null)
        {
            return character.DefaultRoleInstruction.Id;
        }

        var resolvedArchetypeId = ResolveSandboxArchetypeId(slot);
        if (_combatContentLookup.TryGetArchetype(resolvedArchetypeId, out var archetype))
        {
            return ResolveDefaultRoleInstructionId(archetype.Class.Id, slot.Anchor);
        }

        return string.Empty;
    }

    private string ResolveSandboxRoleTag(CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.RoleTag) && !string.Equals(slot.RoleTag, "auto", StringComparison.Ordinal))
        {
            return slot.RoleTag;
        }

        var roleInstructionId = ResolveSandboxRoleInstructionId(slot);
        if (_combatContentLookup.TryGetRoleInstructionDefinition(roleInstructionId, out var roleInstruction))
        {
            return roleInstruction.RoleTag;
        }

        var resolvedArchetypeId = ResolveSandboxArchetypeId(slot);
        if (_combatContentLookup.TryGetArchetype(resolvedArchetypeId, out var archetype))
        {
            return ResolveRoleTag(archetype.Class.Id, slot.Anchor);
        }

        return slot.Anchor.IsFrontRow() ? "frontline" : "backline";
    }

    private static string ResolveDefaultRoleInstructionId(string classId, DeploymentAnchorId anchor)
    {
        return classId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => anchor.IsFrontRow() ? "frontline" : "backline",
        };
    }

    private static SessionTextToken BuildBattleSummaryToken(bool victory, int stepCount, int eventCount)
    {
        return new SessionTextToken(
            GameLocalizationTables.UIReward,
            "ui.reward.battle_summary.base",
            "{0} / {1} steps / {2} events",
            SessionTextArg.Localized(
                GameLocalizationTables.UIReward,
                victory ? "ui.reward.result.victory" : "ui.reward.result.defeat",
                victory ? "Victory" : "Defeat"),
            SessionTextArg.Number(stepCount),
            SessionTextArg.Number(eventCount));
    }

    private static string BuildRewardChoiceSummaryKey(RewardChoiceViewModel choice)
    {
        return choice.Kind switch
        {
            RewardChoiceKind.Gold => "ui.reward.kind.gold",
            RewardChoiceKind.Item => "ui.reward.kind.item",
            RewardChoiceKind.TemporaryAugment => "ui.reward.kind.temp_augment",
            RewardChoiceKind.Echo => "ui.reward.kind.echo",
            RewardChoiceKind.PermanentAugmentSlot => "ui.reward.kind.permanent_slot",
            _ => "ui.common.none"
        };
    }

    private static SessionTextToken BuildRewardChoiceSummaryToken(RewardChoiceViewModel choice)
    {
        return choice.Kind switch
        {
            RewardChoiceKind.Gold => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.gold",
                "Gold +{0}",
                SessionTextArg.Number(choice.GoldAmount)),
            RewardChoiceKind.Item => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.item",
                "Item / {0}",
                SessionTextArg.ItemName(choice.PayloadId)),
            RewardChoiceKind.TemporaryAugment => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.temp_augment",
                "Temp / {0}",
                SessionTextArg.AugmentName(choice.PayloadId)),
            RewardChoiceKind.Echo => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.echo",
                "Echo +{0}",
                SessionTextArg.Number(choice.EchoAmount)),
            RewardChoiceKind.PermanentAugmentSlot => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.permanent_slot",
                "Permanent Slot +{0}",
                SessionTextArg.Number(choice.PermanentSlotAmount)),
            _ => SessionTextToken.Plain(choice.PayloadId)
        };
    }
}
