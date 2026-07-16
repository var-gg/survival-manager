using System;
using System.IO;
using System.Linq;
using System.Text;
using SM.Editor.SeedData;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>같은 campaign seed/profile identity에서 SearchPlanner와 Greedy의 실제 결과 방향을 비교한다.</summary>
public static class H100PolicyWitnessRunner
{
    private const string GateSpecRelativePath = "Assets/_Game/Scripts/Runtime/HeadlessMetrics/h100-gates-v1.json";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static void RunFromCli()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100PolicyWitnessRunner));
        var settings = H100MetricsRunSettings.FromEnvironment();
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var outputDirectory = ResolveOutputDirectory(projectRoot, settings.OutputDirectory);
        var spec = H100GateSpec.LoadFromFile(Path.Combine(projectRoot, GateSpecRelativePath));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out _, out var contentError))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {contentError}");
        }

        var greedySettings = settings with { PolicyId = HeadlessPolicyFactory.GreedyId };
        var plannerSettings = settings with { PolicyId = HeadlessPolicyFactory.SearchPlannerId };
        var greedy = H100CampaignCorpusRunner.Run(lookup, greedySettings, spec.TargetBattleSeconds);
        var planner = H100CampaignCorpusRunner.Run(lookup, plannerSettings, spec.TargetBattleSeconds);

        var greedyCompletionRate = Rate(greedy.Campaigns.Count(record => record.Completed), greedy.Campaigns.Count);
        var plannerCompletionRate = Rate(planner.Campaigns.Count(record => record.Completed), planner.Campaigns.Count);
        var greedyBattleWinRate = Rate(greedy.Battles.Count(record => record.WinnerSide == "ally"), greedy.Battles.Count);
        var plannerBattleWinRate = Rate(planner.Battles.Count(record => record.WinnerSide == "ally"), planner.Battles.Count);
        var completionImproved = plannerCompletionRate > greedyCompletionRate;
        var battleWinRateImproved = plannerBattleWinRate > greedyBattleWinRate;
        var improved = completionImproved || battleWinRateImproved;
        var result = new PolicyWitnessResult
        {
            SchemaVersion = "h100-policy-witness-v1",
            CampaignCount = settings.CampaignCount,
            SeedBase = settings.SeedBase,
            CampaignSiteSafety = settings.CampaignSiteSafety,
            GreedyCompletionRate = greedyCompletionRate,
            PlannerCompletionRate = plannerCompletionRate,
            GreedyBattleWinRate = greedyBattleWinRate,
            PlannerBattleWinRate = plannerBattleWinRate,
            CompletionImproved = completionImproved,
            BattleWinRateImproved = battleWinRateImproved,
            Improved = improved,
        };

        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "policy-witness.json");
        File.WriteAllText(outputPath, HeadlessMetricJson.Serialize(result) + "\n", Utf8WithoutBom);
        Debug.Log(
            $"[H100PolicyWitness] campaigns={settings.CampaignCount} "
            + $"completion greedy={greedyCompletionRate:F4} planner={plannerCompletionRate:F4} "
            + $"battleWin greedy={greedyBattleWinRate:F4} planner={plannerBattleWinRate:F4} "
            + $"improved={improved} output={outputPath}");
        if (!improved)
        {
            throw new InvalidOperationException(
                "SearchPlanner did not exceed Greedy completion or battle win rate on the paired smoke seed set.");
        }
    }

    private static double Rate(int numerator, int denominator)
        => denominator == 0 ? 0d : (double)numerator / denominator;

    private static string ResolveOutputDirectory(string projectRoot, string requested)
    {
        var candidate = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"H100 policy witness output must stay inside project root: {candidate}");
        }

        return candidate;
    }

    private sealed class PolicyWitnessResult
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public int CampaignCount { get; set; }
        public int SeedBase { get; set; }
        public int CampaignSiteSafety { get; set; }
        public double GreedyCompletionRate { get; set; }
        public double PlannerCompletionRate { get; set; }
        public double GreedyBattleWinRate { get; set; }
        public double PlannerBattleWinRate { get; set; }
        public bool CompletionImproved { get; set; }
        public bool BattleWinRateImproved { get; set; }
        public bool Improved { get; set; }
    }
}
