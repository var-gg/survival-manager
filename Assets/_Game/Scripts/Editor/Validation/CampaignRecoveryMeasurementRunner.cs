using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.HeadlessPolicies;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>Runs the paired stuck-player measurement through the production session path.</summary>
public static class CampaignRecoveryMeasurementCli
{
    private const string DefaultOutputPath = "Logs/campaign-recovery-measurement.json";

    public static void RunFromCli()
    {
        try
        {
            var cellCount = ReadInt("SM_CAMPAIGN_RECOVERY_CELLS", 48, 1, 480);
            var attemptCap = ReadInt("SM_CAMPAIGN_RECOVERY_ATTEMPT_CAP", 10, 1, 10);
            var outputPath = Environment.GetEnvironmentVariable("SM_CAMPAIGN_RECOVERY_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = DefaultOutputPath;
            }

            var report = CampaignTwoArmSweepRunner.RunRecoveryMeasurement(cellCount, attemptCap);
            var absolutePath = Path.IsPathRooted(outputPath)
                ? Path.GetFullPath(outputPath)
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllText(
                absolutePath,
                JsonConvert.SerializeObject(report, Formatting.Indented, SerializerSettings()));
            Debug.Log(
                $"[CampaignRecovery] cells={cellCount} cap={attemptCap} "
                + $"hash={report.CanonicalHash} report={absolutePath}");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[CampaignRecovery] failed: {exception}");
            EditorApplication.Exit(1);
        }
    }

    private static int ReadInt(string name, int fallback, int minimum, int maximum)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || value < minimum
            || value > maximum)
        {
            throw new InvalidOperationException($"{name} must be in [{minimum}, {maximum}], got '{raw}'.");
        }

        return value;
    }

    private static JsonSerializerSettings SerializerSettings()
        => new()
        {
            Culture = CultureInfo.InvariantCulture,
            NullValueHandling = NullValueHandling.Include,
        };
}

internal static partial class CampaignTwoArmSweepRunner
{
    private const string RecoverySchemaVersion = "campaign-recovery-measurement-v1";

    private static readonly IReadOnlyList<CampaignRecoveryTarget> RecoveryTargets = new[]
    {
        new CampaignRecoveryTarget(
            "site_glass_forest_boss_1",
            "site_glass_forest",
            "late_boss_near_50",
            0.5000000000000000d),
        new CampaignRecoveryTarget(
            "site_heartforge_gate_boss_1",
            "site_heartforge_gate",
            "late_boss_near_50",
            0.5000000000000000d),
        new CampaignRecoveryTarget(
            "site_worldscar_depths_boss_1",
            "site_worldscar_depths",
            "final_boss_58_3",
            0.5833333333333334d),
    };

