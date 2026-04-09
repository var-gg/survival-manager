using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using UnityEngine;

namespace SM.Unity.Sandbox;

public sealed class CombatSandboxSceneController : MonoBehaviour
{
    [SerializeField] private CombatSandboxConfig sandboxConfig = null!;
    [SerializeField] private CombatSandboxSceneLayoutAsset sceneLayout = null!;
    [SerializeField] private CombatSandboxPreviewSettingsAsset previewSettings = null!;
    [SerializeField] private Transform[] allyAnchorHandles = Array.Empty<Transform>();
    [SerializeField] private Transform[] enemyAnchorHandles = Array.Empty<Transform>();
    [SerializeField] private float fallbackRangePreviewRadius = 2f;
    [SerializeField] private float fallbackNavigationPreviewRadius = 0.5f;
    [SerializeField] private float fallbackSeparationPreviewRadius = 0.75f;
    [SerializeField] private float fallbackPreferredRangeMinPreview = 1f;
    [SerializeField] private float fallbackPreferredRangeMaxPreview = 3f;
    [SerializeField] private float fallbackEngagementSlotRadiusPreview = 1.25f;
    [SerializeField] private int fallbackEngagementSlotCountPreview = 4;
    [SerializeField] private float fallbackHeadAnchorHeightPreview = 2f;
    [SerializeField] private float fallbackFrontlineGuardRadiusPreview = 2.5f;
    [SerializeField] private float fallbackClusterRadiusPreview = 2.5f;

    public CombatSandboxConfig SandboxConfig => sandboxConfig;
    public CombatSandboxSceneLayoutAsset SceneLayout => sceneLayout != null ? sceneLayout : sandboxConfig != null ? sandboxConfig.SceneLayout : null!;
    public CombatSandboxPreviewSettingsAsset PreviewSettings => previewSettings != null ? previewSettings : sandboxConfig != null ? sandboxConfig.PreviewSettings : null!;
    public Transform[] AllyAnchorHandles => allyAnchorHandles ?? Array.Empty<Transform>();
    public Transform[] EnemyAnchorHandles => enemyAnchorHandles ?? Array.Empty<Transform>();
    public float RangePreviewRadius => Mathf.Max(0.25f, previewSettings != null || (sandboxConfig != null && sandboxConfig.PreviewSettings != null) ? PreviewSettings.RangePreviewRadius : fallbackRangePreviewRadius);
    public float NavigationPreviewRadius => Mathf.Max(0.1f, previewSettings != null || (sandboxConfig != null && sandboxConfig.PreviewSettings != null) ? PreviewSettings.NavigationPreviewRadius : fallbackNavigationPreviewRadius);
    public float SeparationPreviewRadius => Mathf.Max(NavigationPreviewRadius, previewSettings != null || (sandboxConfig != null && sandboxConfig.PreviewSettings != null) ? PreviewSettings.SeparationPreviewRadius : fallbackSeparationPreviewRadius);
    public float PreferredRangeMinPreview => Mathf.Max(0f, previewSettings != null || (sandboxConfig != null && sandboxConfig.PreviewSettings != null) ? PreviewSettings.PreferredRangeMinPreview : fallbackPreferredRangeMinPreview);
    public float PreferredRangeMaxPreview => Mathf.Max(PreferredRangeMinPreview, previewSettings != null || (sandboxConfig != null && sandboxConfig.PreviewSettings != null) ? PreviewSettings.PreferredRangeMaxPreview : fallbackPreferredRangeMaxPreview);
    public float EngagementSlotRadiusPreview => Mathf.Max(0.25f, previewSettings != null || (sandboxConfig != null && sandboxConfig.PreviewSettings != null) ? PreviewSettings.EngagementSlotRadiusPreview : fallbackEngagementSlotRadiusPreview);
    public int EngagementSlotCountPreview => Mathf.Max(1, previewSettings != null || (sandboxConfig != null && sandboxConfig.PreviewSettings != null) ? PreviewSettings.EngagementSlotCountPreview : fallbackEngagementSlotCountPreview);
    public float HeadAnchorHeightPreview => Mathf.Max(0.5f, previewSettings != null || (sandboxConfig != null && sandboxConfig.PreviewSettings != null) ? PreviewSettings.HeadAnchorHeightPreview : fallbackHeadAnchorHeightPreview);
    public float FrontlineGuardRadiusPreview => Mathf.Max(0.5f, previewSettings != null || (sandboxConfig != null && sandboxConfig.PreviewSettings != null) ? PreviewSettings.FrontlineGuardRadiusPreview : fallbackFrontlineGuardRadiusPreview);
    public float ClusterRadiusPreview => Mathf.Max(0.5f, previewSettings != null || (sandboxConfig != null && sandboxConfig.PreviewSettings != null) ? PreviewSettings.ClusterRadiusPreview : fallbackClusterRadiusPreview);
    [SerializeField] private float spawnOffsetX = 1.25f;

