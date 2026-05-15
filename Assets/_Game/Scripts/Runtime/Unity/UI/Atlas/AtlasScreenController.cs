using System.Linq;
using SM.Atlas.Model;
using SM.Atlas.Services;
using UnityEngine;

namespace SM.Unity.UI.Atlas;

public sealed class AtlasScreenController : MonoBehaviour
{
    [SerializeField] private RuntimePanelHost panelHost = null!;

    private AtlasScreenPresenter _presenter = null!;
    private AtlasScreenView _view = null!;
    private GameSessionRoot? _root;
    private AtlasRegionDefinition? _region;
    private int _viewRootBuildCount = -1;

    public event System.Action<AtlasScreenViewState>? ViewStateRendered;

    public AtlasScreenViewState? CurrentState { get; private set; }

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
        if (_root != null)
        {
            _root.SessionState.SetCurrentScene(SceneNames.Atlas);
        }
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
        EnsureSessionReady();
        _region ??= AtlasGrayboxDataFactory.CreateRegion();
        _presenter ??= _root == null
            ? new AtlasScreenPresenter(_region)
            : new AtlasScreenPresenter(_region, _root.SessionState.EnsureAtlasSession(_region));
        if (_view != null && _viewRootBuildCount == panelHost.RootBuildCount)
        {
            SyncPresenterFromSession();
            return;
        }

        _view = new AtlasScreenView(panelHost.Root);
        _viewRootBuildCount = panelHost.RootBuildCount;
        _view.SigilSelected += sigilId =>
        {
            SelectSigil(sigilId);
            Render();
        };
        _view.AnchorSelected += hexId =>
        {
            PlaceSelectedSigil(hexId);
            Render();
        };
        _view.StageCandidateSelected += hexId =>
        {
            SelectNode(hexId);
            Render();
        };
        _view.ContinueSelected += ContinueToExpedition;
        SyncPresenterFromSession();
    }

    private void Render()
    {
        if (_view == null || _presenter == null)
        {
            return;
        }

        CurrentState = _presenter.Build();
        _view.Render(CurrentState);
        ViewStateRendered?.Invoke(CurrentState);
    }

    public bool SelectTileFromWorld(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        ResolvePanelHost();
        panelHost.EnsureReady();
        EnsureView();

        var state = _presenter.Build();
        var tile = state.Tiles.FirstOrDefault(candidate => candidate.NodeId == nodeId);
        if (tile == null)
        {
            return false;
        }

        if (tile.IsSigilAnchor)
        {
            PlaceSelectedSigil(nodeId);
        }
        else
        {
            SelectNode(nodeId);
        }

        Render();
        return true;
    }

    public void ContinueToExpedition()
    {
        EnsureSessionReady();
        if (_root == null)
        {
            return;
        }

        var checkpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
        if (!checkpoint.IsSuccessful)
        {
            _root.SetBlockingError(checkpoint.Message);
            return;
        }

        _root.SceneFlow.GoToExpedition();
    }

    private void EnsureSessionReady()
    {
        if (_root != null || !Application.isPlaying)
        {
            return;
        }

        _root = GameSessionRoot.EnsureInstance();
    }

    private void SelectSigil(string sigilId)
    {
        if (_root == null || _region == null)
        {
            _presenter.SelectSigil(sigilId);
            return;
        }

        _root.SessionState.SelectAtlasSigil(_region, sigilId);
        SyncPresenterFromSession();
    }

    private void SelectNode(string nodeId)
    {
        if (_root == null || _region == null)
        {
            _presenter.SelectNode(nodeId);
            return;
        }

        _root.SessionState.SelectAtlasNode(_region, nodeId);
        SyncPresenterFromSession();
    }

    private void PlaceSelectedSigil(string nodeId)
    {
        if (_root == null || _region == null)
        {
            _presenter.PlaceSelectedSigil(nodeId);
            return;
        }

        _root.SessionState.PlaceSelectedAtlasSigil(_region, nodeId);
        SyncPresenterFromSession();
    }

    private void SyncPresenterFromSession()
    {
        if (_root == null || _region == null || _presenter == null)
        {
            return;
        }

        _presenter.SetSession(_root.SessionState.EnsureAtlasSession(_region));
    }
}
