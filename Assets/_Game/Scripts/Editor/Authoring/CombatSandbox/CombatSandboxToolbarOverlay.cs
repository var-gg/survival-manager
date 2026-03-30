using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace SM.Editor.Authoring.CombatSandbox;

[Overlay(typeof(SceneView), "Combat Sandbox")]
public sealed class CombatSandboxToolbarOverlay : Overlay
{
    public override VisualElement CreatePanelContent()
    {
        var root = new VisualElement();
        var openButton = new Button(CombatSandboxWindow.OpenWindow) { text = "Open Sandbox" };
        root.Add(openButton);
        return root;
    }
}
