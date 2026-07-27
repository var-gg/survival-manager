using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Combat.Model;
using SM.Core;
using SM.Core.Content;
using SM.Meta;
using SM.Unity.Narrative;
using SM.Unity.UI.Expedition;
using SM.Unity.UI.SiteEvents;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Atlas;

public sealed class AtlasScreenController : MonoBehaviour
{
    [SerializeField] private RuntimePanelHost panelHost = null!;
    [SerializeField] private StorySceneFlowBridge? _storyBridge;
    // ADR-0028 P2b: 출격 서약 선택 overlay. scene에서 WarrantSelection.uxml/uss 할당 시 활성, 미할당이면 직행(안전).
    [SerializeField] private VisualTreeAsset? warrantSelectionUxml;
    [SerializeField] private StyleSheet? warrantSelectionUss;

    private AtlasScreenPresenter _presenter = null!;
    private AtlasScreenView _view = null!;
    private GameSessionRoot? _root;
    private AtlasRegionDefinition? _region;
    private int _viewRootBuildCount = -1;
    private SiteEventChoicePanelController? _siteEventChoicePanel;
    private int _siteEventChoicePanelBuildCount = -1;

    public event System.Action<AtlasScreenViewState>? ViewStateRendered;

    public AtlasScreenViewState? CurrentState { get; private set; }

    public void EnsureRuntimeControls()
    {
        ResolvePanelHost();
        panelHost.EnsureReady();
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureView();
        Render();
    }

    private void Start()
    {
        EnsureRuntimeControls();
        if (_root != null)
        {
            _root.SessionState.SetCurrentScene(SceneNames.Atlas);
        }
    }

    private void ResolvePanelHost()
    {
        if (panelHost != null)
        {
            return;
        }

        panelHost = GetComponentInParent<RuntimePanelHost>();
        if (panelHost != null)
        {
            return;
        }

        panelHost = FindObjectsByType<RuntimePanelHost>(FindObjectsSortMode.None).FirstOrDefault();
    }

    private void EnsureView()
    {
        EnsureSessionReady();
        _region ??= AtlasGrayboxDataFactory.CreateRegion();
        if (_presenter == null)
        {
            _presenter = _root == null
                ? new AtlasScreenPresenter(_region)
                : new AtlasScreenPresenter(_region, _root.SessionState.EnsureAtlasSession(_region));
            // wave-25 presenter: scout candidate selection을 위한 hero source inject.
            if (_root != null)
            {
                _presenter.SetHeroSource(
                    () => _root.SessionState.Profile.Heroes,
                    () => _root.SessionState.ExpeditionSquadHeroIds ?? Array.Empty<string>(),
                    () => _root.SessionState.BattleDeployHeroIds ?? Array.Empty<string>());
            }
        }
        if (_view != null && _viewRootBuildCount == panelHost.RootBuildCount)
        {
            SyncPresenterFromSession();
            return;
        }

        _view = new AtlasScreenView(panelHost.Root);
        _viewRootBuildCount = panelHost.RootBuildCount;
        _view.SigilSelected += sigilId =>
        {
            SelectSigil(sigilId);
            Render();
        };
        _view.AnchorSelected += hexId =>
        {
            PlaceSelectedSigil(hexId);
            Render();
        };
        _view.StageCandidateSelected += hexId =>
        {
            SelectNode(hexId);
            Render();
        };
        _view.ContinueSelected += ContinueToExpedition;
        // wave-59 mockup-align: 마을 복귀 cross-flow. (정찰은 service 미연결이라 View에서 잠금 처리)
        _view.ReturnTownSelected += OnReturnTownClicked;
        EnsureSiteEventChoicePanel();
        SyncPresenterFromSession();
    }

    private void OnReturnTownClicked()
    {
        if (_root == null) return;
        // 진행 중 expedition은 abandon하지 않고 그대로 두고 Town 진입 (mockup 의도: "마을 복귀" = scene 전환만).
        // active run이 있으면 Town에서 "Resume Expedition" 노출. CanResumeExpedition 정합.
        _root.SceneFlow.GoToTown();
    }

    private void Render()
    {
        if (_view == null || _presenter == null)
        {
            return;
        }

        CurrentState = InjectEnemyIntel(_presenter.Build());
        _view.Render(CurrentState);
        ViewStateRendered?.Invoke(CurrentState);
    }

