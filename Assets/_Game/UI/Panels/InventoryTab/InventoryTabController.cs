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
        // Inventory panel도 EquipmentRefit과 마찬가지로 SeedDemoProfile의 4-item로는 dev/preview 진입 시
        // pool이 빈약하게 보인다. 모든 hero에 baseline item 1개씩 채워 시각 표면 확보.
        root.SessionState.SeedDevDemoInventoryIfEmpty();
        var contentText = new ContentTextResolver(root.Localization, root.CombatContentLookup);
        var iconResolver = new ContentIconResolver(root.CombatContentLookup);
        var view = new InventoryView(document.rootVisualElement);
        _presenter = new InventoryPresenter(
            root.SessionState,
            root.CombatContentLookup,
            view,
            currencySprite: null,
            itemIconSprite: iconResolver.ResolveItem,
            contentText: contentText,
            affixIconSprite: iconResolver.ResolveAffix);
        _presenter.Initialize();
    }
}
