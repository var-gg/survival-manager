using System;
using System.Linq;
using SM.Unity.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.TacticalWorkshop;

public sealed class TacticalWorkshopSandboxController : MonoBehaviour
{
    [SerializeField] private RuntimePanelHost panelHost = null!;

    private TacticalWorkshopPresenter? _presenter;
    private Button? _openButton;
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
        if (_presenter != null && _viewRootBuildCount == panelHost.RootBuildCount)
        {
            return;
        }

        var root = panelHost.Root;
        _presenter = new TacticalWorkshopPresenter(root);
        _openButton = Require<Button>(root, "TwSandboxOpenButton");
        _openButton.clicked += _presenter.Open;
        _viewRootBuildCount = panelHost.RootBuildCount;
        _presenter.Open();
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
