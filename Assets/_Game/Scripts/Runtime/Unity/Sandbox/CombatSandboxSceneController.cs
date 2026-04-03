using System;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using UnityEngine;

namespace SM.Unity.Sandbox;

public sealed class CombatSandboxSceneController : MonoBehaviour
{
    [SerializeField] private CombatSandboxConfig sandboxConfig = null!;
    [SerializeField] private Transform[] allyAnchorHandles = Array.Empty<Transform>();
    [SerializeField] private Transform[] enemyAnchorHandles = Array.Empty<Transform>();
    [SerializeField] private float rangePreviewRadius = 2f;
    [SerializeField] private float navigationPreviewRadius = 0.5f;
    [SerializeField] private float separationPreviewRadius = 0.75f;
    [SerializeField] private float preferredRangeMinPreview = 1f;
    [SerializeField] private float preferredRangeMaxPreview = 3f;
    [SerializeField] private float engagementSlotRadiusPreview = 1.25f;
    [SerializeField] private int engagementSlotCountPreview = 4;
    [SerializeField] private float headAnchorHeightPreview = 2f;
    [SerializeField] private float frontlineGuardRadiusPreview = 2.5f;

    public CombatSandboxConfig SandboxConfig => sandboxConfig;
    public Transform[] AllyAnchorHandles => allyAnchorHandles ?? Array.Empty<Transform>();
    public Transform[] EnemyAnchorHandles => enemyAnchorHandles ?? Array.Empty<Transform>();
    public float RangePreviewRadius => Mathf.Max(0.25f, rangePreviewRadius);
    public float NavigationPreviewRadius => Mathf.Max(0.1f, navigationPreviewRadius);
    public float SeparationPreviewRadius => Mathf.Max(NavigationPreviewRadius, separationPreviewRadius);
    public float PreferredRangeMinPreview => Mathf.Max(0f, preferredRangeMinPreview);
    public float PreferredRangeMaxPreview => Mathf.Max(PreferredRangeMinPreview, preferredRangeMaxPreview);
    public float EngagementSlotRadiusPreview => Mathf.Max(0.25f, engagementSlotRadiusPreview);
    public int EngagementSlotCountPreview => Mathf.Max(1, engagementSlotCountPreview);
    public float HeadAnchorHeightPreview => Mathf.Max(0.5f, headAnchorHeightPreview);
    public float FrontlineGuardRadiusPreview => Mathf.Max(0.5f, frontlineGuardRadiusPreview);
    [SerializeField] private float spawnOffsetX = 1.25f;

    public BattlefieldLayout ExportSceneLayout()
    {
        var handles = AllyAnchorHandles;
        if (handles.Length < 6)
            return BattlefieldLayout.Default;

        var frontX = (Mathf.Abs(handles[0].position.x)
                    + Mathf.Abs(handles[1].position.x)
                    + Mathf.Abs(handles[2].position.x)) / 3f;
        var backX = (Mathf.Abs(handles[3].position.x)
                   + Mathf.Abs(handles[4].position.x)
                   + Mathf.Abs(handles[5].position.x)) / 3f;
        var topY = (handles[0].position.z + handles[3].position.z) / 2f;
        var centerY = (handles[1].position.z + handles[4].position.z) / 2f;
        var bottomY = (handles[2].position.z + handles[5].position.z) / 2f;

        return new BattlefieldLayout(frontX, backX, topY, centerY, bottomY, Mathf.Max(0.25f, spawnOffsetX));
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
}
