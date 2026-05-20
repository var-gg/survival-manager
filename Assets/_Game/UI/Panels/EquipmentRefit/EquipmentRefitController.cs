using SM.Unity.UI.Town.Preview;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Panels.EquipmentRefit;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class EquipmentRefitController : MonoBehaviour
{
    [SerializeField] private UIDocument document = null!;
    [SerializeField] private bool openOnStart = true;

    private EquipmentRefitPresenter? _presenter;

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
        var view = new EquipmentRefitView(document.rootVisualElement);
        _presenter = new EquipmentRefitPresenter(
            root,
            view,
            contentText,
            itemIconSprite: iconResolver.ResolveItem,
            currencySprite: iconResolver.ResolveAny,
            portraitLoader: iconResolver.ResolveAny,
            affixIconSprite: iconResolver.ResolveAffix);
        _presenter.Initialize();
    }
}
