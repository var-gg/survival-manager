using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// FLAT(web) vs CONSOLE 비교 surface — 동일 USS 레이아웃에 baked PNG chrome(.sm-fx-*) 유무만
/// 다르게 띄워 "콘솔 느낌 = USS layout + PNG 스킨"을 시각 증명하는 dev 도구.
///
/// 콘솔 질감(gloss/glow/depth/bevel)은 ProceduralChromeLibrary 가 구운 디스크 PNG
/// (Sprites/ArtBible/Chrome/*)를 ConsoleCompare.uss 의 .sm-fx-* 클래스가 background-image 로
/// 참조한다 — 순수 USS + 디스크 에셋 (C# 런타임 텍스처 생성 없음, image-gen 비의존).
/// 캡처: TownPreviewCaptureUtility → Screenshots/mockups/console_compare.png.
/// </summary>
public sealed class ConsoleComparePreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Foundation/Components/ConsoleCompare.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";
    private const string CompareUssPath = "Assets/_Game/UI/Foundation/Components/ConsoleCompare.uss";

    [MenuItem("SM/Town/Console Compare 미리보기", false, 14)]
    public static void Open()
    {
        var window = GetWindow<ConsoleComparePreviewBootstrap>("Console Compare");
        window.minSize = new Vector2(1200f, 620f);
    }

    private void CreateGUI() => BuildInto(rootVisualElement);

    /// <summary>EditorWindow + TownPreviewCaptureUtility 공용 — 지정 root 에 비교 surface 빌드.</summary>
    public void BuildInto(VisualElement root)
    {
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath);
        if (tree == null) { root.Add(new Label($"UXML 못 찾음: {VisualTreePath}")); return; }

        foreach (var path in new[] { ThemeTokensPath, RuntimePanelThemePath, CompareUssPath })
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (sheet != null) root.styleSheets.Add(sheet);
        }

        tree.CloneTree(root);
    }
}
