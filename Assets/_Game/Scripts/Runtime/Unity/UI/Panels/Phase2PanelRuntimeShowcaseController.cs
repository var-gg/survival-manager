using System;
using SM.Unity.UI.Panels.EquipmentRefit;
using SM.Unity.UI.Panels.InventoryTab;
using SM.Unity.UI.Panels.RecruitPack;
using SM.Unity.UI.Panels.SkillCompendium;
using SM.Unity.UI.Panels.TacticalWorkshop;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Panels;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class Phase2PanelRuntimeShowcaseController : MonoBehaviour
{
    [SerializeField] private UIDocument document = null!;
    [SerializeField] private PanelSettings panelSettings = null!;
    [SerializeField] private PanelEntry[] panels = Array.Empty<PanelEntry>();
    [SerializeField] private int startIndex;

    private Component? _activeController;
    private int _activeIndex = -1;

    public void Configure(UIDocument resolvedDocument, PanelSettings resolvedPanelSettings, PanelEntry[] resolvedPanels, int initialIndex = 0)
    {
        document = resolvedDocument;
        panelSettings = resolvedPanelSettings;
        panels = resolvedPanels ?? Array.Empty<PanelEntry>();
        startIndex = Mathf.Clamp(initialIndex, 0, Mathf.Max(0, panels.Length - 1));
    }

    private void Reset()
    {
        document = GetComponent<UIDocument>();
    }

    private void Start()
    {
        EnsureDocument();
        Show(startIndex);
    }

    private void Update()
    {
        if (panels.Length == 0)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            Show((_activeIndex + 1 + panels.Length) % panels.Length);
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            Show((_activeIndex - 1 + panels.Length) % panels.Length);
        }
        else
        {
            for (var i = 0; i < panels.Length && i < 9; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                {
                    Show(i);
                    break;
                }
            }
        }
    }

    private void Show(int index)
    {
        if (panels.Length == 0)
        {
            _activeIndex = -1;
            return;
        }

        EnsureDocument();
        _activeIndex = Mathf.Clamp(index, 0, panels.Length - 1);
        var entry = panels[_activeIndex];
        if (entry.VisualTreeAsset == null)
        {
            Debug.LogWarning($"[Phase2PanelShowcase] Missing UXML for {entry.Id}");
            return;
        }

        if (_activeController != null)
        {
            Destroy(_activeController);
            _activeController = null;
        }

        document.visualTreeAsset = entry.VisualTreeAsset;
        document.rootVisualElement.style.display = DisplayStyle.Flex;
        _activeController = gameObject.AddComponent(ResolveControllerType(entry.Kind));
    }

    private void EnsureDocument()
    {
        if (document == null)
        {
            document = GetComponent<UIDocument>();
        }

        if (panelSettings != null && document.panelSettings != panelSettings)
        {
            document.panelSettings = panelSettings;
        }
    }

    private static Type ResolveControllerType(PanelKind kind) => kind switch
    {
        PanelKind.SkillCompendium => typeof(SkillCompendiumController),
        PanelKind.TacticalWorkshop => typeof(TacticalWorkshopController),
        PanelKind.RecruitPack => typeof(RecruitPackController),
        PanelKind.EquipmentRefit => typeof(EquipmentRefitController),
        PanelKind.InventoryTab => typeof(InventoryTabController),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    [Serializable]
    public struct PanelEntry
    {
        public string Id;
        public PanelKind Kind;
        public VisualTreeAsset VisualTreeAsset;
    }

    public enum PanelKind
    {
        SkillCompendium,
        TacticalWorkshop,
        RecruitPack,
        EquipmentRefit,
        InventoryTab,
    }
}
