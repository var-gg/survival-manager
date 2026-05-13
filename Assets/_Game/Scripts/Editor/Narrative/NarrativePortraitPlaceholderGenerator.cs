using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Narrative;

public static class NarrativePortraitPlaceholderGenerator
{
    private const string AuthoringMapPath = "tools/narrative-authoring-map.json";
    private const string PortraitsRoot = "Assets/Resources/Narrative/Portraits";
    private const int PlaceholderSize = 256;

    [MenuItem("SM/내러티브/포트레이트 플레이스홀더 생성")]
    public static void GeneratePlaceholders()
    {
        if (!File.Exists(AuthoringMapPath))
        {
            Debug.LogError($"[PortraitPlaceholder] Authoring map not found: {AuthoringMapPath}");
            return;
        }

        var json = JObject.Parse(File.ReadAllText(AuthoringMapPath, System.Text.Encoding.UTF8));
        var speakers = json["speakers"] as JObject;
        if (speakers == null)
        {
            Debug.LogError("[PortraitPlaceholder] No speakers found in authoring map.");
            return;
        }

        int created = 0;
        int skipped = 0;

        foreach (var prop in speakers.Properties())
        {
            var speakerId = prop.Value?.Value<string>();
            if (string.IsNullOrWhiteSpace(speakerId) || speakerId == "Narrator")
            {
                continue;
            }

            var folderPath = $"{PortraitsRoot}/{speakerId}";
            var defaultPath = $"{folderPath}/Default.png";

            EnsureFolder(folderPath);

            if (File.Exists(defaultPath))
            {
                skipped++;
                continue;
            }

            var tex = CreatePlaceholderTexture(speakerId);
            var pngBytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            File.WriteAllBytes(defaultPath, pngBytes);
            AssetDatabase.ImportAsset(defaultPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(defaultPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }

            created++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[PortraitPlaceholder] Done. Created={created}, Skipped={skipped} (already exist)");
    }

    private static Texture2D CreatePlaceholderTexture(string characterId, int size = PlaceholderSize)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        var hash = characterId.GetHashCode();
        var hue = Mathf.Abs(hash % 360) / 360f;
        var bgColor = Color.HSVToRGB(hue, 0.25f, 0.35f);

        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = bgColor;
        }

        var centerX = size / 2;
        var centerY = size / 2;
        var radius = size * 0.35f;
        var fgColor = Color.HSVToRGB(hue, 0.15f, 0.55f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var dx = x - centerX;
                var dy = y - centerY;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    pixels[y * size + x] = fgColor;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
