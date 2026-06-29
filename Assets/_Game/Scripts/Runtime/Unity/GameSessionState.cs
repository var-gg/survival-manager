using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;
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
    private readonly SessionAtlasFlow _atlasFlow;
    private readonly SessionRewardSettlementFlow _rewardSettlementFlow;
    private readonly LoadoutCompiler _loadoutCompiler = new();
    private readonly List<string> _expeditionSquadHeroIds = new();
    private readonly Dictionary<DeploymentAnchorId, string?> _deploymentAssignments = new();
    // 유저가 출전 anchor를 명시 편성했는지. 자동배치는 이 플래그를 세우지 않으며, true면 자동배치가 배치를 덮지 않는다.
    private bool _deploymentUserAuthored;
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
    private AtlasSessionState? _atlasSession;
    private AtlasExpeditionModifierPayload? _atlasExpeditionModifierPayload;
    private RunBattlePayload? _runBattlePayload;

    public SaveProfile Profile { get; private set; } = new();
    public ActiveRunState? ActiveRun { get; private set; }
    public BattleLoadoutSnapshot? LastCompiledBattleSnapshot { get; private set; }
    public StoryDirectorService StoryDirector { get; private set; }
    public RosterState Roster { get; private set; } = new();
    public ExpeditionState Expedition { get; private set; } = new();
    public AtlasSessionState? AtlasSession => _atlasSession;
    public AtlasExpeditionModifierPayload? AtlasExpeditionModifierPayload => _atlasExpeditionModifierPayload;
    public RunBattlePayload? RunBattlePayload => _runBattlePayload;
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
    public RewardSummaryRecord? LastCommittedRewardSummary { get; private set; }
    // ADR-0028 #1 가독성: 직전 출격 정치 정산(이행/거스름/거절 사유 + 신뢰 delta). reward 화면이 player-readable로 노출.
    public PoliticalSettlementReport LastPoliticalSettlement { get; private set; } = PoliticalSettlementReport.Empty;
    // 게임의 중심 카타르시스 — 직전 전투의 진형 페이오프(MVP·진형 전과·발현). 전투 피드의 transient 요약과
    // 같은 데이터를 빌드를 고르는 보상 화면으로 운반한다(런타임 전용, save truth 비오염).
    public BattleFormationPayoffSummary LastBattleFormationPayoff { get; private set; } = BattleFormationPayoffSummary.Empty;
    // ADR-0028 #provenance: 이번 전투에 적용된 정치 조건(후원/경계 + 출처 세력·채널·사유). 전투 HUD가 관전 중 노출 —
    // compile/snapshot hash가 *효과*는 포착하나 "어느 세력 mandate에서 왔나"는 숨던 것을 player-visible로(GPT Pro 잔여 20%).
    public IReadOnlyList<PoliticalCombatCondition> ActiveBattlePoliticalConditions { get; private set; } = Array.Empty<PoliticalCombatCondition>();
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
    public bool IsDeploymentUserAuthored => _deploymentUserAuthored;
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
        : this(combatContentLookup, GameSessionRuntimeBootstrapProvider.Resources)
    {
    }

    internal GameSessionState(
        ICombatContentLookup combatContentLookup,
        GameSessionRuntimeBootstrapProvider runtimeBootstrapProvider)
        : this(
            combatContentLookup,
            (runtimeBootstrapProvider ?? throw new ArgumentNullException(nameof(runtimeBootstrapProvider)))
                .CreateNarrativeBootstrap())
    {
    }

    internal GameSessionState(ICombatContentLookup combatContentLookup, NarrativeRuntimeBootstrap narrativeRuntimeBootstrap)
    {
        _combatContentLookup = combatContentLookup ?? throw new ArgumentNullException(nameof(combatContentLookup));
        _narrativeRuntimeBootstrap = narrativeRuntimeBootstrap ?? throw new ArgumentNullException(nameof(narrativeRuntimeBootstrap));
        _profileSync = new SessionProfileSync(this);
        _deploymentFlow = new SessionDeploymentFlow(this);
        _recruitmentFlow = new SessionRecruitmentFlow(this);
        _expeditionFlow = new SessionExpeditionFlow(this);
        _atlasFlow = new SessionAtlasFlow(this);
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
            LastCommittedRewardSummary = null;
            LastPoliticalSettlement = PoliticalSettlementReport.Empty;
            LastBattleFormationPayoff = BattleFormationPayoffSummary.Empty;
            ActiveBattlePoliticalConditions = Array.Empty<PoliticalCombatCondition>();
            _lastAutomaticLootBundle = null;
            _hasPendingRewardSettlement = false;
            _lastDuplicateConversion = null;
            _quickBattleSeedOverride = null;
            _compiledQuickBattleScenario = null;
            _atlasSession = null;
            _atlasExpeditionModifierPayload = null;
            _runBattlePayload = null;
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
            RestoreDeploymentFromActiveBlueprint();
            RestoreHeroTargetDirectivesFromActiveBlueprint();
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

    public bool TryDequeueNarrativePresentation(out StoryPresentationRequest? request) =>
        _profileSync.TryDequeueNarrativePresentation(out request);

    /// <summary>
    /// headless/테스트 드라이버가 seed된 narrative director를 주입한다 — BindProfile 이후 호출해야
    /// RebindNarrativeServices(BindProfile 끝)의 클로버를 피한다. 프로덕션 흐름은 ctor/BindProfile에서
    /// bootstrap director를 쓰므로 미호출 시 영향 0(StorySceneFlowBridge.OverridePresentationSink와 동형 seam).
    /// </summary>
    internal void OverrideStoryDirector(StoryDirectorService director)
        => StoryDirector = director ?? throw new ArgumentNullException(nameof(director));

    public void ResetNarrativeRunScopedProgress() => _profileSync.ResetNarrativeRunScopedProgress();

    public void BeginNewExpedition() => _expeditionFlow.BeginNewExpedition();

    public void PrepareQuickBattleSmoke() => _expeditionFlow.PrepareQuickBattleSmoke();

    public void PrepareCombatSandboxDirect() => _expeditionFlow.PrepareCombatSandboxDirect();

    public void PrepareTownQuickBattleSmoke() => _expeditionFlow.PrepareTownQuickBattleSmoke();

    internal void PrepareQuickBattleSmoke(CombatSandboxConfig? quickBattleConfig) =>
        _expeditionFlow.PrepareQuickBattleSmoke(quickBattleConfig);

    public void RestartQuickBattle(bool advanceSeed) => _expeditionFlow.RestartQuickBattle(advanceSeed);

    public void ExitCombatSandbox() => _expeditionFlow.ExitCombatSandbox();

    internal void PrepareQuickBattleSmoke(
        CombatSandboxConfig? quickBattleConfig,
        CombatSandboxLaneKind laneKind,
        bool resetSeedOverride = true) =>
        _expeditionFlow.PrepareQuickBattleSmoke(quickBattleConfig, laneKind, resetSeedOverride);

    public bool TryCycleCampaignChapter(int direction) => _expeditionFlow.TryCycleCampaignChapter(direction);

    public bool TryCycleCampaignSite(int direction) => _expeditionFlow.TryCycleCampaignSite(direction);

    public bool PrepareSelectedBattleNodeHandoff() => _expeditionFlow.PrepareSelectedBattleNodeHandoff();

    public bool ResolveSelectedNodeToRewardSettlement() => _expeditionFlow.ResolveSelectedNodeToRewardSettlement();

    public void ReloadCombatSandboxConfig() => _expeditionFlow.ReloadCombatSandboxConfig();

    public void ReloadQuickBattleConfig() => _expeditionFlow.ReloadQuickBattleConfig();

    public AtlasSessionState EnsureAtlasSession(AtlasRegionDefinition region, AtlasTraversalMode? traversalMode = null) =>
        _atlasFlow.EnsureAtlasSession(region, traversalMode);

    public AtlasSessionResolution ResolveAtlasSession(AtlasRegionDefinition region) =>
        _atlasFlow.ResolveAtlasSession(region);

    public void SelectAtlasSigil(AtlasRegionDefinition region, string sigilId) =>
        _atlasFlow.SelectSigil(region, sigilId);

    public void SelectAtlasNode(AtlasRegionDefinition region, string nodeId) =>
        _atlasFlow.SelectNode(region, nodeId);

    public void PlaceSelectedAtlasSigil(AtlasRegionDefinition region, string nodeId) =>
        _atlasFlow.PlaceSelectedSigil(region, nodeId);

    public bool TryApplyAtlasSelectionToExpedition(AtlasRegionDefinition region) =>
        _atlasFlow.TryApplySelectedNodeToExpedition(region);

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

    private static CombatSandboxConfig? LoadCombatSandboxConfig(bool required)
    {
#if UNITY_EDITOR
        var config = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatSandboxConfig>(CombatSandboxEditorAssetPath);
        if (config == null)
        {
            // 에디터 기동/도메인 리로드 직후의 transient 바인딩 실패 — 강제 재임포트 1회 후 재시도.
            // 여기서 null을 그대로 흘리면 전투테스트가 기본 캠페인 전투로 "조용히" 폴백해
            // 사용자가 다른 판을 보게 된다.
            UnityEditor.AssetDatabase.ImportAsset(
                CombatSandboxEditorAssetPath,
                UnityEditor.ImportAssetOptions.ForceSynchronousImport | UnityEditor.ImportAssetOptions.ForceUpdate);
            config = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatSandboxConfig>(CombatSandboxEditorAssetPath);
        }

        if (config == null)
        {
            // 레인 분리: config가 필수인 전투테스트(DirectCombatSandbox)만 에러 — GameSessionRoot의
            // 차단 에러와 짝. config-옵셔널인 smoke 레인(Town/테스트 통합 smoke)은 기본 전투 폴백이
            // 설계상 허용이므로 워닝으로 남긴다(테스트가 unhandled error log로 죽지 않게).
            var message =
                "[CombatSandbox] active config 로드 실패 — 전투테스트 시나리오를 컴파일할 수 없습니다: " +
                CombatSandboxEditorAssetPath;
            if (required)
            {
                UnityEngine.Debug.LogError(message);
            }
            else
            {
                UnityEngine.Debug.LogWarning(message + " (smoke 레인 — 기본 전투로 진행)");
            }
        }

        return config;
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

    public bool SelectNextExpeditionNode(int nodeIndex) => _expeditionFlow.SelectNextExpeditionNode(nodeIndex);

    public ExpeditionNodeViewModel? GetCurrentExpeditionNode() => _expeditionFlow.GetCurrentExpeditionNode();

    public ExpeditionNodeViewModel? GetSelectedExpeditionNode() => _expeditionFlow.GetSelectedExpeditionNode();

    public IReadOnlyList<int> GetSelectableNextNodeIndices() => _expeditionFlow.GetSelectableNextNodeIndices();

    public bool ResolveSelectedExpeditionNode() => _expeditionFlow.ResolveSelectedExpeditionNode();

    public void AbandonExpeditionRun() => _expeditionFlow.AbandonExpeditionRun();

    public void ReturnToTownAfterReward() => _expeditionFlow.ReturnToTownAfterReward();

    /// <summary>
    /// wave-55d: transient Town smoke → canonical lane 복원 후 SessionState 자체의 SmokeActive flag를
    /// 명시적으로 false reset. BindProfileCore가 일반 BindProfile path에서는 reset하지만 일부 lane 복원
    /// path에서는 reset되지 않는 corner case가 있어 GameSessionRoot.RestoreCanonicalProfileAfterTransientSmoke
    /// 에서 호출한다.
    /// </summary>
    public void ClearQuickBattleSmokeStatus()
    {
        IsQuickBattleSmokeActive = false;
        QuickBattleLaneKind = CombatSandboxLaneKind.None;
        _quickBattleSeedOverride = null;
        _compiledQuickBattleScenario = null;
    }

    public void SetCurrentScene(string sceneName) => _profileSync.SetCurrentScene(sceneName);

    public bool CanManualProfileReload(out string reason) => _profileSync.CanManualProfileReload(out reason);

    public Result RerollRecruitOffers() => _recruitmentFlow.RerollRecruitOffers();

    public Result Recruit(int offerIndex) => _recruitmentFlow.Recruit(offerIndex);

    public Result UseScout(ScoutDirective directive) => _recruitmentFlow.UseScout(directive);

    public Result RetrainHero(string heroId, RetrainOperationKind operation) =>
        _recruitmentFlow.RetrainHero(heroId, operation);

    public Result DismissHero(string heroId) => _recruitmentFlow.DismissHero(heroId);

    public Result GrantHeroDirect(string archetypeId, RecruitOfferSource source = RecruitOfferSource.DirectGrant) =>
        _recruitmentFlow.GrantHeroDirect(archetypeId, source);

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
            return Result.Fail($"잔향이 부족합니다. 재정비에는 {echoCost} 잔향이 필요합니다.");
        }

        var availableAffixes = BuildRefitCandidateAffixIds(item, affixSlotIndex);
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

        if (!HasPassiveBoardContent(boardId))
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

    private bool HasPassiveBoardContent(string boardId)
    {
        if (string.IsNullOrWhiteSpace(boardId))
        {
            return false;
        }

        if (_combatContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            if (snapshot.FirstPlayableSlice?.PassiveBoardIds.Contains(boardId, StringComparer.Ordinal) == true)
            {
                return true;
            }

            if (snapshot.PassiveNodes.Values.Any(node =>
                    node != null && string.Equals(node.BoardId, boardId, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        var slice = _combatContentLookup.GetFirstPlayableSlice();
        if (slice?.PassiveBoardIds.Contains(boardId, StringComparer.Ordinal) == true)
        {
            return true;
        }

        return _combatContentLookup.TryGetPassiveBoardDefinition(boardId, out _);
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

        // squad에서 빠진 hero의 유령 배치는 항상 정리하되(reconcile), 빈 보드 자동 채움(fill)은
        // 유저가 편성을 만진 적이 없고(_deploymentUserAuthored == false) 현재 배치가 완전히 빈 경우에만 한다.
        // 즉 출전 편성은 유저 1급 결정으로 존중되고, 자동배치는 신규/미편성 상태의 fallback으로만 동작한다.
        ReconcileDeploymentWithSquad();
        if (!_deploymentUserAuthored && BattleDeployHeroIds.Count == 0)
        {
            AutoFillDeploymentFromSquad();
        }
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
            // Quick Battle smoke는 유저 편성과 무관하게 항상 자동배치로 채운다.
            ReconcileDeploymentWithSquad();
            AutoFillDeploymentFromSquad();
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

    private bool AssignHeroToAnchorCore(DeploymentAnchorId anchor, string? heroId, bool markUserAuthored)
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
        // 유저 진입(승격/cycle/reset)은 편성 의도를 기록 — 이후 자동배치가 이 배치를 덮지 않는다.
        // 자동배치(AutoFillDeploymentFromSquad)는 markUserAuthored: false로 호출해 이 신호를 남기지 않는다.
        if (markUserAuthored)
        {
            _deploymentUserAuthored = true;
        }

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
        // ADR-0028 slice 2·3 — 정치 루프: 서약 발행/거스른 세력 standing이 다음 전투 조건을 건다.
        // standing 읽기(persistence)는 여기(SM.Unity)서, 임계/package 도출은 SM.Meta 순수 서비스가 소유.
        // 이 seam은 AllySupport 통로만 적용(적 EnemyAlertness는 encounter resolve seam에서).
        var politicalConditions = PoliticalCombatConditionService.Resolve(overlay.PledgedWarrantId, ResolveFactionStanding);
        // #provenance: 도출된 조건(양 채널)을 전투 HUD 노출용으로 보관 — 효과는 hash에 접히지만 출처/사유는 여기서만 산다.
        ActiveBattlePoliticalConditions = politicalConditions;
        var squadSupportPackages = PoliticalCombatConditionService.AllyPackages(politicalConditions);
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
            snapshot,
            squadSupportPackages);

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

    // 정치 standing 읽기(persistence). ADR-0028 — 두 정치 seam(ally compile / enemy resolve)이 공유.
    private int ResolveFactionStanding(string factionId) =>
        Profile.FactionStanding
            .FirstOrDefault(standing => string.Equals(standing.FactionId, factionId, StringComparison.Ordinal))?.Trust ?? 0;

    // EnemyAlertness 통로를 resolved encounter에 적용 — 적대 누적 세력이 적을 경계시킨다(ADR-0028 slice 3).
    private ResolvedEncounterContext ApplyEnemyPoliticalConditions(ResolvedEncounterContext context, ActiveRunState run)
    {
        var conditions = PoliticalCombatConditionService.Resolve(run.Overlay.PledgedWarrantId, ResolveFactionStanding);
        var enemyPackages = PoliticalCombatConditionService.EnemyPackages(conditions);
        return enemyPackages.Count == 0
            ? context
            : context with { Enemies = PoliticalCombatConditionService.ApplyEnemyPackages(context.Enemies, enemyPackages) };
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
        var encounterResolver = new EncounterResolutionService(snapshot);
        if (TryBuildBattleContext(snapshot, run, out var battleContext, out _))
        {
            if (encounterResolver.TryResolveEncounter(battleContext, out context, out error))
            {
                // ADR-0028 slice 3 — 거스른 세력 적대가 누적되면 적이 경계 상태로 출현(EnemyAlertness 통로).
                // 정치 충돌이 다음 전장에 닿는 지점. 적 package는 EnemySnapshotHash가 포착(live 결정적).
                context = ApplyEnemyPoliticalConditions(context, run);
                ActiveRun = RunStateService.SetBattleContext(run, battleContext);
                SyncActiveRunRecord();
                return true;
            }

            // 무음 강등 차단: authored 카탈로그가 있는데 인카운터 해석이 실패하면(오타·미시드 squad/encounter)
            // 디버그 4인 스모크로 조용히 바꿔치기하지 않고 실패를 표면화한다(fail-closed). 콘텐츠 validator가
            // 빌드타임에 이 ref를 막지만, validation이 스킵된 환경에서 깨진 authored 전투가 placeholder로
            // 둔갑하는 걸 차단한다. 디버그 스모크 fallback은 authored 카탈로그가 아예 없는 부트스트랩에만 허용.
            if (encounterResolver.HasAuthoredCatalog)
            {
                error = string.IsNullOrWhiteSpace(error) ? "Authored encounter resolution failed." : error;
                UnityEngine.Debug.LogError($"[Encounter] authored 인카운터 해석 실패 — 디버그 스모크 강등 거부: {error}");
                return false;
            }
        }

        var debugPlan = BuildQuickBattleEncounterPlan();
        var buildResult = BattleSetupBuilder.Build(Array.Empty<BattleParticipantSpec>(), debugPlan, snapshot);
        if (!buildResult.IsSuccess)
        {
            error = buildResult.Error ?? "Failed to build debug smoke encounter.";
            return false;
        }

        var debugContext = encounterResolver.BuildDebugSmokeContext(run, CurrentExpeditionNodeIndex);
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

    public void SaveDebugSnapshot(string note = "manual-debug-save") => _profileSync.SaveDebugSnapshot(note);

    public void SetLastBattleResult(bool victory, string summary) =>
        _rewardSettlementFlow.SetLastBattleResult(victory, summary);

    /// <summary>
    /// 출격 전 squad가 한 세력의 기준(Warrant)에 서약한다 — 서약 id를 run overlay에 stamp해
    /// settlement까지 운반한다(RewardSourceId와 동렬 rail). ludonarrative 루프 P2a의 write-end:
    /// settlement(WriteDossierEntry)이 이 값을 읽어 WarrantJudge로 이행/위반을 가린다.
    /// "어떤 세력 기준에 서약하나"의 선택 UI는 P2b(SM.Unity)가 이 메서드를 호출한다. ADR-0027.
    /// public facade가 아니라 internal flow API — P2b UI(같은 SM.Unity)와 test(InternalsVisibleTo)만 호출.
    /// </summary>
    internal void PledgeWarrant(string warrantId)
    {
        if (ActiveRun == null)
        {
            return;
        }

        ActiveRun = ActiveRun with
        {
            Overlay = ActiveRun.Overlay with { PledgedWarrantId = warrantId ?? string.Empty }
        };
        SyncActiveRunIfPresent();
    }

    public void MarkBattleResolved(
        bool victory,
        int stepCount,
        int eventCount,
        IReadOnlyList<BattleUnitReadModel>? finalUnits = null,
        BattleFormationPayoffSummary? formationPayoff = null)
    {
        LastBattleFormationPayoff = formationPayoff ?? BattleFormationPayoffSummary.Empty;
        _rewardSettlementFlow.MarkBattleResolved(victory, stepCount, eventCount, finalUnits);

        // 발화는 세션: 전투 해소 moment를 여기서 발화한다 — 씬 FinishBattle과 헤드리스 sim
        // (TryResolveSelectedBattleNodeViaSimulation)이 모두 이 1곳을 경유하므로 같은 발화를 공유한다
        // (표시+continuation은 씬이 bridge.PresentPending(GoToReward)으로). smoke 레인은 narrative 제외.
        if (!IsQuickBattleSmokeActive)
        {
            AdvanceNarrative(
                NarrativeMoment.BattleResolved,
                NarrativeMomentResolver.BuildNodeContext(this));
        }
    }

    public bool ApplyRewardChoice(int index)
    {
        var committed = _rewardSettlementFlow.ApplyRewardChoice(index);

        // 발화는 세션: 보상 확정(commit) moment를 여기서 발화한다 — 씬 controller가 아니라 세션이 단일 소스라
        // 헤드리스 드라이버와 실게임 RewardScreen이 같은 발화를 공유한다(표시는 씬이 bridge.PresentPending으로).
        // 빈/비해당 director는 no-op, smoke 레인은 narrative 제외(BattleStarted 가드와 동일).
        if (committed && !IsQuickBattleSmokeActive)
        {
            AdvanceNarrative(
                NarrativeMoment.RewardCommitted,
                NarrativeMomentResolver.BuildNodeContext(this, withRewardSummary: true));
        }

        return committed;
    }

    public string PreviewPermanentUnlockFromTemporaryAugment(string augmentId) =>
        _rewardSettlementFlow.PreviewPermanentUnlockFromTemporaryAugment(augmentId);

}
