using System.Collections.Generic;
using System.Linq;
using SM.Unity;
using SM.Unity.UI;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

public static class BattleActorWrapperValidator
{
    private const string PrefabRoot = "Assets/_Game/Prefabs/Battle/Actors";

    [MenuItem("SM/Validation/Validate Battle Actor Wrappers")]
    public static void ValidateProjectWrappers()
    {
        var reports = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => new
            {
                Path = path,
                Wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<BattleActorWrapper>(),
            })
            .Where(entry => entry.Wrapper != null)
            .Select(entry => Validate(entry.Wrapper!, entry.Path))
            .ToList();

        var errors = reports.Where(report => report.HasErrors).ToList();
        if (errors.Count > 0)
        {
            throw new System.InvalidOperationException(string.Join("\n\n", errors.Select(report => report.BuildSummary())));
        }

        Debug.Log($"Battle actor wrapper validation passed for {reports.Count} prefab(s).");
    }

    public static BattleActorWrapperValidationReport Validate(BattleActorWrapper wrapper, string assetPath)
    {
        var issues = new List<BattleActorWrapperValidationIssue>();
        var go = wrapper.gameObject;

        if (go.GetComponent<BattleActorView>() == null)
        {
            issues.Add(new BattleActorWrapperValidationIssue(true, "missing_view", "BattleActorView component is required."));
        }

        var adapters = go.GetComponents<BattleActorVisualAdapter>();
        if (adapters.Length != 1)
        {
            issues.Add(new BattleActorWrapperValidationIssue(true, "adapter_count", "Exactly one BattleActorVisualAdapter is required."));
        }

        if (go.transform.localScale != Vector3.one)
        {
            issues.Add(new BattleActorWrapperValidationIssue(true, "root_scale", "Wrapper root local scale must be 1,1,1."));
        }

        if (go.transform.localRotation != Quaternion.identity)
        {
            issues.Add(new BattleActorWrapperValidationIssue(true, "root_rotation", "Wrapper root local rotation must be identity."));
        }

        if (wrapper.VisualRoot == wrapper.transform)
        {
            issues.Add(new BattleActorWrapperValidationIssue(false, "visual_root_fallback", "VisualRoot is using the wrapper root fallback instead of an authored child."));
        }

        foreach (var socket in wrapper.CaptureSocketStatus())
        {
            if (socket.SocketId is BattleActorSocketId.Center
                or BattleActorSocketId.Head
                or BattleActorSocketId.Hud
                or BattleActorSocketId.FeetRing
                or BattleActorSocketId.Telegraph)
            {
                if (socket.UsesFallback)
                {
                    issues.Add(new BattleActorWrapperValidationIssue(false, $"fallback_{socket.SocketId}", $"{socket.SocketId} is using fallback positioning."));
                }
            }
        }

        var worldSpaceCanvas = go.GetComponentsInChildren<Canvas>(true).FirstOrDefault(canvas => canvas.renderMode == RenderMode.WorldSpace);
        if (worldSpaceCanvas != null)
        {
            issues.Add(new BattleActorWrapperValidationIssue(true, "world_space_hud", "World-space Canvas is forbidden inside battle actor wrappers."));
        }

        if (go.GetComponentInChildren<RuntimePanelHost>(true) != null)
        {
            issues.Add(new BattleActorWrapperValidationIssue(true, "runtime_panel_host", "RuntimePanelHost must not live inside a battle actor wrapper."));
        }

        if (go.GetComponentInChildren<BattleCameraController>(true) != null)
        {
            issues.Add(new BattleActorWrapperValidationIssue(true, "camera_controller", "BattleCameraController must not live inside a battle actor wrapper."));
        }

        return new BattleActorWrapperValidationReport(assetPath, issues);
    }
}

public readonly record struct BattleActorWrapperValidationIssue(bool IsError, string Code, string Message);

public sealed class BattleActorWrapperValidationReport
{
    public BattleActorWrapperValidationReport(string assetPath, IReadOnlyList<BattleActorWrapperValidationIssue> issues)
    {
        AssetPath = assetPath;
        Issues = issues;
    }

    public string AssetPath { get; }
    public IReadOnlyList<BattleActorWrapperValidationIssue> Issues { get; }
    public bool HasErrors => Issues.Any(issue => issue.IsError);

    public string BuildSummary()
    {
        var lines = new List<string> { AssetPath };
        lines.AddRange(Issues.Select(issue => $"{(issue.IsError ? "ERROR" : "WARN")} {issue.Code}: {issue.Message}"));
        return string.Join("\n", lines);
    }
}
