using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace SM.Editor.Authoring.P09Appearance;

internal static class P09PreviewPoseUtility
{
    private const float SampleTimeRatio = 0.35f;

    private static readonly string[] MaleIdleClipPaths =
    {
        "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/Animation/Demo_Pose/P09_Male_idle.anim",
        "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/Animation/Demo_Pose/P09_Weapon_idle.anim",
    };

    private static readonly string[] FemaleIdleClipPaths =
    {
        "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/Animation/Demo_Pose/P09_Fem_idle.anim",
        "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/Animation/Demo_Pose/P09_Weapon_idle.anim",
    };

    public static bool TryApplyDefaultIdlePose(GameObject root, int sexId)
    {
        if (root == null)
        {
            return false;
        }

        var position = root.transform.position;
        var rotation = root.transform.rotation;
        var scale = root.transform.localScale;
        var clip = LoadIdleClip(sexId);
        if (clip != null)
        {
            var sampleTime = Mathf.Clamp(clip.length * SampleTimeRatio, 0f, Mathf.Max(clip.length, 0f));
            clip.SampleAnimation(root, sampleTime);
            ApplyActiveParentConstraints(root);
            root.transform.SetPositionAndRotation(position, rotation);
            root.transform.localScale = scale;
            DisableAnimators(root);
            return true;
        }

        return TryEvaluateExistingAnimator(root);
    }

    private static AnimationClip? LoadIdleClip(int sexId)
    {
        var preferredPaths = sexId == 2 ? FemaleIdleClipPaths : MaleIdleClipPaths;
        foreach (var path in preferredPaths)
        {
            var clip = LoadClipAtPath(path);
            if (clip != null)
            {
                return clip;
            }
        }

        var prefix = sexId == 2 ? "P09_Fem" : "P09_Male";
        return AssetDatabase
            .FindAssets($"{prefix} t:AnimationClip", new[] { "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/Animation/Demo_Pose" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(LoadClipAtPath)
            .FirstOrDefault(clip => clip != null);
    }

    private static bool TryEvaluateExistingAnimator(GameObject root)
    {
        var animators = root
            .GetComponentsInChildren<Animator>(true)
            .Where(animator => animator.runtimeAnimatorController != null)
            .ToArray();
        if (animators.Length == 0)
        {
            return false;
        }

        var position = root.transform.position;
        var rotation = root.transform.rotation;
        var scale = root.transform.localScale;
        foreach (var animator in animators)
        {
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
            animator.Play(0, 0, SampleTimeRatio);
            animator.Update(0f);
            animator.Update(1f / 30f);
        }

        ApplyActiveParentConstraints(root);
        DisableAnimators(root);
        root.transform.SetPositionAndRotation(position, rotation);
        root.transform.localScale = scale;
        return true;
    }

    private static AnimationClip? LoadClipAtPath(string path)
    {
        return AssetDatabase
            .LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__", StringComparison.Ordinal));
    }

    private static void DisableAnimators(GameObject root)
    {
        foreach (var animator in root.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    private static void ApplyActiveParentConstraints(GameObject root)
    {
        foreach (var constraint in root.GetComponentsInChildren<ParentConstraint>(true))
        {
            if (!constraint.constraintActive || constraint.sourceCount == 0)
            {
                continue;
            }

            var sourceIndex = ResolveDominantSourceIndex(constraint);
            if (sourceIndex < 0)
            {
                continue;
            }

            var source = constraint.GetSource(sourceIndex).sourceTransform;
            if (source == null)
            {
                continue;
            }

            var constrained = constraint.transform;
            constrained.position = source.TransformPoint(constraint.GetTranslationOffset(sourceIndex));
            constrained.rotation = source.rotation * Quaternion.Euler(constraint.GetRotationOffset(sourceIndex));
        }
    }

    private static int ResolveDominantSourceIndex(ParentConstraint constraint)
    {
        var bestIndex = -1;
        var bestWeight = 0f;
        for (var i = 0; i < constraint.sourceCount; i++)
        {
            var source = constraint.GetSource(i);
            if (source.sourceTransform == null || source.weight <= bestWeight)
            {
                continue;
            }

            bestIndex = i;
            bestWeight = source.weight;
        }

        return bestIndex;
    }
}
