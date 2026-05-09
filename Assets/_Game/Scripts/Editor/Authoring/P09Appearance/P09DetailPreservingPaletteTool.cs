using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.P09Appearance;

public static class P09DetailPreservingPaletteTool
{
    private const string VisualPrefabPath = "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/Demo_Prefab/P09_Human_Combat_Demo Variant.prefab";
    private const string PreviewReadableShaderName = "Hidden/SM/P09PreviewTintedUnlit";
    private const string OutputFolder = "Logs/p09-fullbody";
    private const int RenderWidth = 1200;
    private const int RenderHeight = 1600;
    private static readonly Color ContactSheetBackgroundColor = new(0.68f, 0.79f, 0.88f, 1f);

    [MenuItem("SM/Internal/P09/Apply Detail Preserving Palettes")]
    public static void ApplyDetailPreservingPalettesMenu()
    {
        ApplyDetailPreservingPalettes();
    }

    [MenuItem("SM/Internal/P09/Export Detail Preserving Full Body Crops")]
    public static void ExportFullBodyCropsMenu()
    {
        ExportFullBodyCrops();
    }

    [MenuItem("SM/Internal/P09/Apply Palettes And Export Crops")]
    public static void ApplyPalettesAndExportCropsForAutomation()
    {
        ApplyDetailPreservingPalettes();
        ExportFullBodyCrops();
    }