    internal static CampaignRecoveryMeasurementReport RunRecoveryMeasurement(int cellCount, int attemptCap)
    {
        var config = CampaignBalanceSweepConfig.Default;
        config.Validate();
        SM.Editor.SeedData.SampleSeedGenerator.RequireCanonicalSampleContentReady(
            nameof(CampaignRecoveryMeasurementCli));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var content, out var contentError))
        {
            throw new InvalidOperationException($"campaign recovery content unavailable: {contentError}");
        }

        var itemIndex = CampaignBalanceSweepRunner.BuildItemMetaIndex(content);
        var order = CampaignContentOrderIndex.Build(content);
        var informedArm = config.Arms.Single(arm => string.Equals(
            arm.ArmId,
            "informed",
            StringComparison.Ordinal));
        var cells = SampleRecoveryCells(config.BuildGrid(), cellCount);
        var pairsByTarget = RecoveryTargets.ToDictionary(
            target => target.NodeId,
            _ => new List<CampaignRecoveryPairObservation>(cells.Count),
            StringComparer.Ordinal);
        var defeatRewardsDriven = false;
        var runTerminationDriven = false;
        var townDecisionsDriven = false;
        foreach (var cell in cells)
        {
            var arrivals = CaptureRecoveryArrivals(
                lookup,
                itemIndex,
                order,
                config,
                informedArm,
                cell);
            foreach (var target in RecoveryTargets)
            {
                var arrival = arrivals[target.NodeId];
                var pair = RunRecoveryPair(lookup, cell, arrival, attemptCap);
                if (!pair.ArmA.Attempts[0].TargetReached
                    || !pair.ArmB.Attempts[0].TargetReached
                    || pair.ArmA.Attempts[0].TargetWon != pair.ArmB.Attempts[0].TargetWon
                    || pair.ArmA.Attempts[0].TerminalBattleSeed != pair.ArmB.Attempts[0].TerminalBattleSeed)
                {
                    throw new InvalidOperationException(
                        $"unpaired first attempt: target={target.NodeId} cell={cell.CellId}");
                }

                CampaignRecoverySeedInvariant.RequireStable(pair, target.NodeId);

                if (pair.ArmB.Attempts
                        .SelectMany(attempt => attempt.Settlements)
                        .Any(settlement => !string.IsNullOrWhiteSpace(settlement.ChoiceKind)
                                           || settlement.RecruitDecisionApplied != 0
                                           || settlement.PassiveDecisionApplied != 0
                                           || settlement.RefitDecisionApplied != 0
                                           || settlement.PrepEquipmentAssignments != 0))
                {
                    throw new InvalidOperationException(
                        $"control arm applied recovery: target={target.NodeId} cell={cell.CellId}");
                }

                defeatRewardsDriven |= pair.ArmA.Attempts
                    .SelectMany(attempt => attempt.Settlements)
                    .Any(settlement => !settlement.Victory && !string.IsNullOrWhiteSpace(settlement.ChoiceKind));
                runTerminationDriven |= pair.ArmA.Attempts
                    .SelectMany(attempt => attempt.Settlements)
                    .Any(settlement => !settlement.Victory && settlement.RunTerminated);
                townDecisionsDriven |= pair.ArmA.Attempts
                    .SelectMany(attempt => attempt.Settlements)
                    .Any(settlement => settlement.TownDecisionsDriven);
                pairsByTarget[target.NodeId].Add(pair);
            }
        }

        var nodes = RecoveryTargets.Select(target =>
            new CampaignRecoveryNodeObservation(
                target.NodeId,
                target.SiteId,
                target.Band,
                target.CanonicalFirstAttemptWinRate,
                pairsByTarget[target.NodeId]))
            .ToArray();

        var reentry = ProbeClearedSiteReentry(lookup, itemIndex, config);
        var unreachableParts = new List<string>();
        if (!defeatRewardsDriven)
        {
            unreachableParts.Add("defeat_rewards");
        }

        if (!runTerminationDriven)
        {
            unreachableParts.Add("run_termination");
        }

        if (!townDecisionsDriven)
        {
            unreachableParts.Add("town_decisions");
        }

        var payload = new CampaignRecoveryMeasurementReport(
            RecoverySchemaVersion,
            cells.Count,
            attemptCap,
            HeadlessPolicyFactory.PreviewGroundedConceptId,
            nodes,
            reentry,
            new CampaignRecoveryReachabilityObservation(
                defeatRewardsDriven,
                runTerminationDriven,
                townDecisionsDriven,
                unreachableParts),
            string.Empty);
        var canonicalJson = JsonConvert.SerializeObject(payload, Formatting.None, new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture,
            NullValueHandling = NullValueHandling.Include,
        });
        return payload with { CanonicalHash = Sha256Hex(canonicalJson) };
    }

    private static IReadOnlyDictionary<string, CampaignRecoveryArrival> CaptureRecoveryArrivals(
        RuntimeCombatContentLookup lookup,
        IReadOnlyDictionary<string, CampaignBalanceSweepRunner.ItemMeta> itemIndex,
        CampaignContentOrderIndex order,
        CampaignBalanceSweepConfig config,
        CampaignBalanceArmSpec informedArm,
        CampaignBalanceGridCell cell)
    {
        var accumulator = new CampaignTwoArmSweepAccumulator(config);
        var profileSnapshots = new Dictionary<string, string>(StringComparer.Ordinal);
        RunCell(
            lookup,
            itemIndex,
            order,
            config,
            informedArm,
            cell,
            accumulator,
            stopAfterEncounterId: string.Empty,
            battleRunner: (state, _) => BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps),
            setupObserver: (session, _, _, encounter) =>
            {
                var target = RecoveryTargets.FirstOrDefault(value => string.Equals(
                    value.NodeId,
                    encounter.Context.EncounterId,
                    StringComparison.Ordinal));
                if (target != null)
                {
                    profileSnapshots[target.NodeId] = H100ProfileSnapshotCodec.Capture(session.Profile);
                }
            });

        foreach (var target in RecoveryTargets)
        {
            if (!profileSnapshots.ContainsKey(target.NodeId))
            {
                throw new InvalidOperationException(
                    $"recovery arrival was not captured: target={target.NodeId} cell={cell.CellId}");
            }
        }

        return RecoveryTargets.ToDictionary(
            target => target.NodeId,
            target => new CampaignRecoveryArrival(
                profileSnapshots[target.NodeId],
                cell.CellId,
                target),
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<CampaignBalanceGridCell> SampleRecoveryCells(
        IReadOnlyList<CampaignBalanceGridCell> grid,
        int sampleCount)
        => Enumerable.Range(0, sampleCount)
            .Select(index => grid[(int)Math.Floor(index * grid.Count / (double)sampleCount)])
            .ToArray();

    private static string Sha256Hex(string value)
    {
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value))
            .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
    }
}
