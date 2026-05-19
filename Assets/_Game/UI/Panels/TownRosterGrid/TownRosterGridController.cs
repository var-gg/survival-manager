using SM.Unity.UI.Town.Preview;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Panels.TownRosterGrid;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class TownRosterGridController : MonoBehaviour
{
    [SerializeField] private UIDocument document = null!;
    [SerializeField] private bool openOnStart = true;

    private RosterGridPresenter? _presenter;
    private RosterGridView? _view;

    private void Reset()
    {
        document = GetComponent<UIDocument>();
    }

    private void Start()
    {
        EnsureReady();
        if (openOnStart)
        {
            Open();
        }
    }

    public void Open()
    {
        EnsureReady();
        _view?.Open();
        _presenter?.Refresh();
    }

    public void Close()
    {
        _view?.Close();
    }

    public void ReloadDummyData()
    {
        EnsureReady();
        _presenter?.Refresh();
    }

    private void EnsureReady()
    {
        if (_presenter != null)
        {
            return;
        }

        if (document == null)
        {
            document = GetComponent<UIDocument>();
        }

        var root = GameSessionRoot.EnsureInstance();
        var contentText = new ContentTextResolver(root.Localization, root.CombatContentLookup);
        _view = new RosterGridView(document.rootVisualElement, heroCardTemplate: null);
        _view.BindClose(Close);
        _presenter = new RosterGridPresenter(root, _view, contentText);
        _presenter.Initialize();
    }
}