    public BattlefieldLayout ExportSceneLayout()
    {
        if (SceneLayout != null)
        {
#if UNITY_EDITOR
            SceneLayout.CaptureFromScene(AllyAnchorHandles, EnemyAnchorHandles);
            UnityEditor.EditorUtility.SetDirty(SceneLayout);
#endif
            return SceneLayout.BuildBattlefieldLayout();
        }

        var allyPoses = CombatSandboxSceneLayoutAsset.CreateDefaultAnchors(isEnemy: false);
        var enemyPoses = CombatSandboxSceneLayoutAsset.CreateDefaultAnchors(isEnemy: true);
        CaptureFallbackHandles(allyPoses, AllyAnchorHandles);
        CaptureFallbackHandles(enemyPoses, EnemyAnchorHandles);
        return CombatSandboxSceneLayoutCompiler.BuildBattlefieldLayout(allyPoses, enemyPoses, spawnOffsetX);
    }

    public static CombatSandboxRunResult Execute(CombatSandboxRunRequest request)
    {
        var batchCount = Math.Max(1, request.BatchCount);
        var wins = 0;
        var totalDuration = 0f;
        var totalEvents = 0f;
        var totalFirstActionTime = 0f;
        BattleResult lastResult = null!;
        BattleReplayBundle lastReplay = null!;

        for (var index = 0; index < batchCount; index++)
        {
            var seed = request.Seed + index;
            var state = BattleFactory.Create(
                request.PlayerSnapshot.Allies,
                request.EnemyLoadout,
                request.PlayerSnapshot.TeamTactic.Posture,
                request.EnemyLoadout.FirstOrDefault()?.TeamTactic?.Posture ?? TeamPostureType.StandardAdvance,
                BattleSimulator.DefaultFixedStepSeconds,
                seed,
                layout: request.Layout);
            lastResult = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps);
            if (lastResult.Winner == TeamSide.Ally)
            {
                wins++;
            }

            totalDuration += lastResult.DurationSeconds;
            totalEvents += lastResult.Events.Count;
            totalFirstActionTime += lastResult.Events
                .Where(@event => @event.ActionType != BattleActionType.WaitDefend)
                .Select(@event => @event.TimeSeconds)
                .DefaultIfEmpty(lastResult.DurationSeconds)
                .First();
            var timestamp = DateTime.UtcNow.ToString("O");
            lastReplay = ReplayAssembler.Assemble(request.PlayerSnapshot, request.EnemyLoadout, lastResult, seed, timestamp, timestamp);
        }

        var metrics = new CombatSandboxMetrics(
            wins / (float)batchCount,
            totalDuration / batchCount,
            totalEvents / batchCount,
            totalFirstActionTime / batchCount,
            batchCount);

        return new CombatSandboxRunResult(
            request.PlayerSnapshot,
            request.EnemyLoadout,
            lastReplay,
            metrics,
            lastReplay.Header.FinalStateHash,
            request.PlayerSnapshot.Provenance ?? Array.Empty<CompileProvenanceEntry>());
    }

    private static void CaptureFallbackHandles(IReadOnlyList<CombatSandboxAnchorPose> poses, Transform[] handles)
    {
        for (var index = 0; index < poses.Count && index < handles.Length; index++)
        {
            if (handles[index] == null)
            {
                continue;
            }

            poses[index].Position = handles[index].position;
        }
    }
}
