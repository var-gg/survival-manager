using System;
using System.Linq;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Core;
using SM.Meta;
using SM.Unity.Narrative;
using SM.Unity.UI.Expedition;
using UnityEngine;

namespace SM.Unity.UI.Atlas;

public sealed class AtlasScreenController : MonoBehaviour
{
    [SerializeField] private RuntimePanelHost panelHost = null!;
    [SerializeField] private StorySceneFlowBridge? _storyBridge;

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
        _presenter ??= _root == null
            ? new AtlasScreenPresenter(_region)
            : new AtlasScreenPresenter(_region, _root.SessionState.EnsureAtlasSession(_region));
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
        SyncPresenterFromSession();
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
            : $"적 구성 징후: {string.Join(", ", preview.EnemyNames)}";
        var rewards = preview.RewardDropTags.Count == 0
            ? "보상 징후: 미상"
            : $"보상 징후: {string.Join(", ", preview.RewardDropTags.Take(5).Select(HumanizeIdentifier))}";
        var boss = string.IsNullOrWhiteSpace(preview.BossOverlayName)
            ? string.Empty
            : $"boss overlay 징후: {preview.BossOverlayName}{FormatOptional(HumanizeIdentifier(preview.BossAuraTag), " · aura ")}{FormatOptional(HumanizeIdentifier(preview.BossUtilityTag), " · utility ")}";

        return new AtlasEnemyIntelViewState(
            true,
            "부분 정보",
            $"예상 정보: {preview.EncounterName} ({preview.Kind}) - 정찰 기준",
            enemies,
            $"위협 예상: {preview.ThreatSkulls} skull · {preview.DifficultyBand}",
            $"세력 징후: {HumanizeIdentifier(preview.FactionId)}",
            rewards,
            boss);
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

        foreach (var prefix in new[] { "faction_", "reward_", "reward_source_", "item_", "augment_", "boss_", "aura_", "utility_" })
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
