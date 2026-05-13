using SM.Unity.P09Appearance;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.P09Appearance;

/// <summary>
/// <see cref="CharacterShowcaseProfile"/>을 <see cref="PreviewRenderUtility"/>에 일관되게 적용한다.
/// Studio preview / Wiki capture 양쪽이 이 helper 하나만 호출하므로 두 출력이 강제 동일해진다.
/// </summary>
public static class CharacterShowcasePreviewApplier
{
    public const string DefaultProfilePath = "Assets/_Game/Authoring/Character/CharacterShowcase_Default.asset";

    private static CharacterShowcaseProfile? _cachedDefault;

    /// <summary>Default profile을 로드. 없으면 null. 호출자가 fallback 처리.</summary>
    public static CharacterShowcaseProfile? LoadDefault()
    {
        if (_cachedDefault != null) return _cachedDefault;

        _cachedDefault = AssetDatabase.LoadAssetAtPath<CharacterShowcaseProfile>(DefaultProfilePath);
        if (_cachedDefault != null) return _cachedDefault;

        // Fallback: 프로젝트 어디든 CharacterShowcaseProfile가 있으면 첫 번째 사용.
        var guids = AssetDatabase.FindAssets("t:CharacterShowcaseProfile");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _cachedDefault = AssetDatabase.LoadAssetAtPath<CharacterShowcaseProfile>(path);
        }
        return _cachedDefault;
    }

    public static void InvalidateCache()
    {
        _cachedDefault = null;
    }

    /// <summary>
    /// PreviewRenderUtility의 카메라 / ambient / 2개 라이트에 profile 값을 적용.
    /// 카메라 framing(거리/각도)은 호출자가 별도 처리 (캐릭터별 bounding 따라 다름).
    /// </summary>
    public static void ApplyTo(PreviewRenderUtility renderer, CharacterShowcaseProfile profile)
    {
        if (renderer == null || profile == null) return;

        renderer.cameraFieldOfView = profile.fieldOfView;
        var camera = renderer.camera;
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = profile.backgroundColor;
            camera.nearClipPlane = profile.nearClip;
            camera.farClipPlane = profile.farClip;
            camera.allowHDR = profile.allowHdr;
        }

        renderer.ambientColor = profile.ambientColor;

        if (renderer.lights != null && renderer.lights.Length >= 1 && renderer.lights[0] != null)
        {
            renderer.lights[0].color = profile.keyColor;
            renderer.lights[0].intensity = profile.keyIntensity;
            renderer.lights[0].transform.rotation = Quaternion.Euler(profile.keyRotation);
        }
        if (renderer.lights != null && renderer.lights.Length >= 2 && renderer.lights[1] != null)
        {
            renderer.lights[1].color = profile.fillColor;
            renderer.lights[1].intensity = profile.fillIntensity;
            renderer.lights[1].transform.rotation = Quaternion.Euler(profile.fillRotation);
        }
    }

    public static bool ShouldUseReadableMaterials(CharacterShowcaseProfile? profile)
    {
        if (profile == null) return true; // 기본은 안전한 readable swap
        return profile.shaderTreatment == CharacterShowcaseProfile.ShaderTreatment.CompatibilityReadable;
    }

    /// <summary>asset이 아직 없으면 default path에 생성한다. Studio 처음 열 때 자동 호출 가능.</summary>
    public static CharacterShowcaseProfile EnsureDefaultExists()
    {
        var existing = LoadDefault();
        if (existing != null) return existing;

        var dir = System.IO.Path.GetDirectoryName(DefaultProfilePath)!.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
        {
            CreateFolderRecursive(dir);
        }
        var profile = ScriptableObject.CreateInstance<CharacterShowcaseProfile>();
        AssetDatabase.CreateAsset(profile, DefaultProfilePath);
        AssetDatabase.SaveAssets();
        InvalidateCache();
        return LoadDefault()!;
    }

    private static void CreateFolderRecursive(string folderPath)
    {
        var parts = folderPath.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
