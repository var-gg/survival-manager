using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.HeadlessMetrics;

/// <summary>기존 BattleState/BattleResult를 H100 원시 레코드로 투영하는 관측 전용 경계.</summary>
public static class BattleMetricProjector
{
    private static readonly CombatBeatType[] TriggerEffectTypes =
    {
        CombatBeatType.BattleStartEffect,
        CombatBeatType.OnKillEffect,
        CombatBeatType.HpThresholdEffect,
        CombatBeatType.AllyDeathEffect,
    };

    public static BattleMetricRecord Project(
        string runId,
        string campaignId,
        string battleId,
        string replayGroupId,
        int replayIteration,
        string scenarioId,
        string policyId,
        BattleState finalState,
        BattleResult result,
        int maxSteps,
        float targetBattleSeconds = 35f,
        bool intentionalHardCounter = false)
    {
        var activity = result.ActivityTelemetry ?? finalState.ActivityTelemetry.BuildSnapshot(finalState);
        var canonicalHash = BattleStateCanonicalHash.Compute(finalState);
        var beats = (result.Beats ?? Array.Empty<CombatBeat>())
            .Where(beat => beat.Side == TeamSide.Ally)
            .OrderBy(beat => beat.StepIndex)
            .ThenBy(beat => beat.SequenceInStep)
            .ToArray();

        var synergy = CountBeats(beats, beat => beat.Type == CombatBeatType.SynergyActivated);
        var combo = CountBeats(beats, beat => beat.Type == CombatBeatType.ComboConsumed);
        var doctrine = CountBeats(
            beats,
            beat => TriggerEffectTypes.Contains(beat.Type)
                    && beat.Tag.StartsWith("rule.", StringComparison.Ordinal));
        var augment = CountBeats(
            beats,
            beat => TriggerEffectTypes.Contains(beat.Type)
                    && !beat.Tag.StartsWith("rule.", StringComparison.Ordinal));
        var firedRuleIds = synergy.Concat(combo).Concat(doctrine).Concat(augment)
            .Where(item => item.Count > 0)
            .Select(item => item.Id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var eligibleRuleIds = ResolveEligibleDepthRuleIds(finalState, firedRuleIds);

        var allyUnits = result.FinalUnits
            .Where(unit => unit.Side == TeamSide.Ally && unit.EntityKind == CombatEntityKind.RosterUnit)
            .ToArray();
        var enemyUnits = result.FinalUnits
            .Where(unit => unit.Side == TeamSide.Enemy && unit.EntityKind == CombatEntityKind.RosterUnit)
            .ToArray();
        var allyHp = allyUnits.Where(unit => unit.IsAlive).Sum(unit => Math.Max(0f, unit.CurrentHealth));
        var enemyHp = enemyUnits.Where(unit => unit.IsAlive).Sum(unit => Math.Max(0f, unit.CurrentHealth));
        var totalMaxHp = allyUnits.Concat(enemyUnits).Sum(unit => Math.Max(0f, unit.MaxHealth));
        var timeout = ResolveTimeout(result, maxSteps);
        var containsNonFinite = HasNonFinite(result, finalState);
        var illegalNegative = result.StepCount < 0
                              || result.DurationSeconds < 0f
                              || result.FinalUnits.Any(unit => unit.CurrentHealth < -0.0001f
                                                               || unit.MaxHealth < 0f
                                                               || unit.CurrentEnergy < -0.0001f
                                                               || unit.Barrier < -0.0001f);

        return new BattleMetricRecord
        {
            RunId = runId,
            CampaignId = campaignId,
            BattleId = battleId,
            ReplayGroupId = replayGroupId,
            ReplayIteration = replayIteration,
            ScenarioId = scenarioId,
            PolicyId = policyId,
            BuildFamilyId = ResolveBuildFamilyId(finalState, TeamSide.Ally),
            OpponentFamilyId = ResolveBuildFamilyId(finalState, TeamSide.Enemy),
            AllyFormationId = ResolveFormationId(finalState, TeamSide.Ally),
            EnemyFormationId = ResolveFormationId(finalState, TeamSide.Enemy),
            AllyBuildComponentCounts = ResolveBuildComponentCounts(finalState, TeamSide.Ally),
            EnemyBuildComponentCounts = ResolveBuildComponentCounts(finalState, TeamSide.Enemy),
            IntentionalHardCounter = intentionalHardCounter,
            Seed = finalState.Seed,
            FixedStepSeconds = FiniteOrZero(finalState.FixedStepSeconds),
            StepCount = Math.Max(0, result.StepCount),
            DurationSeconds = FiniteOrZero(result.DurationSeconds),
            WinnerSide = result.Winner.ToString().ToLowerInvariant(),
            Timeout = timeout,
            Stomp = !timeout && result.DurationSeconds < Math.Max(0f, targetBattleSeconds) * 0.35f,
            FirstDeathSide = ResolveFirstDeathSide(result.TelemetryEvents),
            AllySurvivingHp = FiniteOrZero(allyHp),
            EnemySurvivingHp = FiniteOrZero(enemyHp),
            FinalHpDifference = FiniteOrZero(allyHp - enemyHp),
            NormalizedFinalPowerDifference = totalMaxHp <= 0f ? 0f : FiniteOrZero((allyHp - enemyHp) / totalMaxHp),
            FlankStrikeCount = activity.FlankStrikeCount,
            RearStrikeCount = activity.RearStrikeCount,
            ScreenBlockCount = activity.ScreenAbsorbCount + activity.ScreenDeterrenceCount,
            ScreenAbsorbCount = activity.ScreenAbsorbCount,
            ScreenDeterrenceCount = activity.ScreenDeterrenceCount,
            SaveMomentCount = activity.SaveMomentCount,
            BacklineDiveKillCount = activity.BacklineDiveKillCount,
            SynergyRuleActivationCounts = synergy,
            ComboRuleActivationCounts = combo,
            AugmentRuleActivationCounts = augment,
            DoctrineRuleActivationCounts = doctrine,
            EligibleDepthRuleIds = eligibleRuleIds,
            FiredDepthRuleIds = firedRuleIds,
            CausalDepthRuleIds = Array.Empty<string>(),
            SalientEventCount = ResolveSalientEventCount(result.TelemetryEvents),
            CausalSalientEventCount = 0,
            ContainsNonFinite = containsNonFinite,
            IllegalNegativeState = illegalNegative,
            // BattleResolver returned, so a max-step timeout is a forced termination rather than a non-terminating state.
            NonTerminating = false,
            ReplayHash = ReplayHash.Compute(canonicalHash, activity.ReplayHash),
            CanonicalStateHash = canonicalHash,
            ActivityReplayHash = activity.ReplayHash,
        };
    }

    public static BattleMetricRecord ProjectFailure(
        string runId,
        string campaignId,
        string battleId,
        string replayGroupId,
        int replayIteration,
        string scenarioId,
        string policyId,
        int seed,
        string failureCode,
        bool crashed = true,
        bool softlocked = false)
    {
        return new BattleMetricRecord
        {
            RunId = runId,
            CampaignId = campaignId,
            BattleId = battleId,
            ReplayGroupId = replayGroupId,
            ReplayIteration = replayIteration,
            ScenarioId = scenarioId,
            PolicyId = policyId,
            Seed = seed,
            Crashed = crashed,
            Softlocked = softlocked,
            NonTerminating = softlocked,
            FailureCode = failureCode ?? string.Empty,
        };
    }

    private static IReadOnlyList<MetricCount> CountBeats(
        IEnumerable<CombatBeat> beats,
        Func<CombatBeat, bool> predicate)
    {
        return MetricCount.Normalize(
            beats.Where(predicate)
                .Where(beat => !string.IsNullOrWhiteSpace(beat.Tag))
                .GroupBy(beat => beat.Tag, StringComparer.Ordinal)
                .Select(group => new MetricCount(group.Key, group.Count())));
    }

    private static IReadOnlyList<string> ResolveEligibleDepthRuleIds(
        BattleState state,
        IEnumerable<string> firedRuleIds)
    {
        var ids = new HashSet<string>(firedRuleIds, StringComparer.Ordinal);
        foreach (var unit in state.GetTeam(TeamSide.Ally).Where(unit => unit.EntityKind == CombatEntityKind.RosterUnit))
        {
            foreach (var package in unit.Definition.TeamPackages ?? Array.Empty<CombatModifierPackage>())
            {
                if (IsSynergyPackage(package))
                {
                    ids.Add(package.SourceId);
                }

                if (!string.IsNullOrWhiteSpace(package.GrantedTeamRuleId))
                {
                    ids.Add(package.GrantedTeamRuleId);
                }
            }

            foreach (var effect in unit.Definition.EffectiveTriggeredEffects)
            {
                if (!string.IsNullOrWhiteSpace(effect.SourceId))
                {
                    ids.Add(effect.SourceId);
                }
            }
        }

        foreach (var telemetry in state.TelemetryEvents)
        {
            if (telemetry.EventKind == TelemetryEventKind.StatusApplied
                && CombatComboService.IsPrimerStatus(telemetry.StatusId))
            {
                ids.Add(telemetry.StatusId);
            }
        }

        return ids.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    private static string ResolveBuildFamilyId(BattleState state, TeamSide side)
    {
        var packages = state.GetTeam(side)
            .Where(unit => unit.EntityKind == CombatEntityKind.RosterUnit)
            .SelectMany(unit => unit.Definition.TeamPackages ?? Array.Empty<CombatModifierPackage>())
            .Where(IsSynergyPackage)
            .GroupBy(package => package.SourceId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(package => package.SourceId, StringComparer.Ordinal)
            .ToArray();
        var doctrineIds = packages
            .Select(package => package.GrantedTeamRuleId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        if (doctrineIds.Length > 0)
        {
            return $"doctrine:{string.Join("+", doctrineIds)}";
        }

        var packageIds = packages.Select(package => package.SourceId).ToArray();
        return packageIds.Length switch
        {
            0 => "non-doctrine:none",
            1 => $"non-doctrine:{packageIds[0]}",
            _ => $"hybrid:{string.Join("+", packageIds)}",
        };
    }

    private static string ResolveFormationId(BattleState state, TeamSide side)
    {
        return string.Join(
            "+",
            state.GetTeam(side)
                .Where(unit => unit.EntityKind == CombatEntityKind.RosterUnit)
                .OrderBy(unit => (int)unit.Anchor)
                .ThenBy(unit => unit.Definition.Id, StringComparer.Ordinal)
                .Select(unit => $"{(int)unit.Anchor:D2}:{unit.Definition.Id}"));
    }

    private static IReadOnlyList<MetricCount> ResolveBuildComponentCounts(BattleState state, TeamSide side)
    {
        var ids = state.GetTeam(side)
            .Where(unit => unit.EntityKind == CombatEntityKind.RosterUnit)
            .SelectMany(unit => (unit.Definition.Packages ?? Array.Empty<CombatModifierPackage>())
                .Concat(unit.Definition.TeamPackages ?? Array.Empty<CombatModifierPackage>())
                .Select(package => package.SourceId)
                .Concat((unit.Definition.RulePackages ?? Array.Empty<CombatRuleModifierPackage>()).Select(package => package.SourceId))
                .Concat((unit.Definition.TeamRulePackages ?? Array.Empty<CombatRuleModifierPackage>()).Select(package => package.SourceId))
                .Concat(unit.Definition.EffectiveTriggeredEffects.Select(effect => effect.SourceId)));
        return MetricCount.Normalize(
            ids.Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id, StringComparer.Ordinal)
                .Select(group => new MetricCount(group.Key, group.Count())));
    }

    private static bool IsSynergyPackage(CombatModifierPackage package)
    {
        return package.SourceId.StartsWith("synergy:", StringComparison.Ordinal)
               || package.SourceId.StartsWith("race:", StringComparison.Ordinal)
               || package.SourceId.StartsWith("class:", StringComparison.Ordinal)
               || !string.IsNullOrWhiteSpace(package.GrantedTeamRuleId);
    }

    private static bool ResolveTimeout(BattleResult result, int maxSteps)
    {
        var ended = (result.TelemetryEvents ?? Array.Empty<TelemetryEventRecord>())
            .LastOrDefault(record => record.EventKind == TelemetryEventKind.BattleEnded);
        return ended?.BoolValueA ?? result.StepCount >= maxSteps;
    }

    private static string ResolveFirstDeathSide(IReadOnlyList<TelemetryEventRecord>? telemetry)
    {
        var first = (telemetry ?? Array.Empty<TelemetryEventRecord>())
            .Where(record => record.EventKind == TelemetryEventKind.KillCredited && record.Target != null)
            .OrderBy(record => record.TimeSeconds)
            .ThenBy(record => record.Target!.UnitInstanceId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (first?.Target == null)
        {
            return "none";
        }

        return first.Target.SideIndex == (int)TeamSide.Ally ? "ally" : "enemy";
    }

    private static int ResolveSalientEventCount(IReadOnlyList<TelemetryEventRecord>? telemetry)
    {
        return (telemetry ?? Array.Empty<TelemetryEventRecord>())
            .Count(record => record.EventKind == TelemetryEventKind.CinematicMomentDetected
                             || record.Explain != null
                             && (int)record.Explain.Salience >= (int)SalienceClass.Major);
    }

    private static bool HasNonFinite(BattleResult result, BattleState state)
    {
        return !IsFinite(result.DurationSeconds)
               || !IsFinite(state.FixedStepSeconds)
               || result.FinalUnits.Any(unit => !IsFinite(unit.CurrentHealth)
                                                || !IsFinite(unit.MaxHealth)
                                                || !IsFinite(unit.CurrentEnergy)
                                                || !IsFinite(unit.MaxEnergy)
                                                || !IsFinite(unit.Barrier)
                                                || !IsFinite(unit.Position.X)
                                                || !IsFinite(unit.Position.Y))
               || state.AllUnits.Any(unit => !IsFinite(unit.CurrentHealth)
                                             || !IsFinite(unit.MaxHealth)
                                             || !IsFinite(unit.CurrentEnergy)
                                             || !IsFinite(unit.MaxEnergy)
                                             || !IsFinite(unit.Barrier)
                                             || unit.Stats.BaseValues.Values.Any(value => !IsFinite(value))
                                             || unit.Stats.Modifiers.Any(modifier => !IsFinite(modifier.Value))
                                             || unit.Statuses.Any(status => !IsFinite(status.Magnitude)));
    }

    private static float FiniteOrZero(float value) => IsFinite(value) ? value : 0f;

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
