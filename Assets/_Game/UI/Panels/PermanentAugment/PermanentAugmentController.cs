using SM.Unity.UI.Town.Preview;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Panels.PermanentAugment;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class PermanentAugmentController : MonoBehaviour
{
    [SerializeField] private UIDocument document = null!;
    [SerializeField] private bool openOnStart = true;

    private PermanentAugmentPresenter? _presenter;

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
        var iconResolver = new ContentIconResolver(root.CombatContentLookup);
        var view = new PermanentAugmentView(document.rootVisualElement);
        _presenter = new PermanentAugmentPresenter(root, view, iconResolver.ResolveAny);
        _presenter.Initialize();
    }
}
