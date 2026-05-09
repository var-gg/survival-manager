using System.Linq;
using SM.Atlas.Services;
using UnityEngine;

namespace SM.Unity.UI.Atlas;

public sealed class AtlasScreenController : MonoBehaviour
{
    [SerializeField] private RuntimePanelHost panelHost = null!;

    private AtlasScreenPresenter _presenter = null!;
    private AtlasScreenView _view = null!;
    private int _viewRootBuildCount = -1;

    public void EnsureRuntimeControls()
    {
        ResolvePanelHost();
        panelHost.EnsureReady();
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureView();
        Render();
    }

    private void Start()
    {
        EnsureRuntimeControls();
    }

    private void ResolvePanelHost()
    {
        if (panelHost != null)
        {
            return;
        }

        panelHost = GetComponentInParent<RuntimePanelHost>();
        if (panelHost != null)
        {
            return;
        }

        panelHost = FindObjectsByType<RuntimePanelHost>(FindObjectsSortMode.None).FirstOrDefault();
    }

    private void EnsureView()
    {
        _presenter ??= new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion());
        if (_view != null && _viewRootBuildCount == panelHost.RootBuildCount)
        {
            return;
        }

        _view = new AtlasScreenView(panelHost.Root);
        _viewRootBuildCount = panelHost.RootBuildCount;
        _view.NodeSelected += nodeId =>
        {
            _presenter.SelectNode(nodeId);
            Render();
        };
        _view.AnchorSelected += nodeId =>
        {
            _presenter.PlaceSelectedSigil(nodeId);
            Render();
        };
        _view.SigilSelected += sigilId =>
        {
            _presenter.SelectSigil(sigilId);
            Render();
        };
        _view.RouteSelected += routeId =>
        {
            _presenter.SelectRoute(routeId);
            Render();
        };
    }

    private void Render()
    {
        if (_view == null || _presenter == null)
        {
            return;
        }

        _view.Render(_presenter.Build());
    }
}
