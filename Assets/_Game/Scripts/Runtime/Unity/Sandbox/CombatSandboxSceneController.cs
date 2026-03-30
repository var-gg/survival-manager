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

    public CombatSandboxConfig SandboxConfig => sandboxConfig;
    public Transform[] AllyAnchorHandles => allyAnchorHandles ?? Array.Empty<Transform>();
    public Transform[] EnemyAnchorHandles => enemyAnchorHandles ?? Array.Empty<Transform>();
    public float RangePreviewRadius => Mathf.Max(0.25f, rangePreviewRadius);

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
                seed);
            lastResult = BattleResolver.Run(state, 300);
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
