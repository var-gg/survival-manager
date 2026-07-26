using System.Linq;
using SM.Unity.UI.Town.Preview;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Panels.PassiveBoard;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class PassiveBoardController : MonoBehaviour
{
    [SerializeField] private UIDocument document = null!;
    [SerializeField] private bool openOnStart = true;

    private PassiveBoardPresenter? _presenter;

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
        var view = new PassiveBoardView(document.rootVisualElement);
        _presenter = new PassiveBoardPresenter(
            root,
            view,
            contentText,
            classSprite: null,
            affixSprite: iconResolver.ResolveAffix);

        var firstHeroId = root.SessionState.Profile.Heroes.FirstOrDefault()?.HeroId;
        if (!string.IsNullOrEmpty(firstHeroId))
        {
            _presenter.SetSelectedHero(firstHeroId);
        }

        _presenter.Initialize();
    }
}
