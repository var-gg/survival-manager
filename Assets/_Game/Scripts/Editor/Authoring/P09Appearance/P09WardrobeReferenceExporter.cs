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
    private const string AnchorRoot = "art-pipeline/ref/characters";
    private const int RenderWidth = 700;
    private const int RenderHeight = 1200;

    /// <summary>머리 시트의 고정 색. 9색 중 유일하게 나이 든 사람으로 읽히는 색이라 기준으로 쓴다.</summary>
    private const int IvoryHairColorId = 5;

    private static readonly Color Background = new(0.36f, 0.38f, 0.44f, 1f);

    [MenuItem("SM/Internal/P09/Export Wardrobe Reference Sheet")]
    public static void ExportWardrobeMenu()
    {
        var written = ExportWardrobe();
        Debug.Log($"[P09WardrobeReferenceExporter] {written} crop(s) written to {OutputFolder}.");
        EditorUtility.RevealInFinder(Path.GetFullPath(OutputFolder));
    }

    /// <summary>
    /// 저작된 P09 프리셋을 art-pipeline 앵커로 렌더한다. 2D 초상 생성은 이 앵커를 정체성 원본으로
    /// 삼으므로(의상 슬롯·실루엣·색 구역), 앵커 없이 생성하면 기존 캐릭터들과 화풍·의상이 어긋난다.
    /// 앵커가 이미 있는 캐릭터는 건너뛴다 — 재생성은 기존 2D와의 연속성을 깬다.
    /// </summary>
    [MenuItem("SM/Internal/P09/Export Missing Character Anchors")]
    public static void ExportMissingAnchorsMenu()
    {
        var written = ExportMissingAnchors();
        Debug.Log($"[P09WardrobeReferenceExporter] {written} anchor(s) written under {AnchorRoot}.");
    }

    /// <summary>자동화 진입점 — 새로 쓴 앵커 개수를 돌려준다.</summary>
    public static int ExportMissingAnchors()
    {
        var presets = Resources.LoadAll<BattleP09AppearancePreset>(BattleP09AppearancePreset.ResourcesFolder);
        var written = 0;
        foreach (var preset in presets)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.CharacterId))
            {
                continue;
            }

            var dir = Path.Combine(AnchorRoot, preset.CharacterId).Replace('\\', '/');
            var path = Path.Combine(dir, "anchor.png").Replace('\\', '/');
            if (File.Exists(path))
            {
                continue;
            }

            Directory.CreateDirectory(dir);
            if (RenderOne(preset, path, absolutePath: true))
            {
                written++;
                Debug.Log($"[P09WardrobeReferenceExporter] anchor -> {path}");
            }
        }

        return written;
    }

    /// <summary>
    /// 머리 모양과 머리 색을 한 장씩 렌더한다. 옷 계열과 같은 이유로 필요하지만, 이쪽은 함정이 하나 더 있다 —
    /// <see cref="BattleP09AppearancePreset"/>의 머리 색 적용은 <c>Hair_{0:D2}</c> 이름이 <b>정확히</b> 맞는
    /// 메시에만 머티리얼을 바꿔 끼운다. 이름이 어긋나는 모양은 <b>조용히</b> 임포트 기본 머티리얼로 남는다.
    /// 실제로 그렇게 나왔다: Ivory 로 저작한 두 캐릭터가 어두운 올리브 머리로 렌더됐고, 텍스처 자체는
    /// 옅은 회백색이었다. 이 시트는 "어떤 모양인가"와 "이 모양이 머리 색을 먹는가"를 같이 답한다.
    ///
    /// 판독법: 색 시트에서 옅게 나오면 그 색이 도달한 것이고, 어두운 올리브로 나오면 도달하지 않은 것이다.
    /// </summary>
    [MenuItem("SM/Internal/P09/Export Hair Reference Sheet")]
    public static void ExportHairMenu()
    {
        var written = ExportHair();
        Debug.Log($"[P09WardrobeReferenceExporter] {written} hair crop(s) written to {OutputFolder}.");
        EditorUtility.RevealInFinder(Path.GetFullPath(OutputFolder));
    }

    /// <summary>자동화 진입점 — 렌더한 파일 개수를 돌려준다.</summary>
    public static int ExportHair()
    {
        var catalog = Resources.Load<BattleP09AppearanceCatalog>(BattleP09AppearanceCatalog.ResourcesPath);
        if (catalog == null)
        {
            throw new InvalidOperationException(
                $"P09 appearance catalog is missing at Resources/{BattleP09AppearanceCatalog.ResourcesPath}.");
        }

        Directory.CreateDirectory(OutputFolder);
        var written = 0;

        foreach (var sexId in new[] { 1, 2 })
        {
            // 머리 모양 — 색은 Ivory 하나로 고정한다. 옅게 나오는 모양만 머리 색을 실제로 먹는다.
            foreach (var style in CollectContentIds(catalog, BattleP09AppearancePartType.HairStyle, sexId))
            {
                var preset = BuildHairPreset(catalog, sexId, $"hair_sex{sexId}_style{style:00}");
                preset.SetContentId(BattleP09AppearancePartType.HairStyle, style);
                preset.SetContentId(BattleP09AppearancePartType.HairColor, IvoryHairColorId);
                written += RenderOne(preset, $"sex{sexId}_hairstyle_{style:00}.png", headOnly: true) ? 1 : 0;
                UnityEngine.Object.DestroyImmediate(preset);
            }

            // 수염 — 남성만 실제 항목이 있다.
            foreach (var beard in CollectContentIds(catalog, BattleP09AppearancePartType.FacialHair, sexId))
            {
                var preset = BuildHairPreset(catalog, sexId, $"beard_sex{sexId}_{beard:00}");
                preset.SetContentId(BattleP09AppearancePartType.FacialHair, beard);
                written += RenderOne(preset, $"sex{sexId}_beard_{beard:00}.png", headOnly: true) ? 1 : 0;
                UnityEngine.Object.DestroyImmediate(preset);
            }

            // 얼굴형 — 3종뿐이지만 나이대 인상이 여기서 갈린다.
            foreach (var face in CollectContentIds(catalog, BattleP09AppearancePartType.FaceType, sexId))
            {
                var preset = BuildHairPreset(catalog, sexId, $"face_sex{sexId}_{face:00}");
                preset.SetContentId(BattleP09AppearancePartType.FaceType, face);
                written += RenderOne(preset, $"sex{sexId}_facetype_{face:00}.png", headOnly: true) ? 1 : 0;
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        AssetDatabase.Refresh();
        return written;
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

    /// <summary>
    /// 머리 시트용 프리셋. 옷은 다리까지 덮는 중립 계열 하나로 고정해서 얼굴과 머리만 눈에 들어오게 한다.
    /// </summary>
    private static BattleP09AppearancePreset BuildHairPreset(
        BattleP09AppearanceCatalog catalog,
        int sexId,
        string id)
    {
        var preset = BuildPreset(catalog, sexId, id);
        preset.SetContentId(BattleP09AppearancePartType.Chest, 3);
        preset.SetContentId(BattleP09AppearancePartType.Arm, 3);
        preset.SetContentId(BattleP09AppearancePartType.Waist, 3);
        preset.SetContentId(BattleP09AppearancePartType.Leg, 3);
        preset.SetContentId(BattleP09AppearancePartType.Head, 0);
        return preset;
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

    private static bool RenderOne(
        BattleP09AppearancePreset preset,
        string fileName,
        bool absolutePath = false,
        bool headOnly = false)
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
            FrameCamera(camera, instance.transform, headOnly);

            renderer.BeginStaticPreview(new Rect(0, 0, RenderWidth, RenderHeight));
            renderer.Render();
            texture = renderer.EndStaticPreview();

            var path = absolutePath
                ? fileName
                : Path.Combine(OutputFolder, fileName).Replace('\\', '/');
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

    /// <summary>
    /// P09 모델의 forward는 +Z다. 카메라를 -Z에 identity로 두면 <b>뒤통수가 찍힌다</b> —
    /// 첫 렌더가 실제로 그렇게 나왔다. 위키 크롭(<see cref="P09DetailPreservingPaletteTool"/>)이 쓰는
    /// 정면 3/4 방향을 그대로 따른다. 이 시트와 앵커는 기존 캐릭터 크롭과 같은 화각이어야
    /// 나중에 2D 생성 결과가 한 가족으로 보인다.
    /// </summary>
    private static void FrameCamera(Camera camera, Transform root, bool headOnly = false)
    {
        var bounds = CalculateBounds(root);
        var target = bounds.center + Vector3.up * (bounds.size.y * 0.03f);
        var aspect = RenderWidth / (float)RenderHeight;
        var half = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        // 머리·수염·얼굴형은 전신 화각에서 몇 픽셀밖에 안 된다. 그 크기로 고르면 계열 썸네일에서
        // 세 번 틀린 판독을 머리에서 반복하게 된다. 머리 시트는 머리만 채운다.
        if (headOnly)
        {
            var headSpan = bounds.size.y * 0.22f;
            target = new Vector3(bounds.center.x, bounds.max.y - headSpan * 0.5f, bounds.center.z);
            var headDistance = Mathf.Max(headSpan / half, headSpan / (half * aspect)) * 1.05f;
            var headDirection = Quaternion.Euler(3f, -28f, 0f) * Vector3.forward;
            camera.transform.position = target + headDirection * headDistance;
            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = headDistance * 4f;
            return;
        }

        var verticalDistance = bounds.size.y * 0.5f / half;
        var horizontalDistance = bounds.size.x * 0.5f / (half * aspect);
        var distance = Mathf.Max(verticalDistance, horizontalDistance, 1.5f) * 1.16f;
        var direction = Quaternion.Euler(3f, -28f, 0f) * Vector3.forward;
        camera.transform.position = target + direction * distance;
        camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
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
