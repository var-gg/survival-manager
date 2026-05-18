using SM.Unity.UI.Town.Preview;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Panels.TacticalWorkshop;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class TacticalWorkshopController : MonoBehaviour
{
    [SerializeField] private UIDocument document = null!;
    [SerializeField] private bool openOnStart = true;

    private TacticalWorkshopPresenter? _presenter;

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
        document.rootVisualElement.style.display = DisplayStyle.Flex;
        _presenter?.Refresh();
    }

    public void Close()
    {
        document.rootVisualElement.style.display = DisplayStyle.None;
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
        var view = new TacticalWorkshopView(document.rootVisualElement);
        var iconResolver = new ContentIconResolver(root.CombatContentLookup);
        _presenter = new TacticalWorkshopPresenter(
            root,
            view,
            iconResolver.ResolveAny,
            iconResolver.ResolveAny,
            iconResolver.ResolveAny);
        _presenter.Initialize();
    }
}
