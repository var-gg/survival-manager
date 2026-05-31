using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// USS Atom Gallery 미리보기 — ThemeTokens + RuntimePanelTheme 의 atom(frame / corner cap /
/// CTA / item rarity / family rib / affix / chip token)을 한 surface 에 모아
/// claude-design CSS → Unity USS 번역 충실도를 시각 확인하는 dev 도구.
///
/// 정적 showcase 이므로 presenter/view 없이 UXML clone 만. 캡처는 TownPreviewCaptureUtility 가
/// BuildInto 를 호출해 RT 렌더 → Screenshots/mockups/uss_atom_gallery.png.
/// </summary>
public sealed class ComponentGalleryPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Foundation/Components/Gallery.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";
    private const string GalleryUssPath = "Assets/_Game/UI/Foundation/Components/Gallery.uss";

    [MenuItem("SM/Town/USS Atom Gallery 미리보기", false, 13)]
    public static void Open()
    {
        var window = GetWindow<ComponentGalleryPreviewBootstrap>("USS Atom Gallery");
        window.minSize = new Vector2(1320f, 800f);
    }

    private void CreateGUI() => BuildInto(rootVisualElement);

    /// <summary>EditorWindow + TownPreviewCaptureUtility 공용 — 지정 root 에 갤러리 빌드.</summary>
    public void BuildInto(VisualElement root)
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath);
        if (visualTree == null) { root.Add(new Label($"UXML 못 찾음: {VisualTreePath}")); return; }

        var tokens = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeTokensPath);
        var theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(RuntimePanelThemePath);
        var gallery = AssetDatabase.LoadAssetAtPath<StyleSheet>(GalleryUssPath);
        if (tokens != null) root.styleSheets.Add(tokens);
        if (theme != null) root.styleSheets.Add(theme);
        if (gallery != null) root.styleSheets.Add(gallery);

        visualTree.CloneTree(root);
    }
}
