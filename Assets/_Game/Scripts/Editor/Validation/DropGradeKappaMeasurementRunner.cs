using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using SM.Combat.Model;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

public static class DropGradeKappaMeasurementCli
{
    private const string DefaultOutputPath = "Logs/drop-grade-kappa-measurement.json";

    public static void RunFromCli()
    {
        try
        {
            var outputPath = Environment.GetEnvironmentVariable("SM_DROP_GRADE_KAPPA_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = DefaultOutputPath;
            }

            var report = DropGradeKappaMeasurementRunner.Run();
            var absolutePath = Path.IsPathRooted(outputPath)
                ? Path.GetFullPath(outputPath)
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllText(
                absolutePath,
                JsonConvert.SerializeObject(report, Formatting.Indented, new JsonSerializerSettings
                {
                    Culture = CultureInfo.InvariantCulture,
                    NullValueHandling = NullValueHandling.Include,
                }),
                new UTF8Encoding(false));
            Debug.Log(
                $"[DropGradeKappa] measured={report.MeasuredKappa:F6} "
                + $"assumed={report.AssumedKappa:F6} gap={report.Gap:F6} "
                + $"observations={report.Observations.Count} report={absolutePath}");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[DropGradeKappa] failed: {exception}");
            EditorApplication.Exit(1);
        }
    }
}

internal static class DropGradeKappaMeasurementRunner
{
    private const double AssumedKappa = 0.16551443847757333d;
    private const float TargetIncrementBudget = 8f;
    private const int CandidateRotationCount = 16;
    private static readonly IReadOnlyList<StatKey> PowerKeys =
        new[] { StatKey.MaxHealth, StatKey.PhysPower, StatKey.MagPower };
    private static readonly string[] SlotTypes = { "Weapon", "Armor", "Accessory" };

    internal static DropGradeKappaMeasurementReport Run()
    {
        SM.Editor.SeedData.SampleSeedGenerator.RequireCanonicalSampleContentReady(
            nameof(DropGradeKappaMeasurementCli));
        var scenarios = BalanceSweepScenarioFactory.BuildSmokeScenarios();
        if (scenarios.Count == 0)
        {
            throw new InvalidOperationException("No canonical smoke scenario is available for kappa measurement.");
        }

        var observations = new List<DropGradeKappaObservation>();
        foreach (var scenario in scenarios)
        {
            var candidatesBySlot = SlotTypes.ToDictionary(
                slot => slot,
                slot => SelectIncrementCandidates(scenario.Content, slot),
                StringComparer.Ordinal);
            for (var rotation = 0; rotation < CandidateRotationCount; rotation++)
            {
                var selected = SlotTypes
                    .Select(slot => candidatesBySlot[slot][rotation % candidatesBySlot[slot].Count])
                    .ToArray();
                var upgraded = AddOneGradeStep(
                    scenario.PlayerSnapshot,
                    scenario.Content,
                    selected.Select(value => value.Id).ToArray());
                var baselinePower = MeasurePowerProxy(scenario.PlayerSnapshot);
                var upgradedPower = MeasurePowerProxy(upgraded);
                var kappa = Math.Log(upgradedPower.Health / baselinePower.Health)
                            + Math.Log(upgradedPower.Offense / baselinePower.Offense);
                if (!double.IsFinite(kappa))
                {
                    throw new InvalidOperationException(
                        $"Non-finite kappa for scenario '{scenario.ScenarioId}' rotation {rotation}.");
                }

                observations.Add(new DropGradeKappaObservation(
                    scenario.ScenarioId,
                    rotation,
                    selected.Select(value => value.Id).ToArray(),
                    selected.Average(value => (double)value.BudgetScore),
                    baselinePower.Health,
                    upgradedPower.Health,
                    baselinePower.Offense,
                    upgradedPower.Offense,
                    kappa));
            }
        }

        var measured = observations.Average(value => value.Kappa);
        var injected = CampaignPowerInjector.Apply(scenarios[0].PlayerSnapshot, measured);
        var injectionBaseline = MeasurePowerProxy(scenarios[0].PlayerSnapshot);
        var injectionResult = MeasurePowerProxy(injected);
        var recovered = Math.Log(injectionResult.Health / injectionBaseline.Health)
                        + Math.Log(injectionResult.Offense / injectionBaseline.Offense);
        return new DropGradeKappaMeasurementReport(
            "drop-grade-kappa-v1",
            "For every hero in each canonical smoke squad, add one live, non-conditional authored affix "
            + "near BudgetScore 8 to each equipped slot (Weapon, Armor, Accessory). Resolve MaxHealth, "
            + "PhysPower, and MagPower through HeroEffectiveStatPreview, then compute the log of the "
            + "squad-health ratio plus the log of the squad-offense ratio. CampaignPowerInjector is "
            + "applied at the measured mean as an equivalence check.",
            TargetIncrementBudget,
            CandidateRotationCount,
            measured,
            AssumedKappa,
            measured - AssumedKappa,
            recovered,
            Math.Abs(recovered - measured),
            observations);
    }

