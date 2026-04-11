using SM.Core;
using SM.Meta;
using SM.Unity.Narrative;
using SM.Unity.UI;
using SM.Unity.UI.Reward;
using UnityEngine;

namespace SM.Unity;

public sealed class RewardScreenController : MonoBehaviour
{
    [SerializeField] private RuntimePanelHost panelHost = null!;
    [SerializeField] private StorySceneFlowBridge _storyBridge = null!;

    private GameSessionRoot _root = null!;
    private GameLocalizationController _localization = null!;
    private ContentTextResolver _contentText = null!;
    private RewardScreenPresenter? _presenter;

    private void Start()
    {
        if (!EnsureViewReady())
        {
            return;
        }

        _localization.LocaleChanged += HandleLocaleChanged;
        _root.SessionState.SetCurrentScene(SceneNames.Reward);
        _presenter!.Initialize();
        _presenter.RewardChoiceCommitted += HandleRewardChoiceCommitted;
        if (EnsureStoryBridgeReady())
        {
            _storyBridge.Advance(NarrativeMoment.RewardOpened, BuildStoryMomentContext());
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

    public void Choose0() => _presenter?.Choose0();
    public void Choose1() => _presenter?.Choose1();
    public void Choose2() => _presenter?.Choose2();
    public void ReturnToTown() => _presenter?.ReturnToTown();

    private bool EnsureViewReady()
    {
        if (!EnsureSessionReady())
        {
            return false;
        }

        if (panelHost == null)
        {
            Debug.LogError("[RewardScreenController] Missing RuntimePanelHost reference.");
            return false;
        }

        if (_presenter != null)
        {
            panelHost.EnsureReady();
            return true;
        }

        panelHost.EnsureReady();
        var view = new RewardScreenView(panelHost.Root);
        _presenter = new RewardScreenPresenter(_root, _localization, _contentText, view);
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
            Debug.LogError("[RewardScreenController] GameSessionRoot가 없습니다.");
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

    private void HandleRewardChoiceCommitted(int choiceIndex)
    {
        if (!EnsureStoryBridgeReady())
        {
            return;
        }

        _storyBridge.Advance(NarrativeMoment.RewardCommitted, BuildStoryMomentContext());
    }

    private void HandleLocaleChanged(UnityEngine.Localization.Locale _)
    {
        _presenter?.Refresh();
    }
}