    private AtlasScreenViewState InjectEnemyIntel(AtlasScreenViewState state)
    {
        return state with
        {
            Preview = state.Preview with
            {
                EnemyIntel = BuildEnemyIntel(),
            },
        };
    }

    private AtlasEnemyIntelViewState BuildEnemyIntel()
    {
        if (_root == null || _region == null)
        {
            return AtlasEnemyIntelViewState.Empty;
        }

        if (!_root.CombatContentLookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            return AtlasEnemyIntelViewState.Empty with
            {
                ForecastLine = "부분 정보: combat content snapshot을 읽을 수 없어 적 징후가 제한됩니다.",
            };
        }

        var selectedNodeId = _presenter.Session.SelectedNodeId;
        var atlasNode = _region.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, selectedNodeId, StringComparison.Ordinal));
        if (atlasNode is not { SiteNodeIndex: >= 0 })
        {
            return AtlasEnemyIntelViewState.Empty with
            {
                ForecastLine = "부분 정보: 선택한 Atlas node는 전투 site node가 아닙니다.",
            };
        }

        var expeditionNode = _root.SessionState.ExpeditionNodes
            .FirstOrDefault(node => node.Index == atlasNode.SiteNodeIndex);
        if (expeditionNode == null)
        {
            return AtlasEnemyIntelViewState.Empty with
            {
                ForecastLine = "부분 정보: 선택한 Atlas node와 연결된 expedition node가 아직 없습니다.",
            };
        }

        var contentText = new ContentTextResolver(_root.Localization, _root.CombatContentLookup);
        return ExpeditionEncounterPreviewBuilder.TryBuild(
            snapshot,
            expeditionNode,
            contentText.GetCharacterName,
            contentText.GetArchetypeName,
            out var preview,
            contentText.GetEncounterName)
            ? BuildEnemyIntel(preview)
            : AtlasEnemyIntelViewState.Empty with
            {
                ForecastLine = "부분 정보: 이 node는 전투 예고가 없는 settlement 또는 보상 node입니다.",
            };
    }

    private static AtlasEnemyIntelViewState BuildEnemyIntel(ExpeditionEncounterPreview preview)
    {
        var enemies = preview.EnemyNames.Count == 0
            ? "적 구성 징후: 미상"
            : $"적 구성 징후: {FormatIntelList(preview.EnemyNames, 3, FormatEnemyName)}";
        var rewards = preview.RewardDropTags.Count == 0
            ? "보상 징후: 미상"
            : $"보상 징후: {FormatIntelList(preview.RewardDropTags, 3, FormatIntelToken)}";
        var boss = string.IsNullOrWhiteSpace(preview.BossOverlayName)
            ? string.Empty
            : $"우두머리 징후: {FormatIntelToken(preview.BossOverlayName)}{FormatOptional(FormatIntelToken(preview.BossAuraTag), " · 오라 ")}{FormatOptional(FormatIntelToken(preview.BossUtilityTag), " · 보조 ")}{FormatBossPressureClock(preview.BossPressureClock)}";

        return new AtlasEnemyIntelViewState(
            true,
            "부분 정보",
            $"예상 정보: {FormatIntelToken(preview.EncounterName)} / {FormatIntelToken(preview.Kind.ToString())} - 정찰 기준",
            enemies,
            $"위협 예상: {preview.ThreatSkulls}단계 · {FormatIntelToken(preview.DifficultyBand)}",
            $"세력 징후: {FormatIntelToken(preview.FactionId)}",
            rewards,
            boss);
    }

    private static string FormatBossPressureClock(BossPressureClockSpec? clock)
    {
        if (clock?.IsEnabled != true)
        {
            return string.Empty;
        }

        return $" · 압박 시계 {clock.FirstPulseSeconds:0.#}초 후, {clock.IntervalSeconds:0.#}초 간격, 최대 체력 {clock.MaxHealthDamageRatio * 100f:0.#}% × {clock.MaxPulses}회";
    }

    private static string FormatIntelList(IEnumerable<string> values, int maxCount, Func<string, string> formatter)
    {
        var formatted = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(formatter)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (formatted.Length == 0)
        {
            return "미상";
        }

        var visible = formatted.Take(maxCount).ToArray();
        var suffix = formatted.Length > visible.Length
            ? $" 외 {(formatted.Length - visible.Length).ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : string.Empty;
        return $"{string.Join(", ", visible)}{suffix}";
    }

    private static string FormatEnemyName(string value)
    {
        var token = value.Trim();
        var slashIndex = token.IndexOf(" / ", StringComparison.Ordinal);
        if (slashIndex > 0)
        {
            token = token[..slashIndex];
        }

        return FormatIntelToken(token);
    }

    private static string FormatIntelToken(string value)
    {
        var readable = HumanizeIdentifier(value);
        return readable switch
        {
            "Ashen Gate Skirmish 1" => "잿빛 관문 접전 1",
            "Skirmish" => "접전",
            "Elite" => "정예",
            "Boss" => "우두머리",
            "Chapter Entry" => "초입",
            "Solarium Border" => "솔라리움 변경",
            "Solarum Border" => "솔라리움 변경",
            "Source Skirmish" => "접전 보상",
            "Family Bastion Front" => "방벽 전열",
            "Answer Lane Guard Anchor" => "전열 고정",
            "Kojin Gate Warden" => "고진 관문 파수꾼",
            "Solarium Sgil Scribe" => "솔라리움 서기관",
            "Solarium Sigil Scribe" => "솔라리움 서기관",
            "Border Irregular Carrier" => "변경 임시 운반자",
            _ => readable,
        };
    }

    private static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var token = value.Trim();
        if (token.Contains('.', StringComparison.Ordinal))
        {
            var parts = token.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = parts.Length - 1; i >= 0; i--)
            {
                if (!string.Equals(parts[i], "name", StringComparison.Ordinal)
                    && !string.Equals(parts[i], "desc", StringComparison.Ordinal))
                {
                    token = parts[i];
                    break;
                }
            }
        }

        foreach (var prefix in new[] { "site_", "encounter_", "faction_", "reward_", "reward_source_", "item_", "augment_", "boss_", "aura_", "utility_" })
        {
            if (token.StartsWith(prefix, StringComparison.Ordinal))
            {
                token = token[prefix.Length..];
                break;
            }
        }

        var words = token.Replace('_', ' ').Replace('-', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0
            ? value
            : string.Join(" ", words.Select(word => char.ToUpperInvariant(word[0]) + (word.Length > 1 ? word[1..] : string.Empty)));
    }

    private static string FormatOptional(string value, string prefix)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : $"{prefix}{value}";
    }

    public bool SelectTileFromWorld(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        ResolvePanelHost();
        panelHost.EnsureReady();
        EnsureView();

        var state = _presenter.Build();
        var tile = state.Tiles.FirstOrDefault(candidate => candidate.NodeId == nodeId);
        if (tile == null)
        {
            return false;
        }

        if (tile.IsSigilAnchor)
        {
            PlaceSelectedSigil(nodeId);
        }
        else
        {
            SelectNode(nodeId);
        }

        Render();
        return true;
    }

    /// <summary>
    /// Atlas 진행 confirm — Atlas 선택을 Expedition 경로로 넘기고 SiteEntered narrative 발화 후
    /// 즉시 Battle 또는 Reward scene으로 진입한다. pindoc://decision-expedition-screen-deprecation-atlas-absorption
    /// 후속 (2단계): ExpeditionScreen UI surface를 거치지 않고 NextBattleOrAdvance 분기를 in-Atlas로 inline.
    /// 이름은 호환을 위해 ContinueToExpedition 유지 (UI binding 영향 회피).
    /// </summary>
    public void ContinueToExpedition()
    {
        EnsureSessionReady();
        if (_root == null)
        {
            return;
        }

        var session = _root.SessionState;
        var selectedBeforeAtlasHandoff = session.GetSelectedExpeditionNode();
        var isAuthoredEventHandoff = selectedBeforeAtlasHandoff?.NodeKind == SiteNodeKindValue.Event;
        if (!isAuthoredEventHandoff
            && _region != null
            && !session.TryApplyAtlasSelectionToExpedition(_region))
        {
            _root.SetBlockingError("Atlas 선택을 Expedition 경로로 넘길 수 없습니다.");
            return;
        }

        var townExitCheckpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
        if (!townExitCheckpoint.IsSuccessful)
        {
            _root.SetBlockingError(townExitCheckpoint.Message);
            return;
        }

        // 발화는 세션: GameSessionState.BeginNewExpedition이 SiteEntered를 발화한다.
        // 여기선 세션이 큐잉한 연출만 화면에 present한다(발화원 통합 → 헤드리스+실게임 동일).
        EnsureStoryBridgeReady();
        _storyBridge?.PresentPending();

        // ExpeditionScreenPresenter.NextBattleOrAdvance 분기를 in-Atlas inline.
        var selectedNode = session.GetSelectedExpeditionNode();
        if (selectedNode == null)
        {
            _root.SetBlockingError("진행할 노드가 선택되지 않았습니다.");
            return;
        }

        if (selectedNode.RequiresBattle)
        {
            session.EnsureBattleDeployReady();

            if (!session.PrepareSelectedBattleNodeHandoff())
            {
                _root.SetBlockingError("노드 진행 준비 실패.");
                return;
            }

            var manualCheckpoint = _root.SaveProfile(SessionCheckpointKind.ManualSave);
            if (!manualCheckpoint.IsSuccessful)
            {
                _root.SetBlockingError(manualCheckpoint.Message);
                return;
            }

            // 출격 편성 확인 게이트 — 빈 배치여도 열어 슬롯 클릭으로 영웅을 다시 채워 출격하게 한다
            // (빈 배치는 게이트의 CanLaunch=false가 출격을 막는다). slot cycle로 배치 조정 + 출격/취소.
            // overlay가 뜨면 이후 warrant/battle 진행은 SortieConfirm 콜백이 잇는다. asset 미배치면 false→직행.
            if (TryShowSortieConfirm())
            {
                return;
            }

            // 게이트 asset이 없을 때만 닿는 직행 fallback — 빈 배치는 여기서 막는다
            // (BuildBattleLoadoutSnapshot이 0 ally면 실패하므로). 게이트가 뜨면 위에서 return되어 안 닿는다.
            if (session.BattleDeployHeroIds.Count == 0)
            {
                _root.SetBlockingError("배치 가능한 영웅이 없습니다.");
                return;
            }

            // ADR-0028 P2b: 출격 전 warrant 선택 surface(UXML 할당 시). 카드=이 서약으로 출격, 스킵=서약 없이.
            // asset 미할당이면 false → 기존대로 직행(흐름 안전, fallback).
            if (TryShowWarrantSelection())
            {
                return;
            }

            _root.SceneFlow.GoToBattle();
            return;
        }

        if (session.ResolveSelectedNodeToRewardSettlement())
        {
            if (session.PendingSiteEvent != null)
            {
                if (!TryShowPendingSiteEvent())
                {
                    _root.SetBlockingError(LocalizeSiteEventUi(
                        "ui.expedition.site_event.error.surface_missing",
                        "사건 선택 화면을 열 수 없습니다."));
                }
                return;
            }

            var manualCheckpoint = _root.SaveProfile(SessionCheckpointKind.ManualSave);
            if (!manualCheckpoint.IsSuccessful)
            {
                _root.SetBlockingError(manualCheckpoint.Message);
                return;
            }

            _root.SceneFlow.GoToReward();
            return;
        }

        _root.SetBlockingError("노드 진행 실패.");
    }

    // ADR-0028 P2b: 출격 서약 선택 overlay를 Atlas panel(RuntimePanelHost.Root) 위에 띄운다.
    // 카드 = 그 서약으로 PledgeWarrant + 출격, 스킵 = 서약 없이 출격. UXML 미할당이면 false → 직행(fallback).
    private bool TryShowWarrantSelection()
    {
        // serialized 할당 우선, 없으면 Resources(Atlas/Resources/WarrantSelection)에서 로드 — scene 와이어 없이 활성.
        var uxml = warrantSelectionUxml != null ? warrantSelectionUxml : Resources.Load<VisualTreeAsset>("WarrantSelection");
        var uss = warrantSelectionUss != null ? warrantSelectionUss : Resources.Load<StyleSheet>("WarrantSelection");
        var root = _root;
        var host = panelHost;
        if (uxml == null || root == null || host == null)
        {
            return false;
        }

        var overlay = uxml.Instantiate();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.top = 0f;
        overlay.style.right = 0f;
        overlay.style.bottom = 0f;
        if (uss != null && !overlay.styleSheets.Contains(uss))
        {
            overlay.styleSheets.Add(uss);
        }

        host.Root.Add(overlay);

        var view = new WarrantSelectionView(overlay);
        // ADR-0028 #b: 이 site의 정치 맥락에서 offer를 도출 — warrant가 "계약 카드"가 아니라 "이 장소의 정치 사건"으로.
        // 미등록 site는 resolver가 전역 정치 카탈로그로 fallback(흐름 안전).
        var siteId = root.SessionState.ActiveRun?.Overlay.SiteId ?? root.SessionState.SelectedCampaignSiteId;
        var siteOffer = SiteWarrantOfferResolver.Resolve(siteId);
        var presenter = new WarrantSelectionPresenter(
            factionId => root.SessionState.Profile.FactionStanding
                .FirstOrDefault(standing => standing.FactionId == factionId)?.Trust ?? 0,
            WarrantDisplayDefaults.FactionName,
            WarrantDisplayDefaults.WarrantName,
            siteOffer.OfferedWarrantIds,
            WarrantDisplayDefaults.SitePressureCause(siteOffer.CauseCode));

        void Proceed(string warrantId)
        {
            host.Root.Remove(overlay);
            root.SessionState.PledgeWarrant(warrantId);
            root.SceneFlow.GoToBattle();
        }

        view.Selected += Proceed;                       // 카드 = 이 서약으로 출격
        view.Confirmed += () => Proceed(string.Empty);  // 스킵 = 서약 없이 출격
        view.Bind();
        view.Render(presenter.BuildState());
        return true;
    }

    // 출격 편성 확인 게이트 overlay — TryShowWarrantSelection 패턴. 배치/시너지를 보여주고 슬롯 클릭으로
    // anchor cycle, 출격 시 warrant 게이트로 체이닝(없으면 GoToBattle). asset 미배치면 false → 직행(fallback).
    private bool TryShowSortieConfirm()
    {
        var uxml = Resources.Load<VisualTreeAsset>("SortieConfirm");
        var uss = Resources.Load<StyleSheet>("SortieConfirm");
        var root = _root;
        var host = panelHost;
        if (uxml == null || root == null || host == null)
        {
            return false;
        }

        var overlay = uxml.Instantiate();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.top = 0f;
        overlay.style.right = 0f;
        overlay.style.bottom = 0f;
        if (uss != null && !overlay.styleSheets.Contains(uss))
        {
            overlay.styleSheets.Add(uss);
        }

        host.Root.Add(overlay);

        var contentText = new ContentTextResolver(root.Localization, root.CombatContentLookup);
        var baselineCatalog = new LaunchCoreRosterBaselineCatalog(root.CombatContentLookup);
        var iconResolver = new ContentIconResolver(root.CombatContentLookup);
        var view = new SortieConfirmView(overlay);
        var presenter = new SortieConfirmPresenter(
            root.SessionState,
            baselineCatalog,
            contentText.GetArchetypeName,
            contentText.GetSynergyName,
            SortieAnchorLabel,
            iconResolver.ResolveCharacterPortrait);

        void CloseOverlay()
        {
            if (host.Root.Contains(overlay))
            {
                host.Root.Remove(overlay);
            }
        }

        view.SlotCycled += anchor =>
        {
            root.SessionState.CycleDeploymentAssignment(anchor);
            view.Render(presenter.BuildState());
        };
        view.Launched += () =>
        {
            CloseOverlay();
            if (!TryShowWarrantSelection())
            {
                root.SceneFlow.GoToBattle();
            }
        };
        view.EditRequested += () =>
        {
            CloseOverlay();
            root.SceneFlow.GoToTown(); // posture/tactic 등 세부 편성은 Town SquadBuilder에서.
        };
        view.Cancelled += CloseOverlay;
        view.Bind();
        view.Render(presenter.BuildState());
        return true;
    }

    private static string SortieAnchorLabel(DeploymentAnchorId anchor) => anchor switch
    {
        DeploymentAnchorId.FrontTop => "전열 상",
        DeploymentAnchorId.FrontCenter => "전열 중",
        DeploymentAnchorId.FrontBottom => "전열 하",
        DeploymentAnchorId.BackTop => "후열 상",
        DeploymentAnchorId.BackCenter => "후열 중",
        DeploymentAnchorId.BackBottom => "후열 하",
        _ => anchor.ToString(),
    };

    private bool EnsureStoryBridgeReady()
    {
        if (_storyBridge != null)
        {
            return true;
        }

        _storyBridge = GetComponent<StorySceneFlowBridge>();
        if (_storyBridge == null)
        {
            _storyBridge = gameObject.AddComponent<StorySceneFlowBridge>();
        }

        return _storyBridge != null;
    }

    private void OnDestroy()
    {
        _siteEventChoicePanel?.Dispose();
        _storyBridge?.ClearPending();
    }

    private void EnsureSiteEventChoicePanel()
    {
        if (_root == null
            || (_siteEventChoicePanel != null
                && _siteEventChoicePanelBuildCount == panelHost.RootBuildCount))
        {
            return;
        }

        _siteEventChoicePanel?.Dispose();
        _siteEventChoicePanel = null;
        _siteEventChoicePanelBuildCount = panelHost.RootBuildCount;
        try
        {
            _siteEventChoicePanel = new SiteEventChoicePanelController(
                panelHost.Root,
                _root.Localization,
                new ContentIconResolver(_root.CombatContentLookup),
                OnSiteEventChoiceSelected);
            if (_root.SessionState.PendingSiteEvent != null)
            {
                TryShowPendingSiteEvent();
            }
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError(exception.Message);
        }
    }

    private bool TryShowPendingSiteEvent()
    {
        EnsureSiteEventChoicePanel();
        var presentation = _root?.SessionState.PendingSiteEvent;
        if (_siteEventChoicePanel == null || presentation == null || _root == null)
        {
            return false;
        }

        var legalChoiceIds = _root.SessionState.SiteEvents.GetLegalActions()
            .Select(action => action.ChoiceId);
        _siteEventChoicePanel.Show(presentation, legalChoiceIds);
        return true;
    }

    private void OnSiteEventChoiceSelected(string choiceId)
    {
        if (_root == null || !_root.SessionState.SiteEvents.ApplyChoice(choiceId))
        {
            _root?.SetBlockingError(LocalizeSiteEventUi(
                "ui.expedition.site_event.error.choice_failed",
                "사건 선택 결과를 적용할 수 없습니다."));
            TryShowPendingSiteEvent();
            return;
        }

        _siteEventChoicePanel?.Hide();
        var checkpoint = _root.SaveProfile(SessionCheckpointKind.ManualSave);
        if (!checkpoint.IsSuccessful)
        {
            _root.SetBlockingError(checkpoint.Message);
            return;
        }

        _root.SceneFlow.GoToReward();
    }

    private string LocalizeSiteEventUi(string key, string fallback)
    {
        return _root?.Localization.LocalizeOrFallback(
                   GameLocalizationTables.UIExpedition,
                   key,
                   fallback)
               ?? fallback;
    }

    private void EnsureSessionReady()
    {
        if (_root != null || !Application.isPlaying)
        {
            return;
        }

        _root = GameSessionRoot.EnsureInstance();
    }

    private void SelectSigil(string sigilId)
    {
        if (_root == null || _region == null)
        {
            _presenter.SelectSigil(sigilId);
            return;
        }

        _root.SessionState.SelectAtlasSigil(_region, sigilId);
        SyncPresenterFromSession();
    }

    private void SelectNode(string nodeId)
    {
        if (_root == null || _region == null)
        {
            _presenter.SelectNode(nodeId);
            return;
        }

        _root.SessionState.SelectAtlasNode(_region, nodeId);
        SyncPresenterFromSession();
    }

    private void PlaceSelectedSigil(string nodeId)
    {
        if (_root == null || _region == null)
        {
            _presenter.PlaceSelectedSigil(nodeId);
            return;
        }

        _root.SessionState.PlaceSelectedAtlasSigil(_region, nodeId);
        SyncPresenterFromSession();
    }

    private void SyncPresenterFromSession()
    {
        if (_root == null || _region == null || _presenter == null)
        {
            return;
        }

        _presenter.SetSession(_root.SessionState.EnsureAtlasSession(_region));
    }
}
