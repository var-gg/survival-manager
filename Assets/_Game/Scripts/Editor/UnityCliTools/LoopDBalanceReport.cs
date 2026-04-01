using System;
using System.IO;
using Newtonsoft.Json.Linq;
using SM.Editor.Validation;
using UnityCliConnector;

namespace SM.Editor.UnityCliTools;

[UnityCliTool(
    Name = "loop_d_balance_report",
    Description = "Runs shardable Loop D slice/balance workloads without routing through the Unity Test Runner.",
    Group = "report")]
public static class LoopDBalanceReport
{
    public static object HandleCommand(JObject parameters)
    {
        var toolParams = new ToolParams(parameters);
        var mode = (toolParams.Get("mode", "smoke") ?? "smoke").Trim().ToLowerInvariant();
        var smokeMode = ParseBool(toolParams.Get("smoke", mode == "smoke" ? "true" : "false"), defaultValue: mode == "smoke");
        var failOnError = ParseBool(
            toolParams.Get("fail_on_error", toolParams.Get("failOnError", "false")),
            defaultValue: false);

        return mode switch
        {
            "slice" => BuildSliceResponse(),
            "purekit" => BuildSuiteResponse("purekit", smokeMode, failOnError, FirstPlayableBalanceRunner.RunPureKitAndWriteArtifacts(smokeMode), "purekit_report.json"),
            "systemic" => BuildSuiteResponse("systemic", smokeMode, failOnError, FirstPlayableBalanceRunner.RunSystemicSliceAndWriteArtifacts(smokeMode), "systemic_slice_report.json"),
            "runlite" => BuildRunLiteResponse(smokeMode, failOnError, FirstPlayableBalanceRunner.RunRunLiteAndWriteArtifacts(smokeMode)),
            "smoke" => BuildFullResponse(true, failOnError, FirstPlayableBalanceRunner.RunAndWriteReport(smokeMode: true)),
            "full" => BuildFullResponse(false, failOnError, FirstPlayableBalanceRunner.RunAndWriteReport(smokeMode: smokeMode)),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode must be one of: slice, purekit, systemic, runlite, smoke, full"),
        };
    }

    private static object BuildSliceResponse()
    {
        var result = FirstPlayableBalanceRunner.GenerateSliceArtifacts();
        return new SuccessResponse(
            "Loop D slice artifact generated.",
            new
            {
                mode = "slice",
                assetPath = FirstPlayableSliceGenerator.AssetPath,
                markdownPath = result.MarkdownPath,
                reportDirectory = Path.GetDirectoryName(result.MarkdownPath) ?? string.Empty,
                counts = new
                {
                    units = result.Slice.UnitBlueprintIds.Count,
                    signatureActives = result.Slice.SignatureActiveIds.Count,
                    signaturePassives = result.Slice.SignaturePassiveIds.Count,
                    flexActives = result.Slice.FlexActiveIds.Count,
                    flexPassives = result.Slice.FlexPassiveIds.Count,
                    affixes = result.Slice.AffixIds.Count,
                    synergies = result.Slice.SynergyFamilyIds.Count,
                    augments = result.Slice.AugmentIds.Count,
                    parkingLot = result.Slice.ParkingLotContentIds.Count,
                },
            });
    }

    private static object BuildSuiteResponse(
        string mode,
        bool smokeMode,
        bool failOnError,
        FirstPlayableBalanceRunner.LoopDSuiteReport report,
        string fileName)
    {
        if (failOnError && !report.Passed)
        {
            throw new InvalidOperationException($"Loop D {mode} failed: {string.Join(", ", report.Failures)}");
        }

        return new SuccessResponse(
            $"Loop D {mode} report generated.",
            new
            {
                mode,
                smokeMode,
                passed = report.Passed,
                failures = report.Failures,
                scenarioCount = report.ScenarioCount,
                seedCount = report.SeedCount,
                reportPath = Path.Combine(FirstPlayableBalanceRunner.EnsureReportDirectory(), fileName),
            });
    }

    private static object BuildRunLiteResponse(
        bool smokeMode,
        bool failOnError,
        FirstPlayableBalanceRunner.LoopDRunLiteReport report)
    {
        if (failOnError && !report.Passed)
        {
            throw new InvalidOperationException($"Loop D runlite failed: {string.Join(", ", report.Failures)}");
        }

        return new SuccessResponse(
            "Loop D runlite report generated.",
            new
            {
                mode = "runlite",
                smokeMode,
                passed = report.Passed,
                failures = report.Failures,
                reportPath = Path.Combine(FirstPlayableBalanceRunner.EnsureReportDirectory(), "runlite_report.json"),
                summary = report.Summary,
            });
    }

    private static object BuildFullResponse(
        bool smokeMode,
        bool failOnError,
        FirstPlayableBalanceRunner.FirstPlayableBalanceRunResult report)
    {
        if (failOnError && report.Failures.Count > 0)
        {
            throw new InvalidOperationException($"Loop D full run failed: {string.Join(", ", report.Failures)}");
        }

        return new SuccessResponse(
            "Loop D full report generated.",
            new
            {
                mode = smokeMode ? "smoke" : "full",
                smokeMode,
                passed = report.Failures.Count == 0,
                failures = report.Failures,
                reportDirectory = report.ReportDirectory,
                artifacts = new
                {
                    pureKit = Path.Combine(report.ReportDirectory, "purekit_report.json"),
                    systemic = Path.Combine(report.ReportDirectory, "systemic_slice_report.json"),
                    runLite = Path.Combine(report.ReportDirectory, "runlite_report.json"),
                    contentHealth = Path.Combine(report.ReportDirectory, "content_health_cards.csv"),
                    pruneLedger = Path.Combine(report.ReportDirectory, "prune_ledger_v1.json"),
                    readabilityWatchlist = Path.Combine(report.ReportDirectory, "readability_watchlist.json"),
                    closureNote = Path.Combine(report.ReportDirectory, "loop_d_closure_note.txt"),
                },
            });
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" => true,
            "true" => true,
            "yes" => true,
            "on" => true,
            "0" => false,
            "false" => false,
            "no" => false,
            "off" => false,
            _ => defaultValue,
        };
    }
}
