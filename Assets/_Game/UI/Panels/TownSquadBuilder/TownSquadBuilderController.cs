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
        // 헤드리스-순수화 배선 — View가 UXML을 감싸고 presenter는 seam(delegate)만 받는다.
        var view = new SquadBuilderView(document.rootVisualElement);
        _presenter = new SquadBuilderPresenter(
            root.SessionState,
            root.CombatContentLookup,
            view,
            () => root.ProfileQueries.GetLoadoutView(root.ActiveProfileId),
            () => root.SaveProfile(),
            contentText.GetClassName,
            contentText.GetRaceName,
            contentText.GetSynergyName,
            contentText.GetRoleName,
            contentText.GetArchetypeName);
        _presenter.Initialize();
    }
}
