using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using SM.Meta.Model;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Tests.EditMode;

public sealed class LoopDTelemetryAndBalanceTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(LoopDTelemetryAndBalanceTests));
    }

    [Test]
    public void TelemetryEventSerialization_RoundTripsExplainStamp()
    {
        var record = new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = TelemetryEventKind.DamageApplied,
            TimeSeconds = 1.25f,
            Actor = new TelemetryEntityRef
            {
                UnitInstanceId = "ally_1",
                UnitBlueprintId = "warden",
                SideIndex = 0,
            },
            Target = new TelemetryEntityRef
            {
                UnitInstanceId = "enemy_1",
                UnitBlueprintId = "raider",
                SideIndex = 1,
            },
            Explain = new ExplainStamp
            {
                SourceKind = ExplainedSourceKind.SignatureActive,
                SourceContentId = "skill_shield_slam",
                SourceDisplayName = "Shield Slam",
                ReasonCode = DecisionReasonCode.BreakGuard,
                Salience = SalienceClass.Major,
            },
            SkillId = "skill_shield_slam",
            ValueA = 18f,
        };

        var json = JsonUtility.ToJson(record, prettyPrint: true);
        var roundTrip = JsonUtility.FromJson<TelemetryEventRecord>(json);

        Assert.That(roundTrip, Is.Not.Null);
        Assert.That(roundTrip!.EventKind, Is.EqualTo(TelemetryEventKind.DamageApplied));
        Assert.That(roundTrip.Explain?.SourceContentId, Is.EqualTo("skill_shield_slam"));
        Assert.That(roundTrip.Explain?.SourceDisplayName, Is.EqualTo("Shield Slam"));
        Assert.That(roundTrip.Actor?.UnitBlueprintId, Is.EqualTo("warden"));
    }

    [Test]
    public void ExplainStampCompletenessValidator_FlagsMissingImpactExplain()
    {
        var records = new[]
        {
            new TelemetryEventRecord
            {
                EventKind = TelemetryEventKind.DamageApplied,
                TimeSeconds = 0.5f,
                Actor = new TelemetryEntityRef { UnitBlueprintId = "warden" },
                Target = new TelemetryEntityRef { UnitBlueprintId = "raider" },
            },
            new TelemetryEventRecord
            {
                EventKind = TelemetryEventKind.HealingApplied,
                TimeSeconds = 0.6f,
                Explain = new ExplainStamp
                {
                    SourceKind = ExplainedSourceKind.SignatureActive,
                    SourceContentId = "skill_guarding_light",
                    SourceDisplayName = "Guarding Light",
                    ReasonCode = DecisionReasonCode.PeelAlly,
                    Salience = SalienceClass.Major,
                },
            },
        };

        var issues = TelemetryExplainValidator.CollectMissingExplainStampIssues(records);

        Assert.That(issues, Has.Count.EqualTo(1));
        Assert.That(issues[0], Does.StartWith("DamageApplied@"));
    }

    [Test]
    public void ReadabilityReport_FlagsUnexplainedDamageAndTargetThrash()
    {
        var records = new[]
        {
            new TelemetryEventRecord
            {
                EventKind = TelemetryEventKind.DamageApplied,
                TimeSeconds = 0.2f,
                Actor = new TelemetryEntityRef { UnitInstanceId = "ally_1", UnitBlueprintId = "warden" },
                Target = new TelemetryEntityRef { UnitInstanceId = "enemy_1", UnitBlueprintId = "raider" },
                ValueA = 12f,
            },
            new TelemetryEventRecord
            {
                EventKind = TelemetryEventKind.TargetSwitched,
                TimeSeconds = 1f,
                Actor = new TelemetryEntityRef { UnitInstanceId = "ally_1", UnitBlueprintId = "warden" },
                Explain = new ExplainStamp
                {
                    SourceKind = ExplainedSourceKind.SystemRule,
                    SourceContentId = "warden",
                    SourceDisplayName = "Warden",
                    ReasonCode = DecisionReasonCode.DefaultCadence,
                    Salience = SalienceClass.Minor,
                },
            },
            new TelemetryEventRecord
            {
                EventKind = TelemetryEventKind.TargetSwitched,
                TimeSeconds = 2f,
                Actor = new TelemetryEntityRef { UnitInstanceId = "ally_1", UnitBlueprintId = "warden" },
                Explain = new ExplainStamp
                {
                    SourceKind = ExplainedSourceKind.SystemRule,
                    SourceContentId = "warden",
                    SourceDisplayName = "Warden",
                    ReasonCode = DecisionReasonCode.DefaultCadence,
                    Salience = SalienceClass.Minor,
                },
            },
            new TelemetryEventRecord
            {
                EventKind = TelemetryEventKind.TargetSwitched,
                TimeSeconds = 3f,
                Actor = new TelemetryEntityRef { UnitInstanceId = "ally_1", UnitBlueprintId = "warden" },
                Explain = new ExplainStamp
                {
                    SourceKind = ExplainedSourceKind.SystemRule,
                    SourceContentId = "warden",
                    SourceDisplayName = "Warden",
                    ReasonCode = DecisionReasonCode.DefaultCadence,
                    Salience = SalienceClass.Minor,
                },
            },
            new TelemetryEventRecord
            {
                EventKind = TelemetryEventKind.TargetSwitched,
                TimeSeconds = 4f,
                Actor = new TelemetryEntityRef { UnitInstanceId = "ally_1", UnitBlueprintId = "warden" },
                Explain = new ExplainStamp
                {
                    SourceKind = ExplainedSourceKind.SystemRule,
                    SourceContentId = "warden",
                    SourceDisplayName = "Warden",
                    ReasonCode = DecisionReasonCode.DefaultCadence,
                    Salience = SalienceClass.Minor,
                },
            },
            new TelemetryEventRecord
            {
                EventKind = TelemetryEventKind.TargetSwitched,
                TimeSeconds = 5f,
                Actor = new TelemetryEntityRef { UnitInstanceId = "ally_1", UnitBlueprintId = "warden" },
                Explain = new ExplainStamp
                {
                    SourceKind = ExplainedSourceKind.SystemRule,
                    SourceContentId = "warden",
                    SourceDisplayName = "Warden",
                    ReasonCode = DecisionReasonCode.DefaultCadence,
                    Salience = SalienceClass.Minor,
                },
            },
            new TelemetryEventRecord
            {
                EventKind = TelemetryEventKind.TargetSwitched,
                TimeSeconds = 6f,
                Actor = new TelemetryEntityRef { UnitInstanceId = "ally_1", UnitBlueprintId = "warden" },
                Explain = new ExplainStamp
                {
                    SourceKind = ExplainedSourceKind.SystemRule,
                    SourceContentId = "warden",
                    SourceDisplayName = "Warden",
                    ReasonCode = DecisionReasonCode.DefaultCadence,
                    Salience = SalienceClass.Minor,
                },
            },
            new TelemetryEventRecord
            {
                EventKind = TelemetryEventKind.TargetSwitched,
                TimeSeconds = 7f,
                Actor = new TelemetryEntityRef { UnitInstanceId = "ally_1", UnitBlueprintId = "warden" },
                Explain = new ExplainStamp
                {
                    SourceKind = ExplainedSourceKind.SystemRule,
                    SourceContentId = "warden",
                    SourceDisplayName = "Warden",
                    ReasonCode = DecisionReasonCode.DefaultCadence,
                    Salience = SalienceClass.Minor,
                },
            },
        };

        var report = BattleTelemetryAnalysisService.BuildReadabilityReport(records, combatantCount: 6);

        Assert.That(report.UnexplainedDamageRatio, Is.GreaterThan(0.9f));
        Assert.That(report.Violations, Contains.Item(ReadabilityViolationKind.UnexplainedDamage));
        Assert.That(report.Violations, Contains.Item(ReadabilityViolationKind.TargetThrash));
    }

    [Test]
    public void FirstPlayableSliceGenerator_BuildsSliceWithinCapsAndWritesArtifact()
    {
        var result = FirstPlayableSliceGenerator.GenerateAndWriteArtifacts();

        Assert.That(result.Slice.UnitBlueprintIds.Count, Is.LessThanOrEqualTo(result.Slice.UnitBlueprintCap));
        Assert.That(result.Slice.SignatureActiveIds.Count, Is.LessThanOrEqualTo(result.Slice.SignatureActiveCap));
        Assert.That(result.Slice.SignaturePassiveIds.Count, Is.LessThanOrEqualTo(result.Slice.SignaturePassiveCap));
        Assert.That(result.Slice.FlexActiveIds.Count, Is.LessThanOrEqualTo(result.Slice.FlexActiveCap));
        Assert.That(result.Slice.FlexPassiveIds.Count, Is.LessThanOrEqualTo(result.Slice.FlexPassiveCap));
        Assert.That(result.Slice.AffixIds.Count, Is.LessThanOrEqualTo(result.Slice.AffixCap));
        Assert.That(result.Slice.SynergyFamilyIds.Count, Is.LessThanOrEqualTo(result.Slice.SynergyFamilyCap));
        Assert.That(result.Slice.AugmentIds.Count, Is.LessThanOrEqualTo(result.Slice.AugmentCap));
        Assert.That(result.Slice.CoverageQuotas.Select(quota => quota.Kind), Contains.Item(SliceCoverageQuotaKind.AntiControlSource));
        Assert.That(File.Exists(result.MarkdownPath), Is.True);
        Assert.That(result.Asset, Is.Not.Null);
    }

    [Test]
    [Explicit("Long-running Loop D artifact smoke. Use tools/unity-bridge.ps1 loopd-smoke or a targeted unity-cli filter when intentionally verifying the runner.")]
    [Category("ManualLoopD")]
    [Timeout(180000)]
    public void FirstPlayableBalanceRunner_ManualSmokeWritesArtifacts()
    {
        var report = FirstPlayableBalanceRunner.RunAndWriteReport(smokeMode: true);

        Assert.That(Directory.Exists(report.ReportDirectory), Is.True);
        Assert.That(File.Exists(Path.Combine(report.ReportDirectory, "purekit_report.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(report.ReportDirectory, "systemic_slice_report.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(report.ReportDirectory, "runlite_report.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(report.ReportDirectory, "content_health_cards.csv")), Is.True);
        Assert.That(File.Exists(Path.Combine(report.ReportDirectory, "prune_ledger_v1.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(report.ReportDirectory, "readability_watchlist.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(report.ReportDirectory, "loop_d_closure_note.txt")), Is.True);
    }

    [Test]
    public void IdentitySimilarityHeuristic_StaysHigherForNearDuplicateFingerprints()
    {
        var left = new ContentIdentityFingerprint
        {
            PrimaryTagA = "human",
            PrimaryTagB = "vanguard",
            RoleProfileId = "Vanguard",
            ActivationModelId = "unit",
            RangeDisciplineId = "HoldBand",
            UsesBarrier = true,
            ThreatPatterns = new[] { "ArmorFrontline" },
            CounterTools = new[] { "ArmorShred" },
        };
        var nearDuplicate = new ContentIdentityFingerprint
        {
            PrimaryTagA = "human",
            PrimaryTagB = "vanguard",
            RoleProfileId = "Vanguard",
            ActivationModelId = "unit",
            RangeDisciplineId = "HoldBand",
            UsesBarrier = true,
            ThreatPatterns = new[] { "ArmorFrontline" },
            CounterTools = new[] { "ArmorShred" },
        };
        var distinct = new ContentIdentityFingerprint
        {
            PrimaryTagA = "beastkin",
            PrimaryTagB = "mystic",
            RoleProfileId = "Arcanist",
            ActivationModelId = "spell",
            RangeDisciplineId = "KiteBackward",
            UsesSummon = true,
            ThreatPatterns = new[] { "SwarmFlood" },
            CounterTools = new[] { "CleaveWaveclear" },
        };

        var duplicateScore = LoopDContentHealthAnalysisService.CalculateIdentitySimilarity(left, nearDuplicate);
        var distinctScore = LoopDContentHealthAnalysisService.CalculateIdentitySimilarity(left, distinct);

        Assert.That(duplicateScore, Is.GreaterThanOrEqualTo(0.9f));
        Assert.That(distinctScore, Is.LessThan(duplicateScore));
    }

    [Test]
    public void PruneRuleEvaluation_PrefersSimplifyForReadabilityDebt()
    {
        var card = new ContentHealthCard
        {
            ContentId = "test.content",
            ContentKind = ContentKind.UnitBlueprint,
            PickRate = 0.4f,
            PresenceWinDelta = 0.08f,
            UnexplainedEffectShare = 0.12f,
            ContributionToSalienceOverload = 0.15f,
            Debt = new ContentDebtVector
            {
                PowerDebt = 4,
                ReadabilityDebt = 10,
                RedundancyDebt = 2,
                VarianceDebt = 1,
                TopologyDebt = 1,
                EconomyDebt = 0,
            },
            Reasons = new[] { PruneReason.ReadabilityDebt },
        };
        card.Grade = LoopDContentHealthAnalysisService.ResolveGrade(card);

        var disposition = LoopDContentHealthAnalysisService.ResolveDisposition(card);

        Assert.That(card.Grade, Is.EqualTo(ContentHealthGrade.Watch));
        Assert.That(disposition, Is.EqualTo(PruneDisposition.SimplifyReadability));
    }
}
