using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Content.Definitions;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace SM.Tests.EditMode;

public sealed class LoopCContentGovernanceTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(LoopCContentGovernanceTests));
    }

    [Test]
    public void AllContentHasBudgetCardTests()
    {
        var missing = new List<string>();
        CollectBudgetCardMissing<UnitArchetypeDefinition>(missing, asset => asset.BudgetCard, asset => asset.Id);
        CollectBudgetCardMissing<SkillDefinitionAsset>(missing, asset => asset.BudgetCard, asset => asset.Id);
        CollectBudgetCardMissing<AffixDefinition>(missing, asset => asset.BudgetCard, asset => asset.Id);
        CollectBudgetCardMissing<AugmentDefinition>(missing, asset => asset.BudgetCard, asset => asset.Id);
        CollectBudgetCardMissing<StatusFamilyDefinition>(missing, asset => asset.BudgetCard, asset => asset.Id);

        foreach (var tier in LoadAssets<SynergyTierDefinition>())
        {
            if (tier.BudgetCard == null || tier.BudgetCard.Vector == null)
            {
                missing.Add($"{tier.name}:{tier.Id}");
            }
        }

        Assert.That(missing, Is.Empty, string.Join(Environment.NewLine, missing));
    }

    [Test]
    public void BudgetWindowValidationTests()
    {
        AssertLoopCCodesMissing(
            "loop_c.budget_card_missing",
            "loop_c.budget_domain_mismatch",
            "loop_c.budget_target_missing",
            "loop_c.budget_window",
            "loop_c.derived_delta");
    }

    [Test]
    public void BudgetIdentityValidationTests()
    {
        AssertLoopCCodesMissing(
            "loop_c.role_profile_missing",
            "loop_c.vector_missing",
            "loop_c.unit_economy_forbidden",
            "loop_c.drawback_credit_cap",
            "loop_c.role_primary_share",
            "loop_c.role_primary_secondary_share",
            "loop_c.role_top_two",
            "loop_c.soup_profile");
    }

    [Test]
    public void RarityComplexityValidationTests()
    {
        AssertLoopCCodesMissing(
            "loop_c.rarity_missing",
            "loop_c.keyword_cap",
            "loop_c.condition_cap",
            "loop_c.rule_exception_cap",
            "loop_c.common_affix_counter_forbidden",
            "loop_c.affix_counter_cap",
            "loop_c.affix_threat_cap",
            "loop_c.local_definition_counter_cap",
            "loop_c.augment_counter_cap",
            "loop_c.common_unit_counter_cap",
            "loop_c.rare_unit_counter_cap",
            "loop_c.epic_unit_counter_cap",
            "loop_c.rare_second_counter_strength");
    }

    [Test]
    public void CounterTopologyValidationTests()
    {
        AssertLoopCCodesMissing(
            "loop_c.counter_budget_missing",
            "loop_c.counter_declaration_missing",
            "loop_c.strong_counter_budget",
            "loop_c.light_counter_budget",
            "loop_c.local_threat_cap");
    }

    [Test]
    public void SynergyStructureValidationTests()
    {
        AssertLoopCCodesMissing(
            "loop_c.synergy_thresholds",
            "loop_c.synergy_threshold_value",
            "loop_c.synergy_power_band",
            "loop_c.synergy_economy_forbidden");
    }

    [Test]
    public void ForbiddenFeaturePolicyTests()
    {
        AssertLoopCCodesMissing(
            "loop_c.forbidden_flags",
            "loop_c.forbidden_heuristic");
    }

    [Test]
    public void CounterCoverageReportAggregationTests()
    {
        var report = CounterCoverageAggregationService.AggregateSummaries(new ContentGovernanceSummary?[]
        {
            new("Common", "Standard", "Support", 100, Array.Empty<string>(), new[] { new CompiledCounterToolContribution("TenacityStability", 1) }, "None"),
            new("Common", "Standard", "Support", 100, Array.Empty<string>(), new[] { new CompiledCounterToolContribution("TenacityStability", 1) }, "None"),
            new("Rare", "Standard", "Ranger", 120, Array.Empty<string>(), new[] { new CompiledCounterToolContribution("TrackingArea", 2) }, "None"),
            new("Epic", "Standard", "Arcanist", 140, Array.Empty<string>(), new[] { new CompiledCounterToolContribution("Exposure", 3) }, "None"),
        });

        Assert.That(report.TenacityStability, Is.EqualTo(CounterCoverageLevelValue.Standard));
        Assert.That(report.TrackingArea, Is.EqualTo(CounterCoverageLevelValue.Standard));
        Assert.That(report.Exposure, Is.EqualTo(CounterCoverageLevelValue.Strong));
        Assert.That(report.CleaveWaveclear, Is.EqualTo(CounterCoverageLevelValue.None));
    }

    [Test]
    public void RecruitTierContentRarityMappingTests()
    {
        Assert.That(LoopCContentGovernance.FromRecruitTier(SM.Core.Contracts.RecruitTier.Common), Is.EqualTo(ContentRarity.Common));
        Assert.That(LoopCContentGovernance.FromRecruitTier(SM.Core.Contracts.RecruitTier.Rare), Is.EqualTo(ContentRarity.Rare));
        Assert.That(LoopCContentGovernance.FromRecruitTier(SM.Core.Contracts.RecruitTier.Epic), Is.EqualTo(ContentRarity.Epic));

        Assert.That(LoopCContentGovernance.ToRecruitTier(ContentRarity.Common), Is.EqualTo(SM.Core.Contracts.RecruitTier.Common));
        Assert.That(LoopCContentGovernance.ToRecruitTier(ContentRarity.Rare), Is.EqualTo(SM.Core.Contracts.RecruitTier.Rare));
        Assert.That(LoopCContentGovernance.ToRecruitTier(ContentRarity.Epic), Is.EqualTo(SM.Core.Contracts.RecruitTier.Epic));
    }

    private static void CollectBudgetCardMissing<T>(
        ICollection<string> missing,
        Func<T, BudgetCard?> selector,
        Func<T, string> idSelector)
        where T : ScriptableObject
    {
        foreach (var asset in LoadAssets<T>())
        {
            var card = selector(asset);
            if (card == null || card.Vector == null)
            {
                missing.Add($"{typeof(T).Name}:{idSelector(asset)}");
            }
        }
    }

    private static IReadOnlyList<T> LoadAssets<T>() where T : ScriptableObject
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/Resources/_Game/Content/Definitions" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToList()!;
    }

    private static void AssertLoopCCodesMissing(params string[] codes)
    {
        var report = ContentDefinitionValidator.ValidateAndWriteReport();
        var violations = report.Issues
            .Where(issue => issue.Severity == ContentValidationSeverity.Error && codes.Contains(issue.Code, StringComparer.Ordinal))
            .Select(issue => $"{issue.Code} :: {issue.AssetPath}")
            .ToList();

        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
    }
}