    private static IReadOnlyList<AffixTemplate> SelectIncrementCandidates(
        CombatContentSnapshot content,
        string slotType)
    {
        if (content.AffixCatalog == null)
        {
            throw new InvalidOperationException("Affix catalog is unavailable.");
        }

        var candidates = content.AffixCatalog.Values
            .Where(template => template.SpawnWeight > 0f
                               && template.ItemLevelMin < 999
                               && !template.IsConditional
                               && template.BudgetScore > 0f
                               && (template.AllowedSlotTypes == null
                                   || template.AllowedSlotTypes.Count == 0
                                   || template.AllowedSlotTypes.Contains(slotType, StringComparer.Ordinal))
                               && content.AffixPackages.TryGetValue(template.Id, out var package)
                               && package.Modifiers.Count > 0)
            .OrderBy(template => Math.Abs(template.BudgetScore - TargetIncrementBudget))
            .ThenBy(template => template.Id, StringComparer.Ordinal)
            .Take(CandidateRotationCount)
            .ToArray();
        if (candidates.Length == 0)
        {
            throw new InvalidOperationException($"No measurable grade-step affix exists for slot '{slotType}'.");
        }

        return candidates;
    }

    private static BattleLoadoutSnapshot AddOneGradeStep(
        BattleLoadoutSnapshot snapshot,
        CombatContentSnapshot content,
        IReadOnlyList<string> affixIds)
    {
        var packages = affixIds.Select(id => content.AffixPackages[id]).ToArray();
        return snapshot with
        {
            CompileHash = $"{snapshot.CompileHash}|grade:+1",
            Allies = snapshot.Allies.Select(unit => unit with
            {
                Packages = (unit.Packages ?? Array.Empty<CombatModifierPackage>())
                    .Concat(packages)
                    .ToArray(),
            }).ToArray(),
        };
    }

    private static (double Health, double Offense) MeasurePowerProxy(BattleLoadoutSnapshot snapshot)
    {
        var health = 0d;
        var offense = 0d;
        foreach (var ally in snapshot.Allies)
        {
            var stats = HeroEffectiveStatPreview.Resolve(ally, PowerKeys)
                .ToDictionary(value => value.Key, value => (double)value.EffectiveValue);
            health += Math.Max(0.000001d, stats.GetValueOrDefault(StatKey.MaxHealth));
            offense += Math.Max(
                0.000001d,
                stats.GetValueOrDefault(StatKey.PhysPower)
                + stats.GetValueOrDefault(StatKey.MagPower));
        }

        if (health <= 0d || offense <= 0d)
        {
            throw new InvalidOperationException(
                $"Power proxy requires positive squad health and offense, got {health:R}/{offense:R}.");
        }

        return (health, offense);
    }
}

internal sealed record DropGradeKappaMeasurementReport(
    string SchemaVersion,
    string Method,
    float TargetIncrementBudget,
    int CandidateRotationCount,
    double MeasuredKappa,
    double AssumedKappa,
    double Gap,
    double InjectorRecoveredKappa,
    double InjectorResidual,
    IReadOnlyList<DropGradeKappaObservation> Observations);

internal sealed record DropGradeKappaObservation(
    string ScenarioId,
    int Rotation,
    IReadOnlyList<string> AddedAffixIds,
    double MeanBudgetScore,
    double HealthBefore,
    double HealthAfter,
    double OffenseBefore,
    double OffenseAfter,
    double Kappa);
