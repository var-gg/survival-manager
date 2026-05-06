using P09.Modular.Humanoid.Data;
using UnityEngine;
using UnityEditor;

namespace P09.Modular.Humanoid
{
    [CustomEditor(typeof(RendererEditPartData))]
    public class RendererEditPartDataEditor: Editor
    {
        private RendererEditPartData _rendererEditPartData => (RendererEditPartData) target;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (_rendererEditPartData.Icon == null) return;

            Texture2D sprite = AssetPreview.GetAssetPreview(_rendererEditPartData.Icon);
            GUILayout.Label("", GUILayout.Width(120), GUILayout.Height(120));
            GUI.DrawTexture(GUILayoutUtility.GetLastRect(), sprite);
        }
    }
}