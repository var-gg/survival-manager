using System.IO;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Art;

/// <summary>
/// 절차적 chrome 라이브러리 베이커 — USS 가 네이티브로 못 만드는 콘솔 질감(gloss/glow/depth/
/// bevel/grain)을 C# Texture2D 로 생성해 재사용 가능한 디스크 PNG 로 굽는다 (image-gen 비의존).
/// 산출은 9-slice/stretch 용 white 템플릿이라 USS 에서 -unity-background-image-tint-color 로 색을 입힌다.
///
/// 메뉴: SM/Internal/Art/Bake Procedural Chrome → Assets/_Game/UI/Foundation/Sprites/ArtBible/Chrome/
/// </summary>
public static class ProceduralChromeLibrary
{
    private const string OutputDir = "Assets/_Game/UI/Foundation/Sprites/ArtBible/Chrome";

    [MenuItem("SM/Internal/Art/Bake Procedural Chrome", false, 30)]
    public static void BakeAll()
    {
        Directory.CreateDirectory(OutputDir);

        WritePng("chrome_panel_bevel.png", BakePanelBevel(), TextureWrapMode.Clamp);
        WritePng("chrome_panel_depth.png", BakePanelDepth(), TextureWrapMode.Clamp);
        WritePng("chrome_bar_gloss.png", BakeBarGloss(), TextureWrapMode.Clamp);
        WritePng("chrome_glow_radial.png", BakeRadialGlow(), TextureWrapMode.Clamp);
        WritePng("chrome_parchment.png", BakeParchment(), TextureWrapMode.Repeat);

        AssetDatabase.Refresh();
        Debug.Log($"[ProceduralChrome] 5 chrome 텍스처 baked → {OutputDir}/");
    }

    private static void WritePng(string fileName, Texture2D tex, TextureWrapMode wrap)
    {
        var path = $"{OutputDir}/{fileName}";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    // --- 9-slice 베벨 프레임: top/left 라이트 + bottom/right 셰이드 = 입체 raised 엣지 (slice 20) ---
    private static Texture2D BakePanelBevel()
    {
        const int s = 64, border = 20;
        var tex = NewTex(s, s);
        for (var y = 0; y < s; y++)
        for (var x = 0; x < s; x++)
        {
            float dt = (s - 1) - y; // top 까지 거리 (Unity y=0 bottom)
            float db = y;
            float dl = x;
            float dr = (s - 1) - x;
            var light = Mathf.Max(Falloff(dt, border), Falloff(dl, border));
            var dark = Mathf.Max(Falloff(db, border), Falloff(dr, border));
            var net = light - dark;
            Color c;
            if (net > 0f) c = new Color(1f, 1f, 1f, net * 0.5f);
            else if (net < 0f) c = new Color(0f, 0f, 0f, -net * 0.55f);
            else c = new Color(0f, 0f, 0f, 0f);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    private static float Falloff(float dist, float border) =>
        dist >= border ? 0f : Mathf.Pow(1f - dist / border, 1.5f);

    // --- 세로 depth: 상단 라이트 + 하단 셰이드 (stretch overlay) ---
    private static Texture2D BakePanelDepth()
    {
        const int w = 8, h = 64;
        var tex = NewTex(w, h);
        for (var y = 0; y < h; y++)
        {
            var u = y / (float)(h - 1);
            Color c;
            if (u > 0.66f) c = new Color(1f, 1f, 1f, (u - 0.66f) / 0.34f * 0.14f);
            else if (u < 0.30f) c = new Color(0f, 0f, 0f, (0.30f - u) / 0.30f * 0.34f);
            else c = new Color(0f, 0f, 0f, 0f);
            for (var x = 0; x < w; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    // --- 바 fill 광택: 세로 그라데이션 + 상단 specular (white 템플릿, tint 로 색) ---
    private static Texture2D BakeBarGloss()
    {
        const int w = 8, h = 48;
        var tex = NewTex(w, h);
        for (var y = 0; y < h; y++)
        {
            var u = y / (float)(h - 1);
            var lum = Mathf.Lerp(0.40f, 0.96f, u);
            lum += 0.30f * Mathf.Exp(-Mathf.Pow((u - 0.84f) / 0.05f, 2f));
            lum = Mathf.Clamp01(lum);
            var c = new Color(lum, lum, lum, 1f);
            for (var x = 0; x < w; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    // --- radial glow halo (white 템플릿, tint 로 색) ---
    private static Texture2D BakeRadialGlow()
    {
        const int s = 96;
        var tex = NewTex(s, s);
        var c = (s - 1) / 2f;
        var maxR = (s - 1) / 2f;
        for (var y = 0; y < s; y++)
        for (var x = 0; x < s; x++)
        {
            var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / maxR;
            var a = Mathf.Clamp01(1f - d);
            a = a * a * a;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * 0.9f));
        }
        tex.Apply();
        return tex;
    }

    // --- 양피지 grain: 저진폭 noise, tileable(repeat). 패널 위에 얹어 material 질감 ---
    private static Texture2D BakeParchment()
    {
        const int s = 128;
        var tex = NewTex(s, s);
        for (var y = 0; y < s; y++)
        for (var x = 0; x < s; x++)
        {
            var n = Mathf.PerlinNoise(x * 0.09f, y * 0.09f);
            var fine = Mathf.PerlinNoise(x * 0.37f + 11f, y * 0.37f + 7f);
            var v = (n * 0.65f + fine * 0.35f) - 0.5f; // -0.5 .. 0.5
            // 밝은 grain 은 warm white, 어두운 grain 은 black — 둘 다 저알파
            Color c = v >= 0f
                ? new Color(1f, 0.96f, 0.86f, v * 0.10f)
                : new Color(0f, 0f, 0f, -v * 0.10f);
            tex.SetPixel(x, y, c);
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
