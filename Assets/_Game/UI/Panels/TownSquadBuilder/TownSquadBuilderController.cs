using SM.Unity.UI.Town;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Panels.TownSquadBuilder;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class TownSquadBuilderController : MonoBehaviour
{
    [SerializeField] private UIDocument document = null!;
    [SerializeField] private bool openOnStart = true;

    private SquadBuilderPresenter? _presenter;

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
        _presenter?.Open();
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
        _presenter = new SquadBuilderPresenter(document.rootVisualElement, root, contentText);
    }
}
