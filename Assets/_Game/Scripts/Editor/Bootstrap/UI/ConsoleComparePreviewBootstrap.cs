using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// FLAT(web) vs CONSOLE 비교 surface — 동일 USS 레이아웃에 baked PNG chrome 유무만 다르게 띄워
/// "콘솔 느낌 = USS layout + 절차적 PNG 스킨" 임을 시각 증명하는 dev 도구.
///
/// 핵심: 콘솔 질감(gloss/glow/depth)은 image-gen 이나 손그림 없이 C# 에서 Texture2D 로 절차 생성해
/// background-image 로 입힌다. USS 가 못 하는 gradient/glow/bevel 을 PNG 레이어로 우회하는 패턴.
/// 캡처는 TownPreviewCaptureUtility (→ Screenshots/mockups/console_compare.png).
/// </summary>
public sealed class ConsoleComparePreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Foundation/Components/ConsoleCompare.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";
    private const string CompareUssPath = "Assets/_Game/UI/Foundation/Components/ConsoleCompare.uss";

    // ThemeTokens 와 동일한 가족/골드 색 (gloss tint 용).
    private static readonly Color Striker = new(182f / 255f, 51f / 255f, 74f / 255f, 1f);
    private static readonly Color Vanguard = new(74f / 255f, 114f / 255f, 184f / 255f, 1f);
    private static readonly Color Gold300 = new(230f / 255f, 183f / 255f, 81f / 255f, 1f);
    private static readonly Color Gold200 = new(245f / 255f, 214f / 255f, 138f / 255f, 1f);

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

        // 절차적 chrome 텍스처 — USS 가 못 하는 gloss/glow/depth 를 PNG 로 baking.
        var gloss = BakeBarGloss();
        var glow = BakeRadialGlow();
        var depth = BakePanelDepth();

        ApplyTexture(root, "cpanel_depth", depth, null);
        ApplyTexture(root, "cslot_glow", glow, Gold200);
        ApplyTexture(root, "cbar_hp", gloss, Striker);
        ApplyTexture(root, "cbar_energy", gloss, Vanguard);
        ApplyTexture(root, "cbar_xp", gloss, Gold300);
    }

    private static void ApplyTexture(VisualElement root, string elementName, Texture2D texture, Color? tint)
    {
        var element = root.Q(elementName);
        if (element == null) return;
        element.style.backgroundImage = new StyleBackground(texture);
        element.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
        if (tint.HasValue) element.style.unityBackgroundImageTintColor = tint.Value;
    }

    /// <summary>세로 그라데이션 + 상단 specular = 광택 바 fill (white 템플릿, tint 로 색 입힘).</summary>
    private static Texture2D BakeBarGloss()
    {
        const int w = 8, h = 48;
        var tex = NewTex(w, h);
        for (var y = 0; y < h; y++)
        {
            var u = y / (float)(h - 1); // 0 bottom .. 1 top
            var lum = Mathf.Lerp(0.40f, 0.96f, u);
            lum += 0.30f * Mathf.Exp(-Mathf.Pow((u - 0.84f) / 0.05f, 2f)); // 상단 specular band
            lum = Mathf.Clamp01(lum);
            var c = new Color(lum, lum, lum, 1f);
            for (var x = 0; x < w; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    /// <summary>중심 → 가장자리 soft falloff = 선택/강조 glow halo (white, tint 로 색 입힘).</summary>
    private static Texture2D BakeRadialGlow()
    {
        const int s = 80;
        var tex = NewTex(s, s);
        var c = (s - 1) / 2f;
        var maxR = (s - 1) / 2f;
        for (var y = 0; y < s; y++)
        for (var x = 0; x < s; x++)
        {
            var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / maxR;
            var a = Mathf.Clamp01(1f - d);
            a = a * a * a; // soft
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * 0.9f));
        }
        tex.Apply();
        return tex;
    }

    /// <summary>상단 라이트 + 하단 셰이드 세로 그라데이션 = 패널 depth overlay (box-shadow 없이 입체).</summary>
    private static Texture2D BakePanelDepth()
    {
        const int w = 8, h = 64;
        var tex = NewTex(w, h);
        for (var y = 0; y < h; y++)
        {
            var u = y / (float)(h - 1); // 0 bottom .. 1 top
            Color c;
            if (u > 0.66f) c = new Color(1f, 1f, 1f, (u - 0.66f) / 0.34f * 0.14f);      // 상단 라이트
            else if (u < 0.30f) c = new Color(0f, 0f, 0f, (0.30f - u) / 0.30f * 0.34f); // 하단 셰이드
            else c = new Color(0f, 0f, 0f, 0f);
            for (var x = 0; x < w; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D NewTex(int w, int h) =>
        new(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
}
