using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Unity;
using SM.Unity.P09Appearance;
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
    private const bool UseCompatibilityPreviewShader = true;
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
        preset.SetContentId(BattleP09AppearancePartType.Head, spec.HeadId);
        preset.SetContentId(BattleP09AppearancePartType.Chest, spec.ChestId);
        preset.SetContentId(BattleP09AppearancePartType.Arm, spec.ArmId);
        preset.SetContentId(BattleP09AppearancePartType.Waist, spec.WaistId);
        preset.SetContentId(BattleP09AppearancePartType.Leg, spec.LegId);
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

            // 단일 진실원: CharacterShowcaseProfile. Studio window preview와 동일 profile을 사용해
            // wiki 캡쳐 이미지가 사용자가 Studio에서 보는 것과 강제로 일치하게 한다.
            var profile = CharacterShowcasePreviewApplier.LoadDefault();
            if (CharacterShowcasePreviewApplier.ShouldUseReadableMaterials(profile))
            {
                ApplyPreviewReadableMaterials(instance.transform, generatedMaterials);
            }

            var backgroundColor = ParseColor(spec.Background);
            previewRenderer = new PreviewRenderUtility();
            if (profile != null)
            {
                CharacterShowcasePreviewApplier.ApplyTo(previewRenderer, profile);
            }
            else
            {
                // Profile 자산이 없을 때만 legacy fallback.
                previewRenderer.cameraFieldOfView = 23f;
                previewRenderer.camera.clearFlags = CameraClearFlags.Color;
                previewRenderer.camera.nearClipPlane = 0.05f;
                previewRenderer.camera.farClipPlane = 80f;
                previewRenderer.ambientColor = new Color(0.68f, 0.70f, 0.72f, 1f);
                previewRenderer.lights[0].intensity = 1.45f;
                previewRenderer.lights[0].transform.rotation = Quaternion.Euler(38f, -32f, 0f);
                previewRenderer.lights[1].intensity = 0.75f;
                previewRenderer.lights[1].transform.rotation = Quaternion.Euler(330f, 142f, 0f);
            }
            // Per-character background는 spec이 override (wiki crop은 인물별 배경 달라야 함).
            previewRenderer.camera.backgroundColor = backgroundColor;
            previewRenderer.AddSingleGO(instance);

            var camera = previewRenderer.camera;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = backgroundColor;
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
        CopyTexture(source, previewMaterial, "_BaseMap", "_MainTex");
        CopyTexture(source, previewMaterial, "_BaseColorMap", "_MainTex");
        CopyColor(source, previewMaterial, "_BaseColor", "_Color");
        CopyColor(source, previewMaterial, "_Color", "_Color");
        CopyColor(source, previewMaterial, "_Color2nd", "_Color2nd");
        CopyColor(source, previewMaterial, "_Color3rd", "_Color3rd");
        SetFloatIfPresent(previewMaterial, "_PreviewTintStrength", CalculatePreviewTintStrength(source));
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

    private static void SetFloatIfPresent(Material target, string targetProperty, float value)
    {
        if (target.HasProperty(targetProperty))
        {
            target.SetFloat(targetProperty, value);
        }
    }

    private static float CalculatePreviewTintStrength(Material source)
    {
        var main = ReadColor(source, "_Color", Color.white);
        var baseColor = ReadColor(source, "_BaseColor", main);
        var second = ReadColor(source, "_Color2nd", main);
        var third = ReadColor(source, "_Color3rd", main);
        var distance = Mathf.Max(
            ColorDistance(main, Color.white),
            ColorDistance(baseColor, Color.white),
            ColorDistance(second, main),
            ColorDistance(third, main));

        return distance < 0.08f ? 0f : Mathf.Clamp(0.12f + distance * 0.16f, 0.14f, 0.30f);
    }

    private static Color ReadColor(Material source, string propertyName, Color fallback)
    {
        return source.HasProperty(propertyName) ? source.GetColor(propertyName) : fallback;
    }

    private static float ColorDistance(Color first, Color second)
    {
        var red = first.r - second.r;
        var green = first.g - second.g;
        var blue = first.b - second.b;
        return Mathf.Sqrt((red * red + green * green + blue * blue) / 3f);
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
                2, 2, 4, 6, 1, 1, 0, 2,
                7, 7, 5, 7, 4, 3, 4,
                "#6F7884",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#9B643F", "#6D3F24", "#C58A5C"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#D8C8A8", "#C9A24E", "#D9856F"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#D8C8A8", "#C9A24E", "#7A6688"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#CFC7B8", "#8E7F72", "#C9A24E"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#8E6A45", "#5B4430", "#C9A24E"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#7A5635", "#4D3828", "#D8C8A8"),
                    O("무기", BattleP09AppearancePartType.Weapon, "Weapon", "#C9A24E", "#E8D49A", "#8E6A45"),
                    O("방패", BattleP09AppearancePartType.Shield, "Shield", "#D8C8A8", "#C9A24E", "#7A6688"),
                }),
            new PaletteSpec(
                "hero_pack_raider",
                "이빨바람 / Pack Raider",
                1, 3, 2, 1, 1, 1, 0, 2,
                0, 4, 10, 10, 4, 1, 0,
                "#687078",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#15120F", "#050403", "#3A2B22"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#A96F36", "#D08A2E", "#C9B58A"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#8B5A2E", "#2F4A2F", "#D08A2E"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#5A3A25", "#D08A2E", "#2F4A2F"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#6B472D", "#3F2A1E", "#A96F36"),
                    O("사냥칼", BattleP09AppearancePartType.Weapon, "Weapon", "#73543A", "#C9B58A", "#2D2016"),
                }),
            new PaletteSpec(
                "hero_grave_hexer",
                "묵향 (墨香) / Grave Hexer",
                2, 2, 8, 5, 1, 1, 0, 1,
                9, 9, 9, 8, 9, 7, 0,
                "#66635F",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#C8C5BB", "#A89F8B", "#E2DED6"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#B8B0A3", "#6DBE7C", "#C4A458"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#B8B0A3", "#8E8980", "#6DBE7C"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#B8B0A3", "#7FA88C", "#C4A458"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#6F647A", "#4C405E", "#6DBE7C"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#5F5A54", "#3F3B36", "#B8B0A3"),
                    O("지팡이", BattleP09AppearancePartType.Weapon, "Weapon", "#4F5856", "#6DBE7C", "#B8B0A3", "#234A31"),
                }),
            new PaletteSpec(
                "hero_echo_savant",
                "공한 (空閑) / Echo Savant",
                1, 2, 13, 8, 1, 4, 0, 2,
                11, 11, 11, 8, 11, 13, 0,
                "#7A706A",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#6F647A", "#2A2430", "#C8C1D8"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#8C7FA0", "#4C405E", "#8FD4E8"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#8C7FA0", "#C8C1D8", "#8FD4E8"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#7B6E90", "#4C405E", "#C8C1D8"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#D6D2E3", "#8C7FA0", "#8FD4E8"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#4C405E", "#2E2638", "#8C7FA0"),
                    O("의례 활", BattleP09AppearancePartType.Weapon, "Weapon", "#4D435A", "#8FD4E8", "#C8C1D8", "#2A5360"),
                }),
            new PaletteSpec(
                "npc_lyra_sternfeld",
                "선영 (宣英) / Lyra Sternfeld",
                2, 2, 7, 3, 1, 3, 0, 2,
                9, 7, 8, 7, 7, 9, 0,
                "#77716A",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#D6CBAE", "#BFAE82", "#F0E6C8"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#D8D2C2", "#8D74C9", "#B8B9C2"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#D8D2C2", "#8D74C9", "#D6CBAE"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#C8C0D2", "#6E6480", "#8D74C9"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#6E6480", "#3C2D48", "#D6CBAE"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#5C5248", "#3B322D", "#8D74C9"),
                    O("의식 지팡이", BattleP09AppearancePartType.Weapon, "Weapon", "#D8D2C2", "#8D74C9", "#D4AF37", "#32205A"),
                }),
            new PaletteSpec(
                "npc_grey_fang",
                "회조 (灰爪) / Grey Fang",
                1, 1, 2, 5, 1, 1, 5, 2,
                0, 4, 11, 4, 3, 1, 0,
                "#5E6970",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#B0B3B0", "#817A74", "#D4D0CA"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#8B7A68", "#5C5248", "#C1B59C"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#6A625F", "#3E3A36", "#8F9698"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#5C5248", "#3A2A1F", "#8B7A68"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#6F665C", "#4B3A2B", "#8F9698"),
                    O("낡은 사냥칼", BattleP09AppearancePartType.Weapon, "Weapon", "#5B5A58", "#6A4A35", "#C1B59C"),
                }),
            new PaletteSpec(
                "npc_black_vellum",
                "흑지 (黑紙) / Black Vellum",
                2, 2, 9, 1, 1, 1, 0, 2,
                9, 9, 11, 9, 8, 7, 0,
                "#6A6864",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#151517", "#050508", "#34343A"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#AFA79A", "#202022", "#2E5A4F"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#AFA79A", "#202022", "#5A5148"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#4A4648", "#202022", "#2E5A4F"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#3E3A36", "#151517", "#AFA79A"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#56505C", "#2A2434", "#2E5A4F"),
                    O("단죄 지팡이", BattleP09AppearancePartType.Weapon, "Weapon", "#3B3745", "#2E5A4F", "#AFA79A", "#123028"),
                }),
            new PaletteSpec(
                "npc_silent_moon",
                "침월 (沉月) / Silent Moon",
                2, 2, 9, 8, 1, 3, 0, 1,
                8, 8, 8, 9, 8, 7, 0,
                "#746A64",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#C7C0D8", "#6F5D88", "#F0EAF8"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#AFA7B8", "#3F304D", "#8EA0FF"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#AFA7B8", "#3F304D", "#D6D2E3"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#AFA7B8", "#4A3A5C", "#8EA0FF"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#D6D2E3", "#8A8198", "#3F304D"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#3F304D", "#241B2D", "#AFA7B8"),
                    O("침묵 지팡이", BattleP09AppearancePartType.Weapon, "Weapon", "#4D435A", "#8EA0FF", "#D6D2E3", "#26365A"),
                }),
            new PaletteSpec(
                "npc_baekgyu_sternheim",
                "백규 (白圭) / Baekgyu Sternheim",
                1, 1, 3, 5, 1, 1, 6, 2,
                12, 12, 5, 12, 12, 5, 0,
                "#5B6470",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#D8D4C8", "#AFA89A", "#F2EEE0"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#7E8490", "#D8CFB8", "#7D5CC7"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#7E8490", "#D8CFB8", "#7D5CC7"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#6F737A", "#3E4652", "#D8CFB8"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#3E4652", "#242A31", "#7D5CC7"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#5C6370", "#353B45", "#D8CFB8"),
                    O("귀족 검", BattleP09AppearancePartType.Weapon, "Weapon", "#424047", "#D8CFB8", "#7D5CC7", "#30204A"),
                }),
            new PaletteSpec(
                "warden",
                "철위 (鐵衛) / Iron Warden",
                1, 2, 5, 5, 1, 2, 4, 2,
                6, 6, 6, 5, 6, 3, 5,
                "#5E6670",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#C0C0B8", "#8E8A82", "#E5E0D6"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#9AA0A6", "#3F5F8F", "#D8CFB8"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#9AA0A6", "#3F5F8F", "#B7B0A0"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#8D949C", "#5B626A", "#D8CFB8"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#4A5160", "#2E3440", "#8A5A38"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#7A828C", "#4A5160", "#D8CFB8"),
                    O("정규군 검", BattleP09AppearancePartType.Weapon, "Weapon", "#B0B4B8", "#6D6258", "#D8CFB8"),
                    O("왕청 방패", BattleP09AppearancePartType.Shield, "Shield", "#4A6FA8", "#D8CFB8", "#8A5A38"),
                }),
            new PaletteSpec(
                "guardian",
                "묘직 (墓直) / Crypt Guardian",
                2, 2, 6, 5, 1, 2, 0, 2,
                9, 5, 6, 9, 5, 2, 3,
                "#59635F",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#D2CEC4", "#A9A092", "#EEE9DE"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#B8B0A3", "#7A8FA8", "#C4A458"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#8D969A", "#5E6F80", "#B8B0A3"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#8A9096", "#5B636B", "#C4A458"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#B8B0A3", "#7A8FA8", "#C4A458"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#6B7075", "#3F454A", "#B8B0A3"),
                    O("묘역 검", BattleP09AppearancePartType.Weapon, "Weapon", "#7A7770", "#B8B0A3", "#C4A458"),
                    O("석관 방패", BattleP09AppearancePartType.Shield, "Shield", "#8D969A", "#5E6F80", "#C4A458"),
                }),
            new PaletteSpec(
                "bulwark",
                "송곳벽 / Fang Bulwark",
                1, 1, 2, 6, 1, 1, 2, 2,
                4, 4, 6, 4, 4, 1, 2,
                "#686158",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#A65A2E", "#5A2E1D", "#D08A2E"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#B06A3C", "#D08A2E", "#C9B58A"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#9A5B34", "#D08A2E", "#2F4A2F"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#7E7770", "#5A3A25", "#D08A2E"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#5A3A25", "#3A2618", "#D08A2E"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#6B472D", "#3F2A1E", "#9A5B34"),
                    O("부족 검", BattleP09AppearancePartType.Weapon, "Weapon", "#73543A", "#C9B58A", "#2D2016"),
                    O("나무 방패", BattleP09AppearancePartType.Shield, "Shield", "#D08A2E", "#5A3A25", "#2F4A2F"),
                }),
            new PaletteSpec(
                "slayer",
                "서검 (誓劍) / Oath Slayer",
                2, 2, 6, 1, 1, 2, 0, 1,
                0, 5, 12, 5, 5, 4, 0,
                "#6D6E74",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#1A1718", "#050508", "#3A3336"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#8A8E92", "#2A2A2E", "#D8CFB8"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#5F6570", "#D8CFB8", "#8D74C9"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#3E4652", "#202022", "#D8CFB8"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#5C6370", "#2A2A2E", "#8A8E92"),
                    O("맹세검", BattleP09AppearancePartType.Weapon, "Weapon", "#B8B8BC", "#D8CFB8", "#2A2A2E"),
                }),
            new PaletteSpec(
                "reaver",
                "묵괴 (墨壞) / Grave Reaver",
                2, 3, 8, 5, 1, 2, 0, 1,
                11, 11, 4, 11, 5, 5, 0,
                "#5F5B57",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#CFCAC0", "#A89F8B", "#E7E2D8"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#4A4648", "#AFA79A", "#8E4F36"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#4A4648", "#AFA79A", "#8E4F36"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#6B5544", "#3A2A1F", "#8E4F36"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#3E3A36", "#202022", "#AFA79A"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#5C5A58", "#2F3035", "#AFA79A"),
                    O("회수검", BattleP09AppearancePartType.Weapon, "Weapon", "#3B3745", "#8E4F36", "#AFA79A"),
                }),
            new PaletteSpec(
                "hunter",
                "원시 (遠矢) / Longshot Hunter",
                1, 2, 12, 1, 1, 2, 1, 2,
                3, 3, 5, 3, 3, 10, 0,
                "#657078",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#5A3D2B", "#2A1F1A", "#8A5A38"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#C0A078", "#3F5F8F", "#D8CFB8"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#C0A078", "#3F5F8F", "#D8CFB8"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#8A8E92", "#4A5160", "#D8CFB8"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#3F5F8F", "#2E3440", "#C0A078"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#6A5A45", "#3E342A", "#3F5F8F"),
                    O("장궁", BattleP09AppearancePartType.Weapon, "Weapon", "#8A6A3F", "#3F5F8F", "#D8CFB8"),
                }),
            new PaletteSpec(
                "scout",
                "숲살이 / Trail Scout",
                2, 1, 12, 4, 1, 1, 0, 2,
                10, 10, 3, 10, 4, 11, 0,
                "#5F6C5B",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#4F6B35", "#2E3F24", "#8A9A5B"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#6F8A3A", "#D08A2E", "#C9B58A"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#6F8A3A", "#D08A2E", "#3F5F32"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#A88A5F", "#5A3A25", "#D08A2E"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#5A3A25", "#2F4A2F", "#D08A2E"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#6B472D", "#3F2A1E", "#6F8A3A"),
                    O("숲 활", BattleP09AppearancePartType.Weapon, "Weapon", "#73543A", "#D08A2E", "#3F5F32"),
                }),
            new PaletteSpec(
                "marksman",
                "냉시 (冷矢) / Dread Marksman",
                1, 2, 5, 5, 1, 4, 0, 2,
                5, 5, 5, 9, 5, 12, 0,
                "#596066",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#D0D6D8", "#9EA8AA", "#EEF4F4"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#7F8E94", "#4F6A76", "#76D7E0"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#7F8E94", "#4F6A76", "#76D7E0"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#6F7E84", "#3F5058", "#B8B0A3"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#B8B0A3", "#4F6A76", "#76D7E0"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#5C666A", "#2F3A40", "#7F8E94"),
                    O("냉궁", BattleP09AppearancePartType.Weapon, "Weapon", "#3E4652", "#76D7E0", "#B8B0A3", "#1B4A50"),
                }),
            new PaletteSpec(
                "shaman",
                "풍의 (風儀) / Storm Shaman",
                2, 2, 10, 1, 1, 1, 0, 3,
                10, 10, 10, 7, 10, 8, 0,
                "#6A6458",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#2E2119", "#1A120E", "#8A7F70"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#8A4F2E", "#C98D47", "#2F4A2F"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#8A4F2E", "#C98D47", "#2F4A2F"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#7A4A32", "#C98D47", "#8A7F70"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#6B4A3A", "#D4A458", "#2F4A2F"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#4F3928", "#2E2119", "#8A4F2E"),
                    O("토템 지팡이", BattleP09AppearancePartType.Weapon, "Weapon", "#6A4A35", "#D4A458", "#2F4A2F"),
                }),
            new PaletteSpec(
                "rift_stalker",
                "틈사냥꾼 / Rift Stalker",
                2, 3, 1, 1, 1, 4, 0, 1,
                0, 11, 4, 10, 11, 1, 0,
                "#615F69",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#2A1F1A", "#0F0B08", "#5A3A28"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#5A4A62", "#3F304D", "#8EA0FF"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#7A4A32", "#D08A2E", "#5A3A25"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#5A3A25", "#2F4A2F", "#D08A2E"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#3F304D", "#241B2D", "#5A4A62"),
                    O("틈 단검", BattleP09AppearancePartType.Weapon, "Weapon", "#5B5A58", "#8EA0FF", "#D08A2E"),
                }),
            new PaletteSpec(
                "bastion_penitent",
                "참회벽 / Bastion Penitent",
                2, 1, 5, 9, 1, 2, 0, 2,
                0, 6, 5, 6, 5, 3, 3,
                "#626A72",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#A33A2E", "#5A1F1A", "#D66A4F"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#8A8E92", "#4A5160", "#C9A24E"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#7A828C", "#3E4652", "#D8CFB8"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#5C6370", "#2E3440", "#8A3A2E"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#5C6370", "#2A2A2E", "#C9A24E"),
                    O("옛 정규군 검", BattleP09AppearancePartType.Weapon, "Weapon", "#A8A8AC", "#6D6258", "#C9A24E"),
                    O("가족 방패", BattleP09AppearancePartType.Shield, "Shield", "#8A8E92", "#C9A24E", "#3F5F8F"),
                }),
            new PaletteSpec(
                "pale_executor",
                "백집행 (白執行) / Pale Executor",
                1, 3, 13, 5, 1, 4, 3, 2,
                12, 12, 9, 12, 11, 12, 0,
                "#6A6870",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#D8D4C8", "#AFA89A", "#F2EEE0"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#B8B0C8", "#2F3035", "#2E8A7A"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#B8B0C8", "#2F3035", "#2E8A7A"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#AFA79A", "#3E3A36", "#2E8A7A"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#4A405A", "#2F3035", "#D8CFB8"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#3B3745", "#202022", "#B8B0C8"),
                    O("망명자의 활", BattleP09AppearancePartType.Weapon, "Weapon", "#3B3745", "#2E8A7A", "#B8B0C8", "#124038"),
                }),
            new PaletteSpec(
                "mirror_cantor",
                "명음 (明音) / Mirror Cantor",
                2, 2, 9, 8, 1, 4, 0, 2,
                9, 9, 8, 9, 8, 9, 5,
                "#686370",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#2D1F3F", "#120A20", "#C7C0D8"),
                    O("머리 장비", BattleP09AppearancePartType.Head, "Armor", "#D8D2E3", "#C05BE0", "#8FD4E8"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#D8D2E3", "#C05BE0", "#8FD4E8"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#AFA7B8", "#4A3A5C", "#8FD4E8"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#C7C0D8", "#6F5D88", "#C05BE0"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#4A3A5C", "#2D2438", "#D8D2E3"),
                    O("거울 지팡이", BattleP09AppearancePartType.Weapon, "Weapon", "#D8D2E3", "#C05BE0", "#8FD4E8", "#2A5360"),
                    O("의례 거울", BattleP09AppearancePartType.Shield, "Shield", "#C7C0D8", "#8FD4E8", "#C05BE0", "#2A5360"),
                }),
            new PaletteSpec(
                "hero_aegis_sentinel",
                "방진 (方陣) / Aegis Sentinel",
                1, 1, 5, 5, 1, 2, 1, 2,
                0, 6, 6, 9, 6, 5, 5,
                "#646B76",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#C9C6BC", "#8F8A82", "#E8E2D6"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#9EA5B0", "#5A4B78", "#D8D2E3"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#8D96A0", "#4C405E", "#B8B0C8"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#4B3A32", "#2D2438", "#8FD4E8"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#7E8792", "#3E4652", "#D8D2E3"),
                    O("결사 의례검", BattleP09AppearancePartType.Weapon, "Weapon", "#B8B0C8", "#8FD4E8", "#4C405E", "#26365A"),
                    O("격자 방패", BattleP09AppearancePartType.Shield, "Shield", "#D8D2E3", "#8FD4E8", "#5A4B78", "#26365A"),
                }),
            new PaletteSpec(
                "hero_shardblade",
                "편검 (片劍) / Shardblade",
                2, 1, 3, 8, 1, 4, 0, 2,
                0, 4, 5, 4, 3, 4, 0,
                "#6B6862",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#6F4B88", "#2A1838", "#C8B5E8"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#7B614A", "#2E3440", "#8FD4E8"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#6F737A", "#4C405E", "#D8D2E3"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#2E3440", "#1A1D24", "#8FD4E8"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#5C4A38", "#2D241C", "#6F4B88"),
                    O("재조립 검", BattleP09AppearancePartType.Weapon, "Weapon", "#D8D2E3", "#8FD4E8", "#6F4B88", "#2A5360"),
                }),
            new PaletteSpec(
                "hero_prism_seeker",
                "광로 (光路) / Prism Seeker",
                1, 1, 12, 1, 1, 4, 0, 2,
                0, 3, 11, 8, 3, 13, 0,
                "#686C76",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#2B211D", "#0D0908", "#6B4A38"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#D6D2E3", "#5A4B78", "#8FD4E8"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#6F647A", "#3F304D", "#8FD4E8"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#4C405E", "#2A2430", "#D6D2E3"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#5C6370", "#2E3440", "#8FD4E8"),
                    O("프리즘 활", BattleP09AppearancePartType.Weapon, "Weapon", "#4D435A", "#8FD4E8", "#D6D2E3", "#2A5360"),
                }),
            new PaletteSpec(
                "hero_ember_runner",
                "연주 (燕走) / Ember Runner",
                2, 1, 1, 6, 1, 1, 0, 1,
                0, 3, 4, 4, 3, 10, 0,
                "#66706A",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#B66A34", "#5A2E1D", "#E08A42"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#8A6B3E", "#D08A2E", "#3F5F32"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#8A4F2E", "#5A3A25", "#D08A2E"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#5A3A25", "#2F4A2F", "#D08A2E"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#7A4A32", "#3F2A1E", "#8A6B3E"),
                    O("견습 활", BattleP09AppearancePartType.Weapon, "Weapon", "#8A6A3F", "#D08A2E", "#3F5F32"),
                }),
            new PaletteSpec(
                "hero_iron_pelt",
                "철피 (鐵皮) / Iron Pelt",
                1, 1, 2, 1, 1, 3, 5, 2,
                0, 6, 6, 4, 4, 5, 3,
                "#5E5B54",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#1F1713", "#080504", "#5A3A25"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#5E6266", "#2F3438", "#9A4A2E"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#5A3A25", "#2F4A2F", "#8A4A2E"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#4A2E22", "#1E1712", "#9A4A2E"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#5A3A25", "#2F241C", "#5E6266"),
                    O("강경파 검", BattleP09AppearancePartType.Weapon, "Weapon", "#4A4642", "#9A4A2E", "#C1B59C"),
                    O("탈취 방패", BattleP09AppearancePartType.Shield, "Shield", "#5E6266", "#9A4A2E", "#2F4A2F"),
                }),
            new PaletteSpec(
                "npc_aldric",
                "단현 스턴홀트 (丹玄) / Aldric Sternfeld",
                1, 1, 8, 5, 1, 2, 6, 2,
                0, 3, 3, 3, 3, 0, 0,
                "#706A64",
                new[]
                {
                    O("머리", BattleP09AppearancePartType.HairColor, "Hair", "#D8D4C8", "#AFA89A", "#F2EEE0"),
                    O("상의", BattleP09AppearancePartType.Chest, "Armor", "#5C5A58", "#202022", "#B8A078"),
                    O("팔 장비", BattleP09AppearancePartType.Arm, "Armor", "#4A4648", "#202022", "#B8A078"),
                    O("허리 장비", BattleP09AppearancePartType.Waist, "Armor", "#2A2A2E", "#151517", "#8A3A2E"),
                    O("하의", BattleP09AppearancePartType.Leg, "Armor", "#3F3B36", "#202022", "#B8A078"),
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
        int HeadId,
        int ChestId,
        int ArmId,
        int WaistId,
        int LegId,
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
