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
        // SeedDemoProfile은 4 hero에만 item을 주므로 dev/preview 진입 시 inventory가 비어있어 panel이
        // 텅 빈 상태가 보이는 케이스가 있다. 모든 hero에 baseline item을 채워 visual surface를 확보한다.
        root.SessionState.SeedDevDemoInventoryIfEmpty();
        var contentText = new ContentTextResolver(root.Localization, root.CombatContentLookup);
        var iconResolver = new ContentIconResolver(root.CombatContentLookup);
        var view = new EquipmentRefitView(document.rootVisualElement);
        _presenter = new EquipmentRefitPresenter(
            root.SessionState,
            root.CombatContentLookup,
            view,
            contentText.GetItemName,
            contentText.GetAffixName,
            contentText.GetCharacterName,
            itemIconSprite: iconResolver.ResolveItem,
            currencySprite: null,
            portraitLoader: iconResolver.ResolveCharacterPortrait,
            affixIconSprite: iconResolver.ResolveAffix);
        _presenter.Initialize();
    }
}
