using SM.Unity.UI.Town.Preview;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Panels.InventoryTab;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class InventoryTabController : MonoBehaviour
{
    [SerializeField] private UIDocument document = null!;
    [SerializeField] private bool openOnStart = true;

    private InventoryPresenter? _presenter;

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
        _presenter?.Open();
    }

    public void Close()
    {
        _presenter?.Close();
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
        var iconResolver = new ContentIconResolver(root.CombatContentLookup);
        var view = new InventoryView(document.rootVisualElement);
        _presenter = new InventoryPresenter(
            root,
            view,
            currencySprite: iconResolver.ResolveAny,
            itemIconSprite: iconResolver.ResolveItem,
            contentText: contentText,
            affixIconSprite: iconResolver.ResolveAffix);
        _presenter.Initialize();
    }
}
