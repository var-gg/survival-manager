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
    private static readonly ProfilerMarker BindProfileMarker = new("SM.GameSessionState.BindProfile");
#if UNITY_EDITOR
    private const string CombatSandboxEditorAssetPath = "Assets/_Game/Authoring/CombatSandbox/combat_sandbox_active.asset";
#endif

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
    private readonly NarrativeRuntimeBootstrap _narrativeRuntimeBootstrap;
    private readonly SessionProfileSync _profileSync;
    private readonly SessionDeploymentFlow _deploymentFlow;
    private readonly SessionRecruitmentFlow _recruitmentFlow;
    private readonly SessionExpeditionFlow _expeditionFlow;
    private readonly SessionRewardSettlementFlow _rewardSettlementFlow;
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
    public StoryDirectorService StoryDirector { get; private set; }
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
    public NarrativeProgressRecord NarrativeProgress => StoryDirector.Progress;

    public GameSessionState(ICombatContentLookup combatContentLookup)
    {
        _combatContentLookup = combatContentLookup;
        _narrativeRuntimeBootstrap = NarrativeRuntimeBootstrap.LoadFromResources();
        _profileSync = new SessionProfileSync(this);
        _deploymentFlow = new SessionDeploymentFlow(this);
        _recruitmentFlow = new SessionRecruitmentFlow(this);
        _expeditionFlow = new SessionExpeditionFlow(this);
        _rewardSettlementFlow = new SessionRewardSettlementFlow(this);
        StoryDirector = _narrativeRuntimeBootstrap.CreateStoryDirector(NarrativeProgressRecord.Empty);
    }

    public void BindProfile(SaveProfile profile) => _profileSync.BindProfile(profile);

    private void BindProfileCore(SaveProfile profile)
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
            Profile.Narrative = NarrativeProgressRecord.Normalize(Profile.Narrative);
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
            RebindNarrativeServices();
        }

        stopwatch.Stop();
        RuntimeInstrumentation.LogDuration(
            nameof(GameSessionState) + ".BindProfile",
            stopwatch.Elapsed,
            $"heroes={Profile.Heroes.Count}; inventory={Profile.Inventory.Count}");
    }

    public void AdvanceNarrative(NarrativeMoment moment, StoryMomentContext? context = null) =>
        _profileSync.AdvanceNarrative(moment, context);

    private void AdvanceNarrativeCore(NarrativeMoment moment, StoryMomentContext? context)
    {
        StoryDirector.Advance(moment, context ?? StoryMomentContext.Empty);
        SyncNarrativeProgress();
    }

    public bool TryDequeueNarrativePresentation(out StoryPresentationRequest? request) =>
        _profileSync.TryDequeueNarrativePresentation(out request);

    private bool TryDequeueNarrativePresentationCore(out StoryPresentationRequest? request)
    {
        var dequeued = StoryDirector.TryDequeuePendingPresentation(out request);
        SyncNarrativeProgress();
        return dequeued;
    }

    public void ResetNarrativeRunScopedProgress() => _profileSync.ResetNarrativeRunScopedProgress();

    private void ResetNarrativeRunScopedProgressCore()
    {
        StoryDirector.ResetRunScopedProgress();
        SyncNarrativeProgress();
    }

    public void BeginNewExpedition() => _expeditionFlow.BeginNewExpedition();

    private void BeginNewExpeditionCore()
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

    public void PrepareQuickBattleSmoke() => _expeditionFlow.PrepareQuickBattleSmoke();

    private void PrepareQuickBattleSmokeCore()
    {
        var config = LoadCombatSandboxConfig();
        PrepareQuickBattleSmoke(config, CombatSandboxLaneKind.TownIntegrationSmoke);
    }

    public void PrepareCombatSandboxDirect() => _expeditionFlow.PrepareCombatSandboxDirect();

    private void PrepareCombatSandboxDirectCore()
    {
        var config = LoadCombatSandboxConfig();
        PrepareQuickBattleSmoke(config, CombatSandboxLaneKind.DirectCombatSandbox);
    }

    public void PrepareTownQuickBattleSmoke() => _expeditionFlow.PrepareTownQuickBattleSmoke();

    private void PrepareTownQuickBattleSmokeCore()
    {
        var config = LoadCombatSandboxConfig();
        PrepareQuickBattleSmoke(config, CombatSandboxLaneKind.TownIntegrationSmoke);
    }

    internal void PrepareQuickBattleSmoke(CombatSandboxConfig? quickBattleConfig) =>
        _expeditionFlow.PrepareQuickBattleSmoke(quickBattleConfig);

    private void PrepareQuickBattleSmokeCore(CombatSandboxConfig? quickBattleConfig)
    {
        PrepareQuickBattleSmoke(quickBattleConfig, CombatSandboxLaneKind.TownIntegrationSmoke);
    }

    public void RestartQuickBattle(bool advanceSeed) => _expeditionFlow.RestartQuickBattle(advanceSeed);

    private void RestartQuickBattleCore(bool advanceSeed)
    {
        ReloadCombatSandboxConfig();
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

    public void ExitCombatSandbox() => _expeditionFlow.ExitCombatSandbox();

    private void ExitCombatSandboxCore()
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
        bool resetSeedOverride = true) =>
        _expeditionFlow.PrepareQuickBattleSmoke(quickBattleConfig, laneKind, resetSeedOverride);

    private void PrepareQuickBattleSmokeCore(
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

    public bool TryCycleCampaignChapter(int direction) => _expeditionFlow.TryCycleCampaignChapter(direction);

    private bool TryCycleCampaignChapterCore(int direction)
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

    public bool TryCycleCampaignSite(int direction) => _expeditionFlow.TryCycleCampaignSite(direction);

    private bool TryCycleCampaignSiteCore(int direction)
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

    public bool PrepareSelectedBattleNodeHandoff() => _expeditionFlow.PrepareSelectedBattleNodeHandoff();

    private bool PrepareSelectedBattleNodeHandoffCore()
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

    public bool ResolveSelectedNodeToRewardSettlement() => _expeditionFlow.ResolveSelectedNodeToRewardSettlement();

    private bool ResolveSelectedNodeToRewardSettlementCore()
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

    public void ReloadCombatSandboxConfig() => _expeditionFlow.ReloadCombatSandboxConfig();

    private void ReloadCombatSandboxConfigCore()
    {
        QuickBattleConfig = LoadCombatSandboxConfig();
    }

    public void ReloadQuickBattleConfig() => _expeditionFlow.ReloadQuickBattleConfig();

    private void ReloadQuickBattleConfigCore()
    {
        ReloadCombatSandboxConfig();
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
        var context = CombatSandboxCompilationContextFactory.Create(
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

    private static CombatSandboxConfig? LoadCombatSandboxConfig()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<CombatSandboxConfig>(CombatSandboxEditorAssetPath);
#else
        return null;
#endif
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

    public void AdvanceExpeditionNode() => _expeditionFlow.AdvanceExpeditionNode();

    private void AdvanceExpeditionNodeCore()
    {
        ResolveSelectedExpeditionNode();
    }

    public bool SelectNextExpeditionNode(int nodeIndex) => _expeditionFlow.SelectNextExpeditionNode(nodeIndex);

    private bool SelectNextExpeditionNodeCore(int nodeIndex)
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

    public ExpeditionNodeViewModel? GetCurrentExpeditionNode() => _expeditionFlow.GetCurrentExpeditionNode();

    private ExpeditionNodeViewModel? GetCurrentExpeditionNodeCore()
    {
        EnsureExpeditionNodes();
        return CurrentExpeditionNodeIndex >= 0 && CurrentExpeditionNodeIndex < _expeditionNodes.Count
            ? _expeditionNodes[CurrentExpeditionNodeIndex]
            : null;
    }

    public ExpeditionNodeViewModel? GetSelectedExpeditionNode() => _expeditionFlow.GetSelectedExpeditionNode();

    private ExpeditionNodeViewModel? GetSelectedExpeditionNodeCore()
    {
        EnsureExpeditionNodes();
        return SelectedExpeditionNodeIndex is int index && index >= 0 && index < _expeditionNodes.Count
            ? _expeditionNodes[index]
            : null;
    }

    public IReadOnlyList<int> GetSelectableNextNodeIndices() => _expeditionFlow.GetSelectableNextNodeIndices();

    private IReadOnlyList<int> GetSelectableNextNodeIndicesCore()
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

    public bool ResolveSelectedExpeditionNode() => _expeditionFlow.ResolveSelectedExpeditionNode();

    private bool ResolveSelectedExpeditionNodeCore()
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

    public void AbandonExpeditionRun() => _expeditionFlow.AbandonExpeditionRun();

    private void AbandonExpeditionRunCore()
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

    public void ReturnToTownAfterReward() => _expeditionFlow.ReturnToTownAfterReward();

    private void ReturnToTownAfterRewardCore()
    {
        ConsumePendingPermanentUnlock();
        FinalizeRewardSettlement();
        IsQuickBattleSmokeActive = false;
        QuickBattleLaneKind = CombatSandboxLaneKind.None;
        _quickBattleSeedOverride = null;
        _compiledQuickBattleScenario = null;
    }

    public void SetCurrentScene(string sceneName) => _profileSync.SetCurrentScene(sceneName);

    private void SetCurrentSceneCore(string sceneName)
    {
        CurrentSceneName = sceneName;
        if (string.Equals(sceneName, SceneNames.Town, StringComparison.Ordinal))
        {
            ResetRecruitPhaseForTownEntry();
            AppendRuntimeTelemetry(BuildEconomySnapshot("town_entry"));
        }
    }

    public bool CanManualProfileReload(out string reason) => _profileSync.CanManualProfileReload(out reason);

    private bool CanManualProfileReloadCore(out string reason)
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

    public Result RerollRecruitOffers() => _recruitmentFlow.RerollRecruitOffers();

    private Result RerollRecruitOffersCore()
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

    public Result Recruit(int offerIndex) => _recruitmentFlow.Recruit(offerIndex);

    private Result RecruitCore(int offerIndex)
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

    public Result UseScout(ScoutDirective directive) => _recruitmentFlow.UseScout(directive);

    private Result UseScoutCore(ScoutDirective directive)
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

    public Result RetrainHero(string heroId, RetrainOperationKind operation) =>
        _recruitmentFlow.RetrainHero(heroId, operation);

    private Result RetrainHeroCore(string heroId, RetrainOperationKind operation)
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

    public Result DismissHero(string heroId) => _recruitmentFlow.DismissHero(heroId);

    private Result DismissHeroCore(string heroId)
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

    public Result GrantHeroDirect(string archetypeId, RecruitOfferSource source = RecruitOfferSource.DirectGrant) =>
        _recruitmentFlow.GrantHeroDirect(archetypeId, source);

    private Result GrantHeroDirectCore(string archetypeId, RecruitOfferSource source = RecruitOfferSource.DirectGrant)
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

    public Result EquipItem(string heroId, string itemInstanceId) => _deploymentFlow.EquipItem(heroId, itemInstanceId);

    private Result EquipItemCore(string heroId, string itemInstanceId)
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

    public Result UnequipItem(string heroId, string itemInstanceId) =>
        _deploymentFlow.UnequipItem(heroId, itemInstanceId);

    private Result UnequipItemCore(string heroId, string itemInstanceId)
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

    public Result RefitItem(string itemInstanceId, int affixSlotIndex) =>
        _deploymentFlow.RefitItem(itemInstanceId, affixSlotIndex);

    private Result RefitItemCore(string itemInstanceId, int affixSlotIndex)
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

    public Result UnlockPermanentAugmentCandidate(string augmentId) =>
        _deploymentFlow.UnlockPermanentAugmentCandidate(augmentId);

    private Result UnlockPermanentAugmentCandidateCore(string augmentId)
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

        PermanentAugmentSlotCount = GameSessionProfileNormalizer.NormalizePermanentAugments(Profile, _combatContentLookup);
        return Result.Success();
    }

    public Result EquipPermanentAugment(string augmentId) => _deploymentFlow.EquipPermanentAugment(augmentId);

    private Result EquipPermanentAugmentCore(string augmentId)
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

    public Result UnequipPermanentAugment(string augmentId) => _deploymentFlow.UnequipPermanentAugment(augmentId);

    private Result UnequipPermanentAugmentCore(string augmentId)
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

    public Result SelectPassiveBoard(string heroId, string boardId) =>
        _deploymentFlow.SelectPassiveBoard(heroId, boardId);

    private Result SelectPassiveBoardCore(string heroId, string boardId)
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

    public Result TogglePassiveNode(string heroId, string nodeId) => _deploymentFlow.TogglePassiveNode(heroId, nodeId);

    private Result TogglePassiveNodeCore(string heroId, string nodeId)
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

    public bool ToggleExpeditionHero(string heroId) => _deploymentFlow.ToggleExpeditionHero(heroId);

    private bool ToggleExpeditionHeroCore(string heroId)
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

    public void EnsureBattleDeployReady() => _deploymentFlow.EnsureBattleDeployReady();

    private void EnsureBattleDeployReadyCore()
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

    internal void ApplyQuickBattleAllySlotOverrides(CombatSandboxConfig config) =>
        _deploymentFlow.ApplyQuickBattleAllySlotOverrides(config);

    private void ApplyQuickBattleAllySlotOverridesCore(CombatSandboxConfig config)
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

    public void PromoteToBattleDeploy(string heroId) => _deploymentFlow.PromoteToBattleDeploy(heroId);

    private void PromoteToBattleDeployCore(string heroId)
    {
        if (!_expeditionSquadHeroIds.Contains(heroId))
        {
            return;
        }

        var preferredAnchor = ResolvePreferredAnchor(heroId);
        AssignHeroToAnchor(preferredAnchor, heroId);
    }

    public string? GetAssignedHeroId(DeploymentAnchorId anchor) => _deploymentFlow.GetAssignedHeroId(anchor);

    private string? GetAssignedHeroIdCore(DeploymentAnchorId anchor)
    {
        return _deploymentAssignments.TryGetValue(anchor, out var heroId) ? heroId : null;
    }

    public bool AssignHeroToAnchor(DeploymentAnchorId anchor, string? heroId) =>
        _deploymentFlow.AssignHeroToAnchor(anchor, heroId);

    private bool AssignHeroToAnchorCore(DeploymentAnchorId anchor, string? heroId)
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

    public bool CycleDeploymentAssignment(DeploymentAnchorId anchor) => _deploymentFlow.CycleDeploymentAssignment(anchor);

    private bool CycleDeploymentAssignmentCore(DeploymentAnchorId anchor)
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

    public void CycleTeamPosture() => _deploymentFlow.CycleTeamPosture();

    private void CycleTeamPostureCore()
    {
        var values = (TeamPostureType[])Enum.GetValues(typeof(TeamPostureType));
        var currentIndex = Array.IndexOf(values, SelectedTeamPosture);
        SelectedTeamPosture = values[(currentIndex + 1) % values.Length];
        CaptureBlueprintState();
        SyncActiveRunIfPresent();
    }

    public void SetTeamPosture(TeamPostureType posture) => _deploymentFlow.SetTeamPosture(posture);

    private void SetTeamPostureCore(TeamPostureType posture)
    {
        SelectedTeamPosture = posture;
        CaptureBlueprintState();
        SyncActiveRunIfPresent();
    }

    public void SetTeamTactic(string teamTacticId) => _deploymentFlow.SetTeamTactic(teamTacticId);

    private void SetTeamTacticCore(string teamTacticId)
    {
        SelectedTeamTacticId = teamTacticId ?? string.Empty;
        CaptureBlueprintState();
        SyncActiveRunIfPresent();
    }

    public IEnumerable<(DeploymentAnchorId Anchor, string? HeroId)> EnumerateDeploymentAssignments() =>
        _deploymentFlow.EnumerateDeploymentAssignments();

    private IEnumerable<(DeploymentAnchorId Anchor, string? HeroId)> EnumerateDeploymentAssignmentsCore()
    {
        EnsureBattleDeployReady();
        foreach (var anchor in DeploymentAnchorOrder)
        {
            yield return (anchor, GetAssignedHeroId(anchor));
        }
    }

    public IReadOnlyList<BattleParticipantSpec> BuildBattleParticipants() => _deploymentFlow.BuildBattleParticipants();

    private IReadOnlyList<BattleParticipantSpec> BuildBattleParticipantsCore()
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

    public BattleLoadoutSnapshot BuildBattleLoadoutSnapshot() => _deploymentFlow.BuildBattleLoadoutSnapshot();

    private BattleLoadoutSnapshot BuildBattleLoadoutSnapshotCore()
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

    public bool TryResolveCurrentEncounter(out ResolvedEncounterContext context, out string error) =>
        _expeditionFlow.TryResolveCurrentEncounter(out context, out error);

    private bool TryResolveCurrentEncounterCore(out ResolvedEncounterContext context, out string error)
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

    public void RecordBattleAudit(BattleReplayBundle replay) => _rewardSettlementFlow.RecordBattleAudit(replay);

    private void RecordBattleAuditCore(BattleReplayBundle replay)
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

    public void SaveDebugSnapshot(string note = "manual-debug-save") => _profileSync.SaveDebugSnapshot(note);

    private void SaveDebugSnapshotCore(string note = "manual-debug-save")
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

    public void SetLastBattleResult(bool victory, string summary) =>
        _rewardSettlementFlow.SetLastBattleResult(victory, summary);

    private void SetLastBattleResultCore(bool victory, string summary)
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

    public void MarkBattleResolved(bool victory, int stepCount, int eventCount) =>
        _rewardSettlementFlow.MarkBattleResolved(victory, stepCount, eventCount);

    private void MarkBattleResolvedCore(bool victory, int stepCount, int eventCount)
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

    public bool ApplyRewardChoice(int index) => _rewardSettlementFlow.ApplyRewardChoice(index);

    private bool ApplyRewardChoiceCore(int index)
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
                ApplyLedgerBackedReward(new RewardOption(choice.PayloadId, SM.Core.Content.RewardType.Gold, choice.GoldAmount, BuildRewardChoiceSummaryKey(choice)), BuildRewardChoiceSummaryToken(choice));
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
                    RewardType = SM.Core.Content.RewardType.Item.ToString(),
                    Amount = 1,
                    CreatedAtUtc = timestamp,
                    Summary = BuildRewardChoiceSummaryKey(choice),
                    SourceId = ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
                    SourceKind = ResolveRewardSourceKind(ActiveRun?.Overlay.RewardSourceId, isSettlementChoice: true),
                });
                LastRewardApplicationSummary = BuildRewardChoiceSummaryToken(choice);
                break;
            case RewardChoiceKind.TemporaryAugment:
                ApplyLedgerBackedReward(new RewardOption(choice.PayloadId, SM.Core.Content.RewardType.TemporaryAugment, 1, BuildRewardChoiceSummaryKey(choice)), BuildRewardChoiceSummaryToken(choice));
                break;
            case RewardChoiceKind.Echo:
                ApplyLedgerBackedReward(new RewardOption(choice.PayloadId, SM.Core.Content.RewardType.Echo, choice.EchoAmount, BuildRewardChoiceSummaryKey(choice)), BuildRewardChoiceSummaryToken(choice));
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

    public string PreviewPermanentUnlockFromTemporaryAugment(string augmentId) =>
        _rewardSettlementFlow.PreviewPermanentUnlockFromTemporaryAugment(augmentId);

    private string PreviewPermanentUnlockFromTemporaryAugmentCore(string augmentId)
    {
        if (string.IsNullOrWhiteSpace(augmentId)
            || ActiveRun == null
            || !string.IsNullOrWhiteSpace(ActiveRun.Overlay.FirstSelectedTemporaryAugmentId))
        {
            return string.Empty;
        }

        return ResolvePendingPermanentUnlockId(augmentId);
    }


}
