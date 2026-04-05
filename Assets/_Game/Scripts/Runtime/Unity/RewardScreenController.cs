using SM.Unity.UI;
using SM.Unity.UI.Reward;
using UnityEngine;

namespace SM.Unity;

public sealed class RewardScreenController : MonoBehaviour
{
    [SerializeField] private RuntimePanelHost panelHost = null!;

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
    }

    private void OnDestroy()
    {
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

    private void HandleLocaleChanged(UnityEngine.Localization.Locale _)
    {
        _presenter?.Refresh();
    }
}
