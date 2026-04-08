using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SM.Unity.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class RuntimePanelHost : MonoBehaviour
{
    [SerializeField] private PanelSettings panelSettings = null!;
    [SerializeField] private VisualTreeAsset visualTreeAsset = null!;
    [SerializeField] private StyleSheet[] styleSheets = Array.Empty<StyleSheet>();
    [SerializeField] private int sortingOrder;
    [SerializeField] private string rootName = string.Empty;

    private static PanelSettings? s_runtimeFallbackPanelSettings;

    private UIDocument _uidocument = null!;
    private VisualElement _root = null!;
    private bool _visible = true;
    private int _rootBuildCount;

    public VisualElement Root
    {
        get
        {
            EnsureReady();
            return _root;
        }
    }

    public UIDocument CurrentBackend
    {
        get
        {
            EnsureReady();
            return _uidocument;
        }
    }

    public int RootBuildCount => _rootBuildCount;

    public void Configure(
        PanelSettings? resolvedPanelSettings,
        VisualTreeAsset? resolvedVisualTree,
        IReadOnlyList<StyleSheet?> resolvedStyleSheets,
        int resolvedSortingOrder,
        string resolvedRootName)
    {
        panelSettings = resolvedPanelSettings;
        visualTreeAsset = resolvedVisualTree;
        sortingOrder = resolvedSortingOrder;
        rootName = resolvedRootName ?? string.Empty;

        if (resolvedStyleSheets.Count == 0)
        {
            styleSheets = Array.Empty<StyleSheet>();
        }
        else
        {
            var filtered = new List<StyleSheet>(resolvedStyleSheets.Count);
            foreach (var sheet in resolvedStyleSheets)
            {
                if (sheet != null)
                {
                    filtered.Add(sheet);
                }
            }

            styleSheets = filtered.ToArray();
        }

        EnsureReady();
    }

    public void EnsureReady()
    {
        EnsureBackendComponent();
        ApplyEditorFallbacks();

        var resolvedPanelSettings = ResolvePanelSettings();
        if (resolvedPanelSettings != null && _uidocument.panelSettings != resolvedPanelSettings)
        {
            _uidocument.panelSettings = resolvedPanelSettings;
        }

        if (_uidocument.visualTreeAsset != visualTreeAsset)
        {
            _uidocument.visualTreeAsset = visualTreeAsset;
        }

        _uidocument.sortingOrder = sortingOrder;

        var currentRoot = _uidocument.rootVisualElement;
        if (!ReferenceEquals(_root, currentRoot))
        {
            _root = currentRoot;
            _rootBuildCount++;
            InitializeRoot();
        }

        ApplyVisibility();
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        ApplyVisibility();
    }

    public void RefreshPanel()
    {
        EnsureReady();
        _root.MarkDirtyRepaint();
    }

    public void FocusFirstFocusable()
    {
        EnsureReady();

        var stack = new Stack<VisualElement>();
        stack.Push(_root);
        while (stack.Count > 0)
        {
            var element = stack.Pop();
            if (!element.focusable || !element.visible || !element.enabledInHierarchy)
            {
                for (var i = element.childCount - 1; i >= 0; i--)
                {
                    stack.Push(element[i]);
                }

                continue;
            }

            element.Focus();
            return;
        }

        if (_root.focusable && _root.visible && _root.enabledInHierarchy)
        {
            _root.Focus();
        }
    }

    private void Awake()
    {
        EnsureReady();
    }

    private void OnEnable()
    {
        EnsureReady();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureBackendComponent();
            ApplyEditorFallbacks();
        }
    }

    private void EnsureBackendComponent()
    {
        _uidocument ??= GetComponent<UIDocument>();
    }

    private void ApplyEditorFallbacks()
    {
#if UNITY_EDITOR
        if (visualTreeAsset == null && RuntimePanelAssetRegistry.TryGetScreenDescriptor(gameObject.scene.name, out _))
        {
            RuntimePanelAssetRegistry.ConfigureHost(this, gameObject.scene.name);
        }
#endif
    }

    private PanelSettings ResolvePanelSettings()
    {
        if (panelSettings != null)
        {
            return panelSettings;
        }

#if UNITY_EDITOR
        panelSettings = RuntimePanelAssetRegistry.LoadSharedPanelSettings();
        if (panelSettings != null)
        {
            return panelSettings;
        }
#endif

        return s_runtimeFallbackPanelSettings ??= CreateRuntimeFallbackPanelSettings();
    }

    private void InitializeRoot()
    {
        if (_root == null)
        {
            return;
        }

        _root.name = string.IsNullOrWhiteSpace(rootName) ? gameObject.name : rootName;
        _root.style.flexGrow = 1f;

        foreach (var styleSheet in styleSheets)
        {
            if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
            {
                _root.styleSheets.Add(styleSheet);
            }
        }
    }

    private void ApplyVisibility()
    {
        if (_root == null)
        {
            return;
        }

        _root.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
        _root.visible = _visible;
        _root.pickingMode = _visible ? PickingMode.Position : PickingMode.Ignore;
    }

    private static PanelSettings CreateRuntimeFallbackPanelSettings()
    {
        var fallback = ScriptableObject.CreateInstance<PanelSettings>();
        fallback.name = "RuntimePanelHostFallbackSettings";
        fallback.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        fallback.referenceResolution = new Vector2Int(1920, 1080);
        fallback.clearColor = false;
        return fallback;
    }
}
