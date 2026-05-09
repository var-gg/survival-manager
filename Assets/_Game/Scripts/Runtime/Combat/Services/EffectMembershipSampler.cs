using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class EffectMembershipSampler
{
    private const float TelemetryAuraRadius = 1.65f;
    private const float TelemetryAoeRadius = 1.55f;

    public static EffectPositionSnapshot GetOrSample(BattleState state)
    {
        if (state.EffectPositionSnapshot != null && state.EffectPositionSnapshotStep == state.StepIndex)
        {
            return state.EffectPositionSnapshot;
        }

        var snapshot = SamplePositions(state);
        state.SetEffectPositionSnapshot(snapshot);
        return snapshot;
    }

    public static EffectPositionSnapshot SamplePositions(BattleState state)
    {
        return new EffectPositionSnapshot(
            state.StepIndex,
            state.ElapsedSeconds,
            state.AllUnits
                .Where(unit => unit.IsAlive)
                .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
                .Select(unit => new EffectPositionUnit(
                    unit.Id.Value,
                    unit.Side,
                    unit.Position,
                    unit.HealthRatio,
                    unit.Behavior.FormationLine,
                    unit.HasStatus("marked"),
                    unit.HasStatus("exposed")))
                .ToList());
    }

    public static ClusterTradeoffTelemetryFrame SampleStep(BattleState state)
    {
        var snapshot = SamplePositions(state);
        state.SetEffectPositionSnapshot(snapshot);
        return BuildTelemetryFrame(state, snapshot);
    }

    public static AoeClusterSelection ResolveAoeSkill(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot primaryTarget,
        BattleSkillSpec skill)
    {
        var snapshot = GetOrSample(state);
        var radius = skill.AreaRadius > 0f ? skill.AreaRadius : Math.Max(1.1f, skill.TargetRuleData?.ClusterRadius ?? 0f);
        return AoeClusterSelectorService.Select(
            snapshot,
            primaryTarget.Side,
            skill.AreaEffectFamily,
            radius,
            skill.PunishCluster ? 0.12f : 0f,
            0f,
            primaryTarget.Id.Value);
    }

    private static ClusterTradeoffTelemetryFrame BuildTelemetryFrame(BattleState state, EffectPositionSnapshot snapshot)
    {
        var buffCoverage = new Dictionary<string, float>(StringComparer.Ordinal);
        var buffBonus = new Dictionary<string, float>(StringComparer.Ordinal);
        var overcap = 0;
        var missed = 0;
        var buffValue = 0f;
        foreach (var side in new[] { TeamSide.Ally, TeamSide.Enemy })
        {
            var team = snapshot.Units.Where(unit => unit.Side == side).ToList();
            if (team.Count == 0)
            {
                continue;
            }

            var center = Centroid(team);
            var kind = state.GetTacticContext(side).ProtectCarryBias > 0.3f
                ? BuffPropagationKind.ShieldBarrier
                : BuffPropagationKind.AttackBuff;
            var result = BuffPropagationService.Resolve(
                snapshot,
                new BuffPropagationRequest(
                    $"telemetry:{side}",
                    side,
                    center,
                    TelemetryAuraRadius,
                    kind,
                    kind.ToString(),
                    10f));
            buffCoverage[$"{side}:{kind}"] = result.EffectiveCount;
            buffBonus[$"{side}:{kind}"] = result.EfficacyBonus;
            overcap += result.OvercapEvents;
            missed += result.MissedByDistanceCount;
            buffValue += result.EffectiveValue - result.BaseValue;
        }

        var catchHistogram = new Dictionary<string, float>(StringComparer.Ordinal);
        var bestAoeScore = 0f;
        var cleaveCatch = 0;
        var chainJumps = 0;
        var aoeCost = 0f;
        foreach (var side in new[] { TeamSide.Ally, TeamSide.Enemy })
        {
            var selection = AoeClusterSelectorService.Select(
                snapshot,
                side,
                BattleAreaEffectFamily.GroundAoe,
                TelemetryAoeRadius);
            bestAoeScore = Math.Max(bestAoeScore, selection.Candidate.Score);
            var catchKey = selection.Hits.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            catchHistogram[catchKey] = catchHistogram.TryGetValue(catchKey, out var current) ? current + 1f : 1f;

            cleaveCatch += AoeClusterSelectorService.Select(snapshot, side, BattleAreaEffectFamily.CleaveCone, TelemetryAoeRadius).Hits.Count;
            chainJumps += Math.Max(0, AoeClusterSelectorService.Select(snapshot, side, BattleAreaEffectFamily.Chain, TelemetryAoeRadius).Hits.Count - 1);
            var compactness = state.GetTacticContext(side).Compactness;
            aoeCost += selection.Hits.Sum(hit => hit.DamageMultiplier) * (0.35f + compactness);
        }

        const float focusValue = 0f;
        var clusterCohesion = ResolveClusterCohesion(snapshot);
        var net = buffValue + focusValue - aoeCost;
        return new ClusterTradeoffTelemetryFrame(
            clusterCohesion,
            buffCoverage,
            buffBonus,
            overcap,
            missed,
            bestAoeScore,
            catchHistogram,
            cleaveCatch,
            chainJumps,
            0,
            ResolveReclusterLatencyMs(state),
            focusValue,
            buffValue,
            aoeCost,
            net);
    }

    private static float ResolveClusterCohesion(EffectPositionSnapshot snapshot)
    {
        var values = new List<float>();
        foreach (var side in new[] { TeamSide.Ally, TeamSide.Enemy })
        {
            var team = snapshot.Units.Where(unit => unit.Side == side).ToList();
            if (team.Count < 2)
            {
                continue;
            }

            var sum = 0f;
            var count = 0;
            for (var i = 0; i < team.Count; i++)
            {
                for (var j = i + 1; j < team.Count; j++)
                {
                    sum += team[i].Position.DistanceTo(team[j].Position);
                    count++;
                }
            }

            if (count > 0)
            {
                values.Add(1f / (1f + (sum / count)));
            }
        }

        return values.Count == 0 ? 0f : values.Sum() / values.Count;
    }

    private static float ResolveReclusterLatencyMs(BattleState state)
    {
        var active = state.ActiveGroupDispersalLocks.ToList();
        if (active.Count == 0)
        {
            return 0f;
        }

        var remaining = active.Average(lockState => Math.Max(0f, lockState.DispersedUntilSeconds - state.ElapsedSeconds));
        return remaining * 1000f;
    }

    private static CombatVector2 Centroid(IReadOnlyList<EffectPositionUnit> units)
    {
        if (units.Count == 0)
        {
            return CombatVector2.Zero;
        }

        return new CombatVector2(
            units.Sum(unit => unit.Position.X) / units.Count,
            units.Sum(unit => unit.Position.Y) / units.Count);
    }
}
