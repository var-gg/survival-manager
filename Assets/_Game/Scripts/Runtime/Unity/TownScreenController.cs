using SM.Core;
using SM.Meta;
using SM.Unity.Narrative;
using SM.Unity.UI;
using SM.Unity.UI.TacticalWorkshop;
using SM.Unity.UI.Town;
using UnityEngine;

namespace SM.Unity
{

public sealed class TownScreenController : MonoBehaviour
{
    [SerializeField] private RuntimePanelHost panelHost = null!;
    [SerializeField] private StorySceneFlowBridge _storyBridge = null!;

    private GameSessionRoot _root = null!;
    private GameLocalizationController _localization = null!;
    private ContentTextResolver _contentText = null!;
    private TownScreenPresenter? _presenter;
    private TacticalWorkshopPresenter? _tacticalWorkshopPresenter;
    private SquadBuilderPresenter? _squadBuilderPresenter;

    private void Start()
    {
        if (!EnsureViewReady()) return;

        _localization.LocaleChanged += HandleLocaleChanged;
        _root.SessionState.SetCurrentScene(SceneNames.Town);
        _presenter!.Initialize();
        if (EnsureStoryBridgeReady())
        {
            _storyBridge.Advance(NarrativeMoment.TownEntered, BuildStoryMomentContext());
        }
    }

    private void OnDestroy()
    {
        _tacticalWorkshopPresenter?.Close();
        _storyBridge?.ClearPending();
        if (_localization != null)
        {
            _localization.LocaleChanged -= HandleLocaleChanged;
        }
    }

    public void SaveProfile() => _presenter?.SaveProfile();
    public void LoadProfile() => _presenter?.LoadProfile();
    public void ReturnToStart() => _presenter?.ReturnToStart();
    public void OpenExpedition() => _presenter?.OpenExpedition();
    public void QuickBattle() => _presenter?.QuickBattle();

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

    private bool EnsureViewReady()
    {
        if (!EnsureSessionReady())
        {
            return false;
        }

        if (panelHost == null)
        {
            Debug.LogError("[TownScreenController] Missing RuntimePanelHost reference.");
            return false;
        }

        if (_presenter != null)
        {
            panelHost.EnsureReady();
            return true;
        }

        panelHost.EnsureReady();
        var view = new TownScreenView(panelHost.Root);
        _presenter = new TownScreenPresenter(_root, _localization, _contentText, view);
        _tacticalWorkshopPresenter = new TacticalWorkshopPresenter(panelHost.Root);
        view.BindTacticalWorkshopOpen(_tacticalWorkshopPresenter.Open);
        _squadBuilderPresenter = new SquadBuilderPresenter(panelHost.Root, _root, _contentText);
        view.BindSquadBuilderOpen(_squadBuilderPresenter.Open);
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
            Debug.LogError("[TownScreenController] GameSessionRoot가 없습니다.");
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
}
