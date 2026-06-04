using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Core;
using SM.Meta;
using SM.Unity.Narrative;
using SM.Unity.UI.Expedition;
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
        // wave-59 mockup-align: 정찰 / 마을 복귀 cross-flow.
        _view.ScoutSelected += OnScoutClicked;
        _view.ReturnTownSelected += OnReturnTownClicked;
        SyncPresenterFromSession();
    }

    private void OnScoutClicked()
    {
        // 정찰 (-35 Echo)은 wave-61+ 본격 wire. 본 wave는 surface 노출만 — Debug.Log placeholder.
        // SessionState.UseScoutDirective 또는 별도 ScoutFlow가 후속 정합 단계에서 적용된다.
        Debug.Log("[AtlasScreen] 정찰 wire 미연결 — surface UI만 노출. 후속 wave에서 Atlas 정찰 service wire.");
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
            : $"우두머리 징후: {FormatIntelToken(preview.BossOverlayName)}{FormatOptional(FormatIntelToken(preview.BossAuraTag), " · 오라 ")}{FormatOptional(FormatIntelToken(preview.BossUtilityTag), " · 보조 ")}";

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

        if (_region != null && !_root.SessionState.TryApplyAtlasSelectionToExpedition(_region))
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

        // narrative SiteEntered — Atlas 확정 시점에 발화 (이전엔 ExpeditionScreenController.Start에서 처리).
        EnsureStoryBridgeReady();
        _storyBridge?.Advance(NarrativeMoment.SiteEntered, BuildStoryMomentContext());

        // ExpeditionScreenPresenter.NextBattleOrAdvance 분기를 in-Atlas inline.
        var session = _root.SessionState;
        var selectedNode = session.GetSelectedExpeditionNode();
        if (selectedNode == null)
        {
            _root.SetBlockingError("진행할 노드가 선택되지 않았습니다.");
            return;
        }

        if (selectedNode.RequiresBattle)
        {
            session.EnsureBattleDeployReady();
            if (session.BattleDeployHeroIds.Count == 0)
            {
                _root.SetBlockingError("배치 가능한 영웅이 없습니다.");
                return;
            }

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
        var uxml = warrantSelectionUxml;
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
        if (warrantSelectionUss != null && !overlay.styleSheets.Contains(warrantSelectionUss))
        {
            overlay.styleSheets.Add(warrantSelectionUss);
        }

        host.Root.Add(overlay);

        var view = new WarrantSelectionView(overlay);
        var presenter = new WarrantSelectionPresenter(
            factionId => root.SessionState.Profile.FactionStanding
                .FirstOrDefault(standing => standing.FactionId == factionId)?.Trust ?? 0,
            WarrantDisplayDefaults.FactionName,
            WarrantDisplayDefaults.WarrantName);

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

    private StoryMomentContext BuildStoryMomentContext()
    {
        var session = _root!.SessionState;
        return new StoryMomentContext
        {
            ChapterId = session.SelectedCampaignChapterId,
            SiteId = session.SelectedCampaignSiteId,
            NodeIndex = session.CurrentExpeditionNodeIndex,
        };
    }

    private void OnDestroy()
    {
        _storyBridge?.ClearPending();
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
