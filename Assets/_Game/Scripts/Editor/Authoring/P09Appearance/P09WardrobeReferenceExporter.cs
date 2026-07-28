#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.P09Appearance
{

/// <summary>
/// P09 카탈로그는 <c>Armor_003_Chest</c> 처럼 순번 이름만 갖는다. 그래서 캐릭터를 저작할 때
/// 번호로는 무엇을 고르는지 알 수 없고, 이 저장소의 규칙("자산 이름을 보고 동작을 추론하지 않는다")을
/// 그대로 어기게 된다. 이 도구는 각 의상 계열과 머리 부위를 한 장씩 렌더해 눈으로 고를 수 있게 한다.
///
/// 3D 풀이 고정이고 명명 로스터가 그 풀 크기로 묶여 있으므로, 이 시트는 한 번 만들면 이후 모든
/// 캐릭터 저작에 계속 재사용된다.
///
/// 라벨 합성은 하지 않는다 — 렌더는 Unity가, 라벨과 대지 조립은 바깥 스크립트가 맡는다.
/// </summary>
public static class P09WardrobeReferenceExporter
{
    private const string VisualPrefabPath =
        "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/Demo_Prefab/P09_Human_Combat_Demo Variant.prefab";
    private const string OutputFolder = "Logs/p09-wardrobe";
    private const int RenderWidth = 700;
    private const int RenderHeight = 1200;

    private static readonly Color Background = new(0.36f, 0.38f, 0.44f, 1f);

    [MenuItem("SM/Internal/P09/Export Wardrobe Reference Sheet")]
    public static void ExportWardrobeMenu()
    {
        var written = ExportWardrobe();
        Debug.Log($"[P09WardrobeReferenceExporter] {written} crop(s) written to {OutputFolder}.");
        EditorUtility.RevealInFinder(Path.GetFullPath(OutputFolder));
    }

    /// <summary>자동화 진입점 — 렌더한 파일 개수를 돌려준다.</summary>
    public static int ExportWardrobe()
    {
        var catalog = Resources.Load<BattleP09AppearanceCatalog>(BattleP09AppearanceCatalog.ResourcesPath);
        if (catalog == null)
        {
            throw new InvalidOperationException(
                $"P09 appearance catalog is missing at Resources/{BattleP09AppearanceCatalog.ResourcesPath}. " +
                "Run SM/Internal/Content/Rebuild P09 Appearance Catalog first.");
        }

        Directory.CreateDirectory(OutputFolder);
        var written = 0;

        foreach (var sexId in new[] { 1, 2 })
        {
            // 의상 한 벌 — chest/arm/waist/leg를 같은 계열 번호로 맞춰 그 계열의 전체 실루엣을 본다.
            foreach (var family in CollectContentIds(catalog, BattleP09AppearancePartType.Chest, sexId))
            {
                var preset = BuildPreset(catalog, sexId, $"wardrobe_sex{sexId}_family{family:00}");
                preset.SetContentId(BattleP09AppearancePartType.Chest, family);
                preset.SetContentId(BattleP09AppearancePartType.Arm, family);
                preset.SetContentId(BattleP09AppearancePartType.Waist, family);
                preset.SetContentId(BattleP09AppearancePartType.Leg, family);
                preset.SetContentId(BattleP09AppearancePartType.Head, 0);
                written += RenderOne(preset, $"sex{sexId}_family_{family:00}.png") ? 1 : 0;
                UnityEngine.Object.DestroyImmediate(preset);
            }

            // 머리 부위 — 몸통은 중립 계열로 고정하고 머리만 바꾼다.
            foreach (var head in CollectContentIds(catalog, BattleP09AppearancePartType.Head, sexId))
            {
                var preset = BuildPreset(catalog, sexId, $"wardrobe_sex{sexId}_head{head:00}");
                preset.SetContentId(BattleP09AppearancePartType.Chest, 3);
                preset.SetContentId(BattleP09AppearancePartType.Arm, 3);
                preset.SetContentId(BattleP09AppearancePartType.Waist, 3);
                preset.SetContentId(BattleP09AppearancePartType.Leg, 3);
                preset.SetContentId(BattleP09AppearancePartType.Head, head);
                written += RenderOne(preset, $"sex{sexId}_head_{head:00}.png") ? 1 : 0;
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        AssetDatabase.Refresh();
        return written;
    }

    private static List<int> CollectContentIds(
        BattleP09AppearanceCatalog catalog,
        BattleP09AppearancePartType type,
        int sexId)
    {
        var ids = new List<int>();
        foreach (var option in catalog.GetOptions(type, sexId))
        {
            if (!ids.Contains(option.ContentId))
            {
                ids.Add(option.ContentId);
            }
        }

        return ids;
    }

    private static BattleP09AppearancePreset BuildPreset(
        BattleP09AppearanceCatalog catalog,
        int sexId,
        string id)
    {
        var preset = ScriptableObject.CreateInstance<BattleP09AppearancePreset>();
        preset.ConfigureIdentity(id, id, catalog);
        preset.SetContentId(BattleP09AppearancePartType.Sex, sexId);
        preset.SetContentId(BattleP09AppearancePartType.FaceType, 1);
        preset.SetContentId(BattleP09AppearancePartType.HairStyle, sexId == 1 ? 2 : 4);
        preset.SetContentId(BattleP09AppearancePartType.HairColor, 3);
        preset.SetContentId(BattleP09AppearancePartType.Skin, 1);
        preset.SetContentId(BattleP09AppearancePartType.EyeColor, 2);
        preset.SetContentId(BattleP09AppearancePartType.FacialHair, 0);
        preset.SetContentId(BattleP09AppearancePartType.BustSize, 2);
        preset.SetContentId(BattleP09AppearancePartType.Weapon, 0);
        preset.SetContentId(BattleP09AppearancePartType.Shield, 0);
        return preset;
    }

    private static bool RenderOne(BattleP09AppearancePreset preset, string fileName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Missing P09 visual prefab: {VisualPrefabPath}");
        }

        var generatedMaterials = new List<Material>();
        PreviewRenderUtility? renderer = null;
        GameObject? instance = null;
        Texture2D? texture = null;
        try
        {
            instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = $"__SM_P09Wardrobe_{preset.CharacterId}";
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            preset.ApplyTo(instance.transform, generatedMaterials);
            P09PreviewPoseUtility.TryApplyDefaultIdlePose(instance, preset.SexId);

            // P09 머티리얼은 프리뷰 컨텍스트에서 셰이더가 안 잡혀 마젠타로 렌더된다. 이 변환을 빠뜨리면
            // 시트가 실루엣만 남아 고르는 근거가 되지 못한다(첫 시도에서 실제로 그렇게 나왔다).
            var profile = CharacterShowcasePreviewApplier.LoadDefault();
            if (CharacterShowcasePreviewApplier.ShouldUseReadableMaterials(profile))
            {
                P09DetailPreservingPaletteTool.ApplyPreviewReadableMaterials(instance.transform, generatedMaterials);
            }

            renderer = new PreviewRenderUtility();
            // Studio window / wiki crop과 같은 조명 정책을 쓴다 — 옷장 시트가 실제 저작 화면과
            // 다르게 보이면 고르는 근거가 되지 못한다.
            if (profile != null)
            {
                CharacterShowcasePreviewApplier.ApplyTo(renderer, profile);
            }

            renderer.AddSingleGO(instance);
            var camera = renderer.camera;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = Background;
            camera.allowMSAA = true;
            FrameCamera(camera, instance.transform);

            renderer.BeginStaticPreview(new Rect(0, 0, RenderWidth, RenderHeight));
            renderer.Render();
            texture = renderer.EndStaticPreview();

            var path = Path.Combine(OutputFolder, fileName).Replace('\\', '/');
            File.WriteAllBytes(path, texture.EncodeToPNG());
            return true;
        }
        finally
        {
            renderer?.Cleanup();
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            foreach (var material in generatedMaterials)
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }

    private static void FrameCamera(Camera camera, Transform root)
    {
        var bounds = CalculateBounds(root);
        var height = Mathf.Max(bounds.size.y, 0.1f);
        var distance = height / (2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)) * 1.18f;
        camera.transform.position = bounds.center + new Vector3(0f, 0f, -distance);
        camera.transform.rotation = Quaternion.identity;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = distance * 4f;
    }

    private static Bounds CalculateBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: false);
        if (renderers.Length == 0)
        {
            return new Bounds(root.position, Vector3.one);
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }
}

}
