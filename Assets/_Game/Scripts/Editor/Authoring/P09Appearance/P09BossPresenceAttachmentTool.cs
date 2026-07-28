#nullable enable
using System.Collections.Generic;
using System.IO;
using SM.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace SM.Editor.Authoring.P09Appearance
{

/// <summary>
/// P09는 인간형 하나만 만들고, 모든 유닛이 같은 키로 정규화된다. 그래서 전투 화면에서
/// 보스가 일반 병사와 실루엣이 같다 — 8명이 붙어 싸우는 걸 <b>보는</b> 게임에서 이건 가독성 결함이다.
///
/// 이 도구는 그 결함을 <b>강체 부착물</b>로 해소한다. P09 리그는 Humanoid(본 77개)라
/// <c>Head</c>/<c>Chest</c>/<c>shoulder_L·R</c>가 이름으로 잡히고, P09 자신이 이미
/// Shield·Staff를 스킨드가 아닌 <b>정적 메시</b>로 소켓에 달고 있다. ProBuilder 산출물이
/// 정확히 그 형태라서 새 메커니즘 없이 기존 패턴을 그대로 탄다.
///
/// 못 하는 것: 변형이 필요한 것(흐르는 망토, 허리에서 접히는 로브, 체격 자체).
/// 그건 스킨 웨이트가 필요하고 ProBuilder는 스킨 웨이트를 저작하지 않는다.
///
/// 이 도구는 <b>판단용 렌더</b>를 만든다. 부착 전/후를 같은 화각으로 나란히 찍어서,
/// "보스로 읽히는가"를 눈으로 결정한 뒤에 파이프라인을 짓기 위한 것이다.
///
/// <para><b>2026-07-29 판정 — 메커니즘은 통과, 조형은 불합격.</b>
/// 네 번 돌려서 확인한 결론이다. 본 부착·재질 상속·캐릭터 공간 저작은 전부 동작하고 재사용 가능하다.
/// 그러나 <b>ProBuilder 프리미티브(상자·원기둥·원뿔)는 캐릭터 부착물의 조형 도구로는 부적합하다</b> —
/// 조각되고 텍스처가 입혀진 툰 셰이딩 캐릭터 옆에서 각진 단색 도형은 위치를 아무리 맞춰도
/// "붙여 놓은 비계"로 읽힌다. 치수를 리그 실측으로 맞춘 뒤에도 그대로였다.
///
/// 따라서 아래 <see cref="BuildAdjudicatorPresence"/>의 형상은 <b>완성물이 아니라 배치 검증용 대역</b>이다.
/// ProBuilder의 제자리는 맵 건축(관문·계단·재판장 단·우리)이다 — 거기서는 프리미티브 형태가
/// 오히려 정답이고 원거리에서 보인다. 보스 실루엣은 (a) P09 화풍에 맞는 소품 에셋 구매 또는
/// (b) P09 자신의 카탈로그 메시(Shield_001~005 / Staff_001~003, 이미 정적 메시다)를 확대·재배치하는 쪽이
/// 화풍을 공짜로 맞춘다. 어느 쪽이든 <b>부착 메커니즘은 이 도구 그대로</b> 쓰면 된다.</para>
/// </summary>
public static class P09BossPresenceAttachmentTool
{
    private const string VisualPrefabPath =
        "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/Demo_Prefab/P09_Human_Combat_Demo Variant.prefab";
    private const string OutputFolder = "Logs/p09-boss-presence";
    private const int RenderWidth = 700;
    private const int RenderHeight = 1200;

    private static readonly Color Background = new(0.36f, 0.38f, 0.44f, 1f);

    [MenuItem("SM/Internal/P09/Render Boss Presence Comparison")]
    public static void RenderComparisonMenu()
    {
        const string characterId = "extra_sunken_bastion_adjudicator";
        Directory.CreateDirectory(OutputFolder);

        RenderOne(characterId, withAttachments: false, $"{characterId}_before.png");
        RenderOne(characterId, withAttachments: true, $"{characterId}_after.png");

        AssetDatabase.Refresh();
        Debug.Log($"[P09BossPresence] before/after written to {OutputFolder}.");
    }

    private static void RenderOne(string characterId, bool withAttachments, string fileName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
        if (prefab == null)
        {
            throw new FileNotFoundException($"Missing P09 visual prefab: {VisualPrefabPath}");
        }

        var preset = FindPreset(characterId);
        var generated = new List<Material>();
        PreviewRenderUtility? renderer = null;
        GameObject? instance = null;
        Texture2D? texture = null;
        try
        {
            instance = Object.Instantiate(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            if (preset != null)
            {
                preset.ApplyTo(instance.transform, generated);
            }

            P09PreviewPoseUtility.TryApplyDefaultIdlePose(instance, preset != null ? preset.SexId : 1);

            var profile = CharacterShowcasePreviewApplier.LoadDefault();
            if (CharacterShowcasePreviewApplier.ShouldUseReadableMaterials(profile))
            {
                P09DetailPreservingPaletteTool.ApplyPreviewReadableMaterials(instance.transform, generated);
            }

            if (withAttachments)
            {
                BuildAdjudicatorPresence(instance, generated);
            }

            renderer = new PreviewRenderUtility();
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
            File.WriteAllBytes(Path.Combine(OutputFolder, fileName).Replace('\\', '/'), texture.EncodeToPNG());
        }
        finally
        {
            renderer?.Cleanup();
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }

            foreach (var material in generated)
            {
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }

            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }
        }
    }

    /// <summary>
    /// 침몰한 보루의 재결관 — 학파 재판 권위. 2챕터 첫 벽이라 플레이어가 "보스"를 처음 인식하는 자리다.
    /// 세 조각을 붙인다. 셋 다 강체라 본을 그대로 타고 애니메이션이 살아 있다.
    ///   1) 등 뒤 현수막 기둥 — 실루엣을 <b>머리 위로</b> 올린다. 몸을 안 키우고 키를 버는 유일한 수단이고
    ///      군중 속에서 보스를 한눈에 찍게 만드는 가장 강한 신호다.
    ///   2) 비대칭 견갑 — 한쪽 어깨만 크게. 좌우 대칭이 깨지면 실루엣이 즉시 갈린다.
    ///   3) 향로 — 허리에 매단 의례 도구. 재판관이라는 직무를 실루엣에 적는다.
    /// </summary>
    private static void BuildAdjudicatorPresence(GameObject instance, List<Material> generated)
    {
        var animator = instance.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning("[P09BossPresence] Humanoid animator를 찾지 못해 부착물을 건너뛴다.");
            return;
        }

        var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest)
                    ?? animator.GetBoneTransform(HumanBodyBones.Chest);
        var shoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
        var hips = animator.GetBoneTransform(HumanBodyBones.Hips);

        var root = instance.transform;

        // 부착물이 "붙여 놓은 도형"으로 보이던 가장 큰 이유는 배치가 아니라 <b>재질</b>이었다.
        // 캐릭터는 툰 셰이딩된 텍스처 메시인데 부착물만 평면 단색이라 같은 화면에서 이물질이 된다.
        // 그래서 새 머티리얼을 만들지 않고 <b>이 캐릭터 자신의 갑옷 머티리얼을 그대로 빌린다.</b>
        var armor = BorrowMaterial(instance, "Armor") ?? CreateMaterial(new Color(0.20f, 0.21f, 0.25f), generated);
        var accent = BorrowMaterial(instance, "Weapon") ?? BorrowMaterial(instance, "Shield") ?? armor;

        // 치수는 짐작하지 않고 리그를 재서 쓴다(실측):
        //   모델 높이 1.661 · 머리끝 약 1.55 · Hips y=0.940 · UpperChest y=1.213
        //   RightShoulder (0.035, 1.255, -0.012) — 이 본은 <b>쇄골 뿌리</b>라 거의 정중선이다.
        //   삼각근은 그보다 바깥이므로 견갑은 여기서 더 밀어내야 한다.
        // 첫 두 번의 실패는 배치 논리가 아니라 전부 <b>1.66 짜리 캐릭터에 너무 큰 치수</b>였다.

        // 1) 등 뒤 현수막. 머리끝 1.55 위로 나와야 군중 속에서 먼저 보인다 → 상단 약 1.95를 노린다.
        if (chest != null)
        {
            var holder = new GameObject("BossPresence_Banner") { hideFlags = HideFlags.DontSave };
            Attach(holder, chest, root, new Vector3(-0.085f, 0f, -0.105f), new Vector3(7f, 0f, 4f), Vector3.one);

            // 기둥 중심 1.213+0.30=1.51, 길이 0.86 → 1.08 ~ 1.94
            var pole = CreateShape(ShapeType.Cylinder, "Pole", armor);
            SetLocal(pole, holder.transform, new Vector3(0f, 0.30f, 0f), Vector3.zero,
                new Vector3(0.019f, 0.86f, 0.019f));

            var banner = CreateShape(ShapeType.Cube, "Cloth", accent);
            SetLocal(banner, holder.transform, new Vector3(0.115f, 0.50f, 0.002f), Vector3.zero,
                new Vector3(0.21f, 0.30f, 0.010f));

            var finial = CreateShape(ShapeType.Cone, "Finial", accent);
            SetLocal(finial, holder.transform, new Vector3(0f, 0.76f, 0f), Vector3.zero,
                new Vector3(0.040f, 0.085f, 0.040f));
        }

        // 2) 비대칭 견갑 — 쇄골 본에서 캐릭터 오른쪽(+X)으로 삼각근까지 밀어낸다.
        if (shoulder != null)
        {
            var holder = new GameObject("BossPresence_Pauldron") { hideFlags = HideFlags.DontSave };
            Attach(holder, shoulder, root, new Vector3(0.115f, 0.015f, 0f), new Vector3(0f, 0f, -16f), Vector3.one);

            var plate = CreateShape(ShapeType.Cube, "Plate", armor);
            SetLocal(plate, holder.transform, Vector3.zero, Vector3.zero, new Vector3(0.155f, 0.062f, 0.155f));

            var upper = CreateShape(ShapeType.Cube, "Upper", armor);
            SetLocal(upper, holder.transform, new Vector3(-0.012f, 0.052f, 0f), Vector3.zero,
                new Vector3(0.120f, 0.050f, 0.128f));

            var crest = CreateShape(ShapeType.Cone, "Crest", accent);
            SetLocal(crest, holder.transform, new Vector3(-0.016f, 0.105f, 0f), Vector3.zero,
                new Vector3(0.048f, 0.070f, 0.048f));
        }

        // 3) 향로 — 칼(오른쪽 허리)과 겹치지 않게 왼쪽 뒤로 뺀다.
        if (hips != null)
        {
            var holder = new GameObject("BossPresence_Censer") { hideFlags = HideFlags.DontSave };
            Attach(holder, hips, root, new Vector3(-0.155f, 0.02f, -0.055f), Vector3.zero, Vector3.one);

            var chain = CreateShape(ShapeType.Cylinder, "Chain", accent);
            SetLocal(chain, holder.transform, new Vector3(0f, -0.055f, 0f), Vector3.zero,
                new Vector3(0.009f, 0.12f, 0.009f));

            var bowl = CreateShape(ShapeType.Sphere, "Bowl", accent);
            SetLocal(bowl, holder.transform, new Vector3(0f, -0.145f, 0f), Vector3.zero,
                new Vector3(0.075f, 0.068f, 0.075f));
        }
    }

    private static void SetLocal(GameObject go, Transform parent, Vector3 position, Vector3 euler, Vector3 scale)
    {
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
    }

    /// <summary>
    /// 이 캐릭터가 실제로 쓰고 있는 머티리얼을 빌려온다. 부착물이 같은 셰이더·같은 톤으로
    /// 그려져야 한 벌로 보인다 — 색만 맞춘 단색 머티리얼로는 안 된다.
    /// </summary>
    private static Material? BorrowMaterial(GameObject instance, string nameContains)
    {
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(includeInactive: false))
        {
            if (!renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            var material = renderer.sharedMaterial;
            if (material != null && material.name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return material;
            }
        }

        return null;
    }

    private static GameObject CreateShape(ShapeType shape, string name, Material material)
    {
        var mesh = ShapeGenerator.CreateShape(shape);
        mesh.gameObject.name = name;
        mesh.gameObject.hideFlags = HideFlags.DontSave;
        mesh.ToMesh();
        mesh.Refresh();

        var meshRenderer = mesh.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sharedMaterial = material;
        }

        return mesh.gameObject;
    }

    /// <summary>
    /// <b>본 로컬 공간에 직접 저작하지 않는다.</b> Humanoid 본은 축이 임의로 회전·스케일돼 있어서
    /// "뒤로 0.16" 같은 직관적 오프셋이 전혀 다른 방향으로 간다 — 첫 렌더에서 깃대가 옆으로 눕고
    /// 첨두가 허공에 떠올랐다. 그래서 부모는 본으로 두되(애니메이션은 그대로 타야 하니까),
    /// 위치·회전은 <b>캐릭터 공간</b>(x=오른쪽, y=위, z=정면)에서 잡고 스케일은 부모 스케일을 상쇄한다.
    /// 저작값이 곧 화면에서 보이는 값이 된다.
    /// </summary>
    private static void Attach(
        GameObject go,
        Transform bone,
        Transform root,
        Vector3 characterSpaceOffset,
        Vector3 characterSpaceEuler,
        Vector3 worldSize)
    {
        go.transform.SetParent(bone, worldPositionStays: false);
        go.transform.position = bone.position + root.rotation * characterSpaceOffset;
        go.transform.rotation = root.rotation * Quaternion.Euler(characterSpaceEuler);

        var parentScale = bone.lossyScale;
        go.transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(Mathf.Abs(parentScale.x), 1e-5f),
            worldSize.y / Mathf.Max(Mathf.Abs(parentScale.y), 1e-5f),
            worldSize.z / Mathf.Max(Mathf.Abs(parentScale.z), 1e-5f));
    }

    private static Material CreateMaterial(Color color, List<Material> generated)
    {
        // 첫 렌더에서 깃대와 천이 둘 다 창백하게 나왔다 — 프리뷰 전용 셰이더의 색 프로퍼티 이름을
        // 짐작해서 넣었기 때문이다. 색이 확실히 먹는 셰이더를 먼저 고르고, 알려진 이름을 전부 채운다.
        var shader = Shader.Find("Unlit/Color")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Hidden/SM/P09PreviewTintedUnlit");
        var material = new Material(shader) { hideFlags = HideFlags.DontSave };
        foreach (var property in new[] { "_Color", "_BaseColor", "_UnlitColor", "_MainColor" })
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, color);
            }
        }

        generated.Add(material);
        return material;
    }

    private static BattleP09AppearancePreset? FindPreset(string characterId)
    {
        foreach (var preset in Resources.LoadAll<BattleP09AppearancePreset>(BattleP09AppearancePreset.ResourcesFolder))
        {
            if (preset != null && string.Equals(preset.CharacterId, characterId, System.StringComparison.Ordinal))
            {
                return preset;
            }
        }

        return null;
    }

    /// <summary>
    /// 부착 전/후를 <b>같은 화각</b>으로 찍는다. 부착물 때문에 bounds가 커져 카메라가 물러나면
    /// 커진 실루엣이 렌더에서 상쇄돼 비교가 무의미해진다 — 그래서 몸통 기준으로 고정한다.
    /// </summary>
    private static void FrameCamera(Camera camera, Transform root)
    {
        var target = new Vector3(0f, 1.02f, 0f);
        var distance = 4.15f;
        var direction = Quaternion.Euler(3f, -28f, 0f) * Vector3.forward;
        camera.transform.position = target + direction * distance;
        camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = distance * 4f;
    }
}

}
