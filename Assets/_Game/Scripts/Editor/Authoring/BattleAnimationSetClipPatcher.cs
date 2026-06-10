using System.Linq;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring;

/// <summary>
/// authored <c>BattleHumanoidAnimationSet.asset</c>에 새 클립 필드를 배선하는 소형 패처.
/// FBX 서브에셋 fileID를 손으로 추측해 YAML을 만지는 대신 AssetDatabase로 클립을 로드해
/// SerializedObject로 기록한다(에디터 fallback 세트와 동일 클립 소스 유지).
/// </summary>
public static class BattleAnimationSetClipPatcher
{
    private const string AnimationSetAssetPath = "Assets/Resources/_Game/Battle/BattleHumanoidAnimationSet.asset";
    private const string BowIdleClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Bow/HumanM@BowIdle01.fbx";

    [MenuItem("SM/Internal/Content/Wire Bow Ready Idle Clip")]
    public static void WireBowReadyIdle()
    {
        var set = AssetDatabase.LoadAssetAtPath<BattleHumanoidAnimationSet>(AnimationSetAssetPath);
        if (set == null)
        {
            Debug.LogError($"[AnimSetPatch] animation set not found: {AnimationSetAssetPath}");
            return;
        }

        var clip = LoadFirstClip(BowIdleClipPath);
        if (clip == null)
        {
            Debug.LogError($"[AnimSetPatch] bow idle clip not found: {BowIdleClipPath}");
            return;
        }

        var serialized = new SerializedObject(set);
        var property = serialized.FindProperty("bowReadyIdle");
        if (property == null)
        {
            Debug.LogError("[AnimSetPatch] field 'bowReadyIdle' not found on BattleHumanoidAnimationSet.");
            return;
        }

        property.objectReferenceValue = clip;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AnimSetPatch] bowReadyIdle <- {clip.name} ({BowIdleClipPath})");
    }

    private static AnimationClip? LoadFirstClip(string path)
    {
        return AssetDatabase
            .LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__", System.StringComparison.Ordinal));
    }
}