    public static IReadOnlyList<string> ApplyDetailPreservingPalettes()
    {
        var catalog = BattleP09AppearanceCatalogBuilder.EnsureCatalog();
        var updated = new List<string>();
        foreach (var spec in BuildAllSpecs())
        {
            var preset = EnsurePreset(spec.CharacterId);
            preset.ConfigureIdentity(spec.CharacterId, spec.DisplayName, catalog);
            ApplyPartIds(preset, spec);
            ApplyColorOverrides(preset, spec.Overrides);
            EditorUtility.SetDirty(preset);
            updated.Add($"{spec.CharacterId}: {spec.DisplayName}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[P09DetailPreservingPaletteTool] Applied {updated.Count} P09 palettes.\n{string.Join("\n", updated)}");
        return updated;
    }

    public static IReadOnlyList<string> ExportFullBodyCrops()
    {
        Directory.CreateDirectory(OutputFolder);
        var paths = new List<string>();
        foreach (var spec in BuildCanonicalSpecs())
        {
            var preset = AssetDatabase.LoadAssetAtPath<BattleP09AppearancePreset>(BuildPresetPath(spec.CharacterId));
            if (preset == null)
            {
                Debug.LogWarning($"[P09DetailPreservingPaletteTool] Missing preset for screenshot: {spec.CharacterId}");
                continue;
            }

            var path = Path.Combine(OutputFolder, $"{spec.CharacterId}.png").Replace('\\', '/');
            RenderPresetCrop(preset, spec, path);
            paths.Add(path);
        }

        var contactSheet = Path.Combine(OutputFolder, "p09_fullbody_contact_sheet.png").Replace('\\', '/');
        if (TryWriteContactSheet(paths, contactSheet))
        {
            paths.Add(contactSheet);
        }

        Debug.Log($"[P09DetailPreservingPaletteTool] Exported {paths.Count} full-body crop image(s) to {OutputFolder}.");
        return paths;
    }

    private static BattleP09AppearancePreset EnsurePreset(string characterId)
    {
        var path = BuildPresetPath(characterId);
        var preset = AssetDatabase.LoadAssetAtPath<BattleP09AppearancePreset>(path);
        if (preset != null)
        {
            return preset;
        }

        Directory.CreateDirectory(BattleP09AppearancePreset.AssetFolder);
        preset = ScriptableObject.CreateInstance<BattleP09AppearancePreset>();
        AssetDatabase.CreateAsset(preset, path);
        return preset;
    }

    private static string BuildPresetPath(string characterId)
    {
        return $"{BattleP09AppearancePreset.AssetFolder}/p09_appearance_{characterId}.asset";
    }

    private static void ApplyPartIds(BattleP09AppearancePreset preset, PaletteSpec spec)
    {
        preset.SetContentId(BattleP09AppearancePartType.Weapon, spec.WeaponId);
        preset.SetContentId(BattleP09AppearancePartType.Shield, spec.ShieldId);
        preset.SetContentId(BattleP09AppearancePartType.Head, spec.ArmorId);
        preset.SetContentId(BattleP09AppearancePartType.Chest, spec.ArmorId);
        preset.SetContentId(BattleP09AppearancePartType.Arm, spec.ArmorId);
        preset.SetContentId(BattleP09AppearancePartType.Waist, spec.ArmorId);
        preset.SetContentId(BattleP09AppearancePartType.Leg, spec.ArmorId);
        preset.SetContentId(BattleP09AppearancePartType.Sex, spec.SexId);
        preset.SetContentId(BattleP09AppearancePartType.FaceType, spec.FaceTypeId);
        preset.SetContentId(BattleP09AppearancePartType.HairStyle, spec.HairStyleId);
        preset.SetContentId(BattleP09AppearancePartType.HairColor, spec.HairColorId);
        preset.SetContentId(BattleP09AppearancePartType.Skin, spec.SkinId);
        preset.SetContentId(BattleP09AppearancePartType.EyeColor, spec.EyeColorId);
        preset.SetContentId(BattleP09AppearancePartType.FacialHair, spec.FacialHairId);
        preset.SetContentId(BattleP09AppearancePartType.BustSize, spec.BustSizeId);
    }

    private static void ApplyColorOverrides(BattleP09AppearancePreset preset, IReadOnlyList<ColorOverrideSpec> overrides)
    {
        var serialized = new SerializedObject(preset);
        serialized.Update();
        var property = serialized.FindProperty("materialColorOverrides");
        property.ClearArray();
        for (var i = 0; i < overrides.Count; i++)
        {
            property.InsertArrayElementAtIndex(i);
            var element = property.GetArrayElementAtIndex(i);
            var colorOverride = overrides[i];
            element.FindPropertyRelative("Label").stringValue = colorOverride.Label;
            element.FindPropertyRelative("UsePartTarget").boolValue = true;
            element.FindPropertyRelative("TargetPart").enumValueIndex = (int)colorOverride.TargetPart;
            element.FindPropertyRelative("TargetContains").stringValue = colorOverride.TargetContains;
            element.FindPropertyRelative("Enabled").boolValue = true;
            element.FindPropertyRelative("MainColor").colorValue = ParseColor(colorOverride.Main);
            element.FindPropertyRelative("SecondColor").colorValue = ParseColor(colorOverride.Second);
            element.FindPropertyRelative("ThirdColor").colorValue = ParseColor(colorOverride.Third);
            element.FindPropertyRelative("UseEmissionColor").boolValue = !string.IsNullOrWhiteSpace(colorOverride.Emission);
            element.FindPropertyRelative("EmissionColor").colorValue = string.IsNullOrWhiteSpace(colorOverride.Emission)
                ? Color.black
                : ParseColor(colorOverride.Emission);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RenderPresetCrop(BattleP09AppearancePreset preset, PaletteSpec spec, string relativeOutputPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Missing P09 visual prefab: {VisualPrefabPath}");
        }

        var generatedMaterials = new List<Material>();
        PreviewRenderUtility? previewRenderer = null;
        Texture2D? fullTexture = null;
        Texture2D? cropTexture = null;
        GameObject? instance = null;
        try
        {
            instance = UnityEngine.Object.Instantiate(prefab);
            if (instance == null)
            {
                throw new InvalidOperationException($"Could not instantiate P09 visual prefab: {VisualPrefabPath}");
            }

            instance.name = $"__SM_P09FullBody_{preset.CharacterId}";
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            preset.ApplyTo(instance.transform, generatedMaterials);
            P09PreviewPoseUtility.TryApplyDefaultIdlePose(instance, preset.SexId);
            ApplyPreviewReadableMaterials(instance.transform, generatedMaterials);

            var backgroundColor = ParseColor(spec.Background);
            previewRenderer = new PreviewRenderUtility();
            previewRenderer.cameraFieldOfView = 23f;
            previewRenderer.camera.clearFlags = CameraClearFlags.Color;
            previewRenderer.camera.backgroundColor = backgroundColor;
            previewRenderer.camera.nearClipPlane = 0.05f;
            previewRenderer.camera.farClipPlane = 80f;
            previewRenderer.ambientColor = new Color(0.68f, 0.70f, 0.72f, 1f);
            previewRenderer.lights[0].intensity = 1.45f;
            previewRenderer.lights[0].transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            previewRenderer.lights[1].intensity = 0.75f;
            previewRenderer.lights[1].transform.rotation = Quaternion.Euler(330f, 142f, 0f);
            previewRenderer.AddSingleGO(instance);

            var camera = previewRenderer.camera;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = backgroundColor;
            camera.fieldOfView = 23f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            ConfigureCamera(camera, instance.transform, RenderWidth, RenderHeight);

            previewRenderer.BeginStaticPreview(new Rect(0, 0, RenderWidth, RenderHeight));
            previewRenderer.Render();
            fullTexture = previewRenderer.EndStaticPreview();

            cropTexture = CropTexture(fullTexture, backgroundColor, 34);
            File.WriteAllBytes(relativeOutputPath, cropTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(relativeOutputPath, ImportAssetOptions.ForceSynchronousImport);
        }
        finally
        {
            if (previewRenderer != null)
            {
                previewRenderer.Cleanup();
            }

            if (fullTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(fullTexture);
            }

            if (cropTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(cropTexture);
            }

            foreach (var material in generatedMaterials)
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }

    private static void ConfigureCamera(Camera camera, Transform root, int width, int height)
    {
        var bounds = CalculateRendererBounds(root);
        var target = bounds.center + Vector3.up * (bounds.size.y * 0.03f);
        var aspect = width / (float)height;
        var verticalDistance = bounds.size.y * 0.5f / Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        var horizontalDistance = bounds.size.x * 0.5f / (Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * aspect);
        var distance = Mathf.Max(verticalDistance, horizontalDistance, 1.5f) * 1.16f;
        var direction = Quaternion.Euler(3f, -28f, 0f) * Vector3.forward;
        camera.transform.position = target + direction * distance;
        camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
    }

    private static Bounds CalculateRendererBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(false)
            .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
            .ToList();
        if (renderers.Count == 0)
        {
            return new Bounds(Vector3.up, new Vector3(1f, 2f, 1f));
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Count; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void ApplyPreviewReadableMaterials(Transform modelRoot, ICollection<Material> generatedMaterials)
    {
        var previewShader = Shader.Find(PreviewReadableShaderName)
                            ?? Shader.Find("Sprites/Default")
                            ?? Shader.Find("Unlit/Texture")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (previewShader == null)
        {
            return;
        }

        var materialCache = new Dictionary<Material, Material>();
        foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>(false))
        {
            var materials = renderer.sharedMaterials;
            var changed = false;
            for (var i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null || source.shader == null || !ShouldUsePreviewReadableMaterial(source))
                {
                    continue;
                }

                if (!materialCache.TryGetValue(source, out var previewMaterial))
                {
                    previewMaterial = CreatePreviewReadableMaterial(source, previewShader);
                    materialCache[source] = previewMaterial;
                    generatedMaterials.Add(previewMaterial);
                }

                materials[i] = previewMaterial;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private static bool ShouldUsePreviewReadableMaterial(Material material)
    {
        return material.shader.name.Contains("lilToon", StringComparison.Ordinal)
               || material.shader.name.StartsWith("Hidden/", StringComparison.Ordinal);
    }

    private static Material CreatePreviewReadableMaterial(Material source, Shader previewShader)
    {
        var previewMaterial = new Material(previewShader)
        {
            name = $"{source.name}_P09FullBodyPreview",
            hideFlags = HideFlags.DontSave,
            renderQueue = source.renderQueue
        };

        CopyTexture(source, previewMaterial, "_MainTex", "_MainTex");
        CopyTexture(source, previewMaterial, "_MainTex", "_BaseMap");
        CopyColor(source, previewMaterial, "_Color", "_Color");
        CopyColor(source, previewMaterial, "_Color", "_BaseColor");
        return previewMaterial;
    }

    private static void CopyTexture(Material source, Material target, string sourceProperty, string targetProperty)
    {
        if (!source.HasProperty(sourceProperty) || !target.HasProperty(targetProperty))
        {
            return;
        }

        var texture = source.GetTexture(sourceProperty);
        if (texture == null)
        {
            return;
        }

        target.SetTexture(targetProperty, texture);
        target.SetTextureScale(targetProperty, source.GetTextureScale(sourceProperty));
        target.SetTextureOffset(targetProperty, source.GetTextureOffset(sourceProperty));
    }

    private static void CopyColor(Material source, Material target, string sourceProperty, string targetProperty)
    {
        if (source.HasProperty(sourceProperty) && target.HasProperty(targetProperty))
        {
            target.SetColor(targetProperty, source.GetColor(sourceProperty));
        }
    }

    private static Texture2D CropTexture(Texture2D source, Color background, int margin)
    {
        var minX = source.width;
        var minY = source.height;
        var maxX = 0;
        var maxY = 0;
        var found = false;
        var pixels = source.GetPixels32();
        var background32 = (Color32)background;
        for (var y = 0; y < source.height; y++)
        {
            for (var x = 0; x < source.width; x++)
            {
                var color = pixels[y * source.width + x];
                if (IsBackgroundPixel(color, background32))
                {
                    continue;
                }

                found = true;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (!found)
        {
            return UnityEngine.Object.Instantiate(source);
        }

        minX = Mathf.Max(0, minX - margin);
        minY = Mathf.Max(0, minY - margin);
        maxX = Mathf.Min(source.width - 1, maxX + margin);
        maxY = Mathf.Min(source.height - 1, maxY + margin);

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
        cropped.SetPixels(source.GetPixels(minX, minY, width, height));
        cropped.Apply();
        return cropped;
    }

    private static bool IsBackgroundPixel(Color32 color, Color32 background)
    {
        var dr = color.r - background.r;
        var dg = color.g - background.g;
        var db = color.b - background.b;
        return (dr * dr) + (dg * dg) + (db * db) < 34;
    }

    private static bool TryWriteContactSheet(IReadOnlyList<string> paths, string outputPath)
    {
        var sourceImages = new List<Texture2D>();
        try
        {
            foreach (var path in paths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (texture.LoadImage(File.ReadAllBytes(path)))
                {
                    sourceImages.Add(texture);
                }
            }

            if (sourceImages.Count == 0)
            {
                return false;
            }

            const int cellWidth = 360;
            const int cellHeight = 520;
            const int columns = 3;
            var rows = Mathf.CeilToInt(sourceImages.Count / (float)columns);
            var sheet = new Texture2D(cellWidth * columns, cellHeight * rows, TextureFormat.RGBA32, false);
            Fill(sheet, ContactSheetBackgroundColor);
            for (var i = 0; i < sourceImages.Count; i++)
            {
                BlitFit(sourceImages[i], sheet, (i % columns) * cellWidth, (rows - 1 - (i / columns)) * cellHeight, cellWidth, cellHeight);
            }

            sheet.Apply();
            File.WriteAllBytes(outputPath, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            return true;
        }
        finally
        {
            foreach (var image in sourceImages)
            {
                if (image != null)
                {
                    UnityEngine.Object.DestroyImmediate(image);
                }
            }
        }
    }

    private static void Fill(Texture2D texture, Color color)
    {
        var pixels = Enumerable.Repeat(color, texture.width * texture.height).ToArray();
        texture.SetPixels(pixels);
    }

    private static void BlitFit(Texture2D source, Texture2D target, int x, int y, int width, int height)
    {
        var scale = Mathf.Min(width / (float)source.width, height / (float)source.height) * 0.92f;
        var scaledWidth = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
        var scaledHeight = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
        var offsetX = x + ((width - scaledWidth) / 2);
        var offsetY = y + ((height - scaledHeight) / 2);
        for (var row = 0; row < scaledHeight; row++)
        {
            for (var column = 0; column < scaledWidth; column++)
            {
                var u = column / Mathf.Max(1f, scaledWidth - 1f);
                var v = row / Mathf.Max(1f, scaledHeight - 1f);
                target.SetPixel(offsetX + column, offsetY + row, source.GetPixelBilinear(u, v));
            }
        }
    }

    private static Color ParseColor(string html)
    {
        if (ColorUtility.TryParseHtmlString(html, out var color))
        {
            return color;
        }

        throw new FormatException($"Invalid color: {html}");
    }

    private static IReadOnlyList<PaletteSpec> BuildAllSpecs()
    {
        var canonical = BuildCanonicalSpecs().ToList();
        canonical.Add(BuildAlias("priest", "단린 (丹麟) / Dawn Priest", canonical[0]));
        canonical.Add(BuildAlias("raider", "이빨바람 / Pack Raider", canonical[1]));
        canonical.Add(BuildAlias("hexer", "묵향 (墨香) / Grave Hexer", canonical[2]));
        return canonical;
    }

    private static PaletteSpec BuildAlias(string characterId, string displayName, PaletteSpec source)
    {
        return source with
        {
            CharacterId = characterId,
            DisplayName = displayName
        };
    }

    private static IReadOnlyList<PaletteSpec> BuildCanonicalSpecs()
    {
        return new[]
        {
            new PaletteSpec(
                "hero_dawn_priest",
                "단린 (丹麟) / Dawn Priest",
                2, 2, 4, 6, 1, 1, 0, 2, 7, 3, 4,
                "#6F7884",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#9B643F", "#C9A24E", "#D9856F"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#D8C8A8", "#C9A24E", "#D9856F"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#D8C8A8", "#C9A24E", "#D9856F"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#D8C8A8", "#C9A24E", "#D9856F"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#8E6A45", "#C9A24E", "#D9856F"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#8E6A45", "#C9A24E", "#D9856F"),
                    O("무기", BattleP09AppearancePartType.Weapon, "Weapon", "#C9A24E", "#8E6A45", "#D9856F", "#FFD979"),
                    O("방패", BattleP09AppearancePartType.Shield, "Shield", "#D8C8A8", "#C9A24E", "#D9856F", "#FFD979"),
                }),
            new PaletteSpec(
                "hero_pack_raider",
                "이빨바람 / Pack Raider",
                1, 3, 2, 1, 1, 1, 0, 2, 4, 1, 0,
                "#687078",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#15120F", "#D08A2E", "#C9B58A"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#A96F36", "#D08A2E", "#C9B58A"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#A96F36", "#D08A2E", "#C9B58A"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#A96F36", "#D08A2E", "#C9B58A"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#6B472D", "#D08A2E", "#C9B58A"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#6B472D", "#D08A2E", "#C9B58A"),
                    O("무기", BattleP09AppearancePartType.Weapon, "Weapon", "#C9B58A", "#D08A2E", "#5A3A25"),
                }),
            new PaletteSpec(
                "hero_grave_hexer",
                "묵향 (墨香) / Grave Hexer",
                2, 2, 8, 5, 1, 1, 0, 2, 9, 7, 0,
                "#66635F",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#D8D4C8", "#8E8980", "#6DBE7C"),
                    O("무기 결정", BattleP09AppearancePartType.Weapon, "Crystal", "#6DBE7C", "#6DBE7C", "#B8B0A3", "#6DBE7C"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#B8B0A3", "#8E8980", "#6DBE7C"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#B8B0A3", "#8E8980", "#6DBE7C"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#B8B0A3", "#8E8980", "#6DBE7C"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#5F5A54", "#8E8980", "#B8B0A3"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#5F5A54", "#8E8980", "#B8B0A3"),
                    O("무기", BattleP09AppearancePartType.Weapon, "Weapon", "#8E8980", "#6DBE7C", "#B8B0A3", "#6DBE7C"),
                }),
            new PaletteSpec(
                "hero_echo_savant",
                "공한 (空閑) / Echo Savant",
                1, 2, 13, 8, 1, 1, 0, 2, 11, 13, 0,
                "#7A706A",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#92879F", "#C8C1D8", "#8FD4E8"),
                    O("무기 프리즘", BattleP09AppearancePartType.Weapon, "Prism", "#8FD4E8", "#C8C1D8", "#4C405E", "#8FD4E8"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#8C7FA0", "#C8C1D8", "#8FD4E8"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#8C7FA0", "#C8C1D8", "#8FD4E8"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#8C7FA0", "#C8C1D8", "#8FD4E8"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#5A4C6B", "#C8C1D8", "#8C7FA0"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#5A4C6B", "#C8C1D8", "#8C7FA0"),
                    O("무기", BattleP09AppearancePartType.Weapon, "Weapon", "#C8C1D8", "#8FD4E8", "#4C405E", "#8FD4E8"),
                }),
            new PaletteSpec(
                "npc_lyra_sternfeld",
                "선영 (宣英) / Lyra Sternfeld",
                2, 2, 7, 3, 1, 1, 0, 2, 7, 9, 0,
                "#77716A",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#D6CBAE", "#8D74C9", "#B8B9C2"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#D8D2C2", "#8D74C9", "#B8B9C2"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#D8D2C2", "#8D74C9", "#B8B9C2"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#D8D2C2", "#8D74C9", "#B8B9C2"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#6E6480", "#8D74C9", "#B8B9C2"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#6E6480", "#8D74C9", "#B8B9C2"),
                    O("무기", BattleP09AppearancePartType.Weapon, "Weapon", "#B8B9C2", "#8D74C9", "#D8D2C2", "#9A7DFF"),
                    O("방패", BattleP09AppearancePartType.Shield, "Shield", "#D8D2C2", "#8D74C9", "#B8B9C2", "#9A7DFF"),
                }),
            new PaletteSpec(
                "npc_grey_fang",
                "회조 (灰爪) / Grey Fang",
                1, 1, 2, 5, 1, 1, 5, 2, 4, 1, 0,
                "#5E6970",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#B0B3B0", "#C1B59C", "#8F9698"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#8B7A68", "#C1B59C", "#8F9698"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#8B7A68", "#C1B59C", "#8F9698"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#8B7A68", "#C1B59C", "#8F9698"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#5C5248", "#C1B59C", "#8B7A68"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#5C5248", "#C1B59C", "#8B7A68"),
                    O("무기", BattleP09AppearancePartType.Weapon, "Weapon", "#8F9698", "#C1B59C", "#5C5248"),
                }),
            new PaletteSpec(
                "npc_black_vellum",
                "흑지 (黑紙) / Black Vellum",
                2, 2, 9, 1, 1, 1, 0, 2, 9, 7, 0,
                "#6A6864",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#151517", "#202022", "#2E5A4F"),
                    O("무기 결정", BattleP09AppearancePartType.Weapon, "Crystal", "#2E5A4F", "#202022", "#AFA79A", "#2E5A4F"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#AFA79A", "#202022", "#2E5A4F"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#AFA79A", "#202022", "#2E5A4F"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#AFA79A", "#202022", "#2E5A4F"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#514A43", "#202022", "#AFA79A"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#514A43", "#202022", "#AFA79A"),
                    O("무기", BattleP09AppearancePartType.Weapon, "Weapon", "#202022", "#2E5A4F", "#AFA79A", "#2E5A4F"),
                }),
            new PaletteSpec(
                "npc_silent_moon",
                "침월 (沉月) / Silent Moon",
                2, 2, 9, 8, 1, 1, 0, 2, 8, 7, 0,
                "#746A64",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#C7C0D8", "#D6D2E3", "#8EA0FF"),
                    O("무기 결정", BattleP09AppearancePartType.Weapon, "Crystal", "#8EA0FF", "#D6D2E3", "#3F304D", "#8EA0FF"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#AFA7B8", "#D6D2E3", "#8EA0FF"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#AFA7B8", "#D6D2E3", "#8EA0FF"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#AFA7B8", "#D6D2E3", "#8EA0FF"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#503D61", "#D6D2E3", "#AFA7B8"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#503D61", "#D6D2E3", "#AFA7B8"),
                    O("무기", BattleP09AppearancePartType.Weapon, "Weapon", "#D6D2E3", "#8EA0FF", "#3F304D", "#8EA0FF"),
                }),
            new PaletteSpec(
                "npc_baekgyu_sternheim",
                "백규 (白圭) / Baekgyu Sternheim",
                1, 1, 3, 5, 1, 1, 6, 2, 12, 5, 0,
                "#5B6470",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#D8D4C8", "#D8CFB8", "#7D5CC7"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#7E8490", "#D8CFB8", "#7D5CC7"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#7E8490", "#D8CFB8", "#7D5CC7"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#7E8490", "#D8CFB8", "#7D5CC7"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#4C5664", "#D8CFB8", "#7D5CC7"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#4C5664", "#D8CFB8", "#7D5CC7"),
                    O("무기", BattleP09AppearancePartType.Weapon, "Weapon", "#D8CFB8", "#7D5CC7", "#3E4652", "#7D5CC7"),
                }),
        };
    }

    private static ColorOverrideSpec O(
        string label,
        BattleP09AppearancePartType targetPart,
        string targetContains,
        string main,
        string second,
        string third,
        string emission = "")
    {
        return new ColorOverrideSpec(label, targetPart, targetContains, main, second, third, emission);
    }

    private sealed record PaletteSpec(
        string CharacterId,
        string DisplayName,
        int SexId,
        int FaceTypeId,
        int HairStyleId,
        int HairColorId,
        int SkinId,
        int EyeColorId,
        int FacialHairId,
        int BustSizeId,
        int ArmorId,
        int WeaponId,
        int ShieldId,
        string Background,
        IReadOnlyList<ColorOverrideSpec> Overrides);

    private sealed record ColorOverrideSpec(
        string Label,
        BattleP09AppearancePartType TargetPart,
        string TargetContains,
        string Main,
        string Second,
        string Third,
        string Emission = "");
}
