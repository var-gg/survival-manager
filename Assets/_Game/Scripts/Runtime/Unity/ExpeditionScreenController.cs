using SM.Core;
using SM.Meta;
using SM.Unity.Narrative;
using SM.Unity.UI;
using SM.Unity.UI.Expedition;
using UnityEngine;

namespace SM.Unity;

public sealed class ExpeditionScreenController : MonoBehaviour
{
    [SerializeField] private RuntimePanelHost panelHost = null!;
    [SerializeField] private StorySceneFlowBridge _storyBridge = null!;

    private GameSessionRoot _root = null!;
    private GameLocalizationController _localization = null!;
    private ContentTextResolver _contentText = null!;
    private ExpeditionScreenPresenter? _presenter;

    private void Start()
    {
        if (!EnsureViewReady())
        {
            return;
        }

        _localization.LocaleChanged += HandleLocaleChanged;
        _root.SessionState.SetCurrentScene(SceneNames.Expedition);
        _presenter!.Initialize();
        if (EnsureStoryBridgeReady())
        {
            _storyBridge.Advance(NarrativeMoment.SiteEntered, BuildStoryMomentContext());
        }
    }

    private void OnDestroy()
    {
        _storyBridge?.ClearPending();
        if (_localization != null)
        {
            _localization.LocaleChanged -= HandleLocaleChanged;
        }
    }

    public void SelectNode1() => _presenter?.SelectNode1();
    public void SelectNode2() => _presenter?.SelectNode2();
    public void SelectNode3() => _presenter?.SelectNode3();
    public void SelectNode4() => _presenter?.SelectNode4();
    public void SelectNode5() => _presenter?.SelectNode5();
    public void NextBattle() => _presenter?.NextBattleOrAdvance();
    public void NextBattleOrAdvance() => _presenter?.NextBattleOrAdvance();
    public void ReturnToTown() => _presenter?.ReturnToTown();

    public void EnsureRuntimeControls()
    {
        if (panelHost != null)
        {
            panelHost.EnsureReady();
        }

        if (!Application.isPlaying)
        {
            return;
        }

        EnsureViewReady();
    }

    public void CycleFrontTop() => _presenter?.CycleFrontTop();
    public void CycleFrontCenter() => _presenter?.CycleFrontCenter();
    public void CycleFrontBottom() => _presenter?.CycleFrontBottom();
    public void CycleBackTop() => _presenter?.CycleBackTop();
    public void CycleBackCenter() => _presenter?.CycleBackCenter();
    public void CycleBackBottom() => _presenter?.CycleBackBottom();
    public void CycleTeamPosture() => _presenter?.CycleTeamPosture();

    private bool EnsureViewReady()
    {
        if (!EnsureSessionReady())
        {
            return false;
        }

        if (panelHost == null)
        {
            Debug.LogError("[ExpeditionScreenController] Missing RuntimePanelHost reference.");
            return false;
        }

        if (_presenter != null)
        {
            panelHost.EnsureReady();
            return true;
        }

        panelHost.EnsureReady();
        var view = new ExpeditionScreenView(panelHost.Root);
        _presenter = new ExpeditionScreenPresenter(_root, _localization, _contentText, view);
        return true;
    }

    private bool EnsureSessionReady()
    {
        if (_root != null)
        {
            return true;
        }

        _root = GameSessionRoot.EnsureInstance();
        if (_root == null)
        {
            Debug.LogError("[ExpeditionScreenController] GameSessionRoot가 없습니다.");
            return false;
        }

        _localization = _root.Localization;
        _contentText = new ContentTextResolver(_localization, _root.CombatContentLookup);
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
        var session = _root.SessionState;
        return new StoryMomentContext
        {
            ChapterId = session.SelectedCampaignChapterId,
            SiteId = session.SelectedCampaignSiteId,
            NodeIndex = session.CurrentExpeditionNodeIndex,
        };
    }

    private void HandleLocaleChanged(UnityEngine.Localization.Locale _)
    {
        _presenter?.Refresh();
    }
}
