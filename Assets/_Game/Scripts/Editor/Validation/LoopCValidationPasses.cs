using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

internal sealed record LoopCGovernanceSubject(
    string ContentId,
    string ContentKind,
    string AssetPath,
    string Scope,
    object Source,
    BudgetCard BudgetCard);

internal sealed record LoopCDerivedSanityPreview(int Score, IReadOnlyList<string> Warnings);

internal interface ILoopCGovernanceSubjectExtractor
{
    IReadOnlyList<LoopCGovernanceSubject> Extract(ValidationAssetCatalog catalog);
}

internal interface ILoopCGovernanceRule
{
    void Execute(LoopCGovernanceContext context);
}

internal sealed class LoopCValidationBuilder
{
    private readonly List<LoopCContentBudgetAuditEntry> _budgetAudit = new();
    private readonly List<LoopCCounterCoverageMatrixEntry> _counterCoverageMatrix = new();
    private readonly List<LoopCForbiddenFeatureEntry> _forbiddenFeatureEntries = new();

    internal ICollection<LoopCContentBudgetAuditEntry> BudgetAudit => _budgetAudit;
    internal ICollection<LoopCCounterCoverageMatrixEntry> CounterCoverageMatrix => _counterCoverageMatrix;
    internal ICollection<LoopCForbiddenFeatureEntry> ForbiddenFeatureEntries => _forbiddenFeatureEntries;

    internal LoopCValidationSummary BuildSummary()
    {
        return new LoopCValidationSummary
        {
            BudgetAudit = _budgetAudit
                .OrderBy(entry => entry.ContentKind, StringComparer.Ordinal)
                .ThenBy(entry => entry.ContentId, StringComparer.Ordinal)
                .ToList(),
            CounterCoverageMatrix = _counterCoverageMatrix
                .OrderBy(entry => entry.ContentKind, StringComparer.Ordinal)
                .ThenBy(entry => entry.ContentId, StringComparer.Ordinal)
                .ToList(),
            ForbiddenFeatureEntries = _forbiddenFeatureEntries
                .OrderBy(entry => entry.ContentKind, StringComparer.Ordinal)
                .ThenBy(entry => entry.ContentId, StringComparer.Ordinal)
                .ToList(),
        };
    }
}

internal sealed class LoopCGovernanceContext
{
    public LoopCGovernanceContext(
        ValidationAssetCatalog catalog,
        IReadOnlyList<LoopCGovernanceSubject> subjects,
        ICollection<ContentValidationIssue> issues,
        LoopCValidationBuilder builder)
    {
        Catalog = catalog;
        Subjects = subjects;
        Issues = issues;
        Builder = builder;
    }

    internal ValidationAssetCatalog Catalog { get; }
    internal IReadOnlyList<LoopCGovernanceSubject> Subjects { get; }
    internal ICollection<ContentValidationIssue> Issues { get; }
    internal LoopCValidationBuilder Builder { get; }
}

internal sealed class LoopCDerivedSanityPreviewCalculator
{
    internal LoopCDerivedSanityPreview Build(object source, BudgetCard budgetCard)
    {
        return LoopCDerivedSanityPreviewService.Build(source, budgetCard);
    }
}

internal sealed class DefaultLoopCGovernanceSubjectExtractor : ILoopCGovernanceSubjectExtractor
{
    public IReadOnlyList<LoopCGovernanceSubject> Extract(ValidationAssetCatalog catalog)
    {
        var subjects = new List<LoopCGovernanceSubject>();
        var referencedSynergyTierIds = catalog.OfType<SynergyDefinition>()
            .SelectMany(definition => definition.Tiers ?? new List<SynergyTierDefinition>())
            .Where(tier => tier != null && !string.IsNullOrWhiteSpace(tier.Id))
            .Select(tier => tier.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var asset in catalog.Assets.Where(asset => asset != null))
        {
            var assetPath = catalog.GetPath(asset);
            switch (asset)
            {
                case UnitArchetypeDefinition archetype:
                    subjects.Add(new LoopCGovernanceSubject(archetype.Id, nameof(UnitArchetypeDefinition), assetPath, nameof(UnitArchetypeDefinition), archetype, archetype.BudgetCard));
                    if (HasGovernedNestedContent(archetype.Loadout?.SignaturePassive))
                    {
                        subjects.Add(new LoopCGovernanceSubject(
                            string.IsNullOrWhiteSpace(archetype.Loadout.SignaturePassive.Id) ? $"{archetype.Id}:signature_passive" : archetype.Loadout.SignaturePassive.Id,
                            nameof(PassiveDefinition),
                            assetPath,
                            $"{nameof(UnitArchetypeDefinition)}.{nameof(archetype.Loadout.SignaturePassive)}",
                            archetype.Loadout.SignaturePassive,
                            archetype.Loadout.SignaturePassive.BudgetCard));
                    }

                    if (HasGovernedNestedContent(archetype.Loadout?.FlexPassive))
                    {
                        subjects.Add(new LoopCGovernanceSubject(
                            string.IsNullOrWhiteSpace(archetype.Loadout.FlexPassive.Id) ? $"{archetype.Id}:flex_passive" : archetype.Loadout.FlexPassive.Id,
                            nameof(PassiveDefinition),
                            assetPath,
                            $"{nameof(UnitArchetypeDefinition)}.{nameof(archetype.Loadout.FlexPassive)}",
                            archetype.Loadout.FlexPassive,
                            archetype.Loadout.FlexPassive.BudgetCard));
                    }

                    if (HasGovernedNestedContent(archetype.Loadout?.MobilityReaction))
                    {
                        subjects.Add(new LoopCGovernanceSubject(
                            string.IsNullOrWhiteSpace(archetype.Loadout.MobilityReaction.Id) ? $"{archetype.Id}:mobility" : archetype.Loadout.MobilityReaction.Id,
                            nameof(MobilityDefinition),
                            assetPath,
                            $"{nameof(UnitArchetypeDefinition)}.{nameof(archetype.Loadout.MobilityReaction)}",
                            archetype.Loadout.MobilityReaction,
                            archetype.Loadout.MobilityReaction.BudgetCard));
                    }

                    break;
                case SkillDefinitionAsset skill:
                    subjects.Add(new LoopCGovernanceSubject(skill.Id, nameof(SkillDefinitionAsset), assetPath, nameof(SkillDefinitionAsset), skill, skill.BudgetCard));
                    break;
                case AffixDefinition affix:
                    subjects.Add(new LoopCGovernanceSubject(affix.Id, nameof(AffixDefinition), assetPath, nameof(AffixDefinition), affix, affix.BudgetCard));
                    break;
                case AugmentDefinition augment:
                    subjects.Add(new LoopCGovernanceSubject(augment.Id, nameof(AugmentDefinition), assetPath, nameof(AugmentDefinition), augment, augment.BudgetCard));
                    break;
                case SynergyTierDefinition tier when referencedSynergyTierIds.Contains(tier.Id):
                    subjects.Add(new LoopCGovernanceSubject(tier.Id, nameof(SynergyTierDefinition), assetPath, nameof(SynergyTierDefinition), tier, tier.BudgetCard));
                    break;
                case StatusFamilyDefinition status:
                    subjects.Add(new LoopCGovernanceSubject(status.Id, nameof(StatusFamilyDefinition), assetPath, nameof(StatusFamilyDefinition), status, status.BudgetCard));
                    break;
            }
        }

        return subjects;
    }

    private static bool HasGovernedNestedContent(PassiveDefinition? definition)
    {
        if (definition == null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(definition.Id)
               || !string.IsNullOrWhiteSpace(definition.NameKey)
               || !string.IsNullOrWhiteSpace(definition.DescriptionKey)
               || !string.IsNullOrWhiteSpace(definition.EffectFamilyId)
               || !string.IsNullOrWhiteSpace(definition.MutuallyExclusiveGroupId)
               || (definition.Effects?.Count ?? 0) > 0
               || (definition.BudgetCard?.Vector?.PositiveTotal ?? 0) > 0
               || (definition.BudgetCard?.Vector?.DrawbackCredit ?? 0) > 0
               || (definition.BudgetCard?.DeclaredThreatPatterns?.Length ?? 0) > 0
               || (definition.BudgetCard?.DeclaredCounterTools?.Length ?? 0) > 0;
    }

    private static bool HasGovernedNestedContent(MobilityDefinition? definition)
    {
        if (definition == null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(definition.Id)
               || !string.IsNullOrWhiteSpace(definition.NameKey)
               || !string.IsNullOrWhiteSpace(definition.DescriptionKey)
               || definition.Profile != null
               || (definition.Effects?.Count ?? 0) > 0
               || (definition.BudgetCard?.Vector?.PositiveTotal ?? 0) > 0
               || (definition.BudgetCard?.Vector?.DrawbackCredit ?? 0) > 0
               || (definition.BudgetCard?.DeclaredThreatPatterns?.Length ?? 0) > 0
               || (definition.BudgetCard?.DeclaredCounterTools?.Length ?? 0) > 0;
    }
}

internal sealed class BudgetWindowGovernanceRule : ILoopCGovernanceRule
{
    public void Execute(LoopCGovernanceContext context)
    {
        BudgetWindowValidationPass.Run(context.Subjects, context.Issues, context.Builder.BudgetAudit);
    }
}

internal sealed class BudgetIdentityGovernanceRule : ILoopCGovernanceRule
{
    public void Execute(LoopCGovernanceContext context)
    {
        BudgetIdentityValidationPass.Run(context.Subjects, context.Issues);
    }
}

internal sealed class RarityComplexityGovernanceRule : ILoopCGovernanceRule
{
    public void Execute(LoopCGovernanceContext context)
    {
        RarityComplexityValidationPass.Run(context.Subjects, context.Issues);
    }
}

internal sealed class CounterTopologyGovernanceRule : ILoopCGovernanceRule
{
    public void Execute(LoopCGovernanceContext context)
    {
        CounterTopologyValidationPass.Run(context.Subjects, context.Issues, context.Builder.CounterCoverageMatrix);
    }
}

internal sealed class SynergyStructureGovernanceRule : ILoopCGovernanceRule
{
    public void Execute(LoopCGovernanceContext context)
    {
        SynergyStructureValidationPass.Run(context.Catalog, context.Subjects, context.Issues);
    }
}

internal sealed class ForbiddenFeatureGovernanceRule : ILoopCGovernanceRule
{
    public void Execute(LoopCGovernanceContext context)
    {
        V1ForbiddenFeatureValidationPass.Run(context.Subjects, context.Issues, context.Builder.ForbiddenFeatureEntries);
    }
}

internal sealed class LoopCGovernanceOrchestrator
{
    private readonly ILoopCGovernanceSubjectExtractor _subjectExtractor;
    private readonly IReadOnlyList<ILoopCGovernanceRule> _rules;

    public LoopCGovernanceOrchestrator(
        ILoopCGovernanceSubjectExtractor subjectExtractor,
        IReadOnlyList<ILoopCGovernanceRule> rules)
    {
        _subjectExtractor = subjectExtractor;
        _rules = rules;
    }

    internal IReadOnlyList<LoopCGovernanceSubject> ExtractSubjects(ValidationAssetCatalog catalog)
    {
        return _subjectExtractor.Extract(catalog);
    }

    internal LoopCValidationSummary Validate(ValidationAssetCatalog catalog, ICollection<ContentValidationIssue> issues)
    {
        var context = new LoopCGovernanceContext(catalog, _subjectExtractor.Extract(catalog), issues, new LoopCValidationBuilder());
        foreach (var rule in _rules)
        {
            rule.Execute(context);
        }

        return context.Builder.BuildSummary();
    }

    internal static LoopCGovernanceOrchestrator CreateDefault()
    {
        return new LoopCGovernanceOrchestrator(
            new DefaultLoopCGovernanceSubjectExtractor(),
            new ILoopCGovernanceRule[]
            {
                new BudgetWindowGovernanceRule(),
                new BudgetIdentityGovernanceRule(),
                new RarityComplexityGovernanceRule(),
                new CounterTopologyGovernanceRule(),
                new SynergyStructureGovernanceRule(),
                new ForbiddenFeatureGovernanceRule(),
            });
    }
}

internal static class LoopCContentGovernanceValidator
{
    internal static LoopCValidationSummary Validate(ValidationAssetCatalog catalog, ICollection<ContentValidationIssue> issues)
    {
        return LoopCGovernanceOrchestrator.CreateDefault().Validate(catalog, issues);
    }

    internal static IReadOnlyList<LoopCGovernanceSubject> GetSubjects(ValidationAssetCatalog catalog)
    {
        return LoopCGovernanceOrchestrator.CreateDefault().ExtractSubjects(catalog);
    }

    internal static ContentValidationIssue CreateIssue(ContentValidationSeverity severity, string code, string message, LoopCGovernanceSubject subject)
    {
        return new ContentValidationIssue(severity, code, message, subject.AssetPath, subject.Scope);
    }

    internal static string FormatThreats(BudgetCard budgetCard)
    {
        return string.Join(", ", budgetCard.DeclaredThreatPatterns?.Select(pattern => pattern.ToString()) ?? Array.Empty<string>());
    }

    internal static string FormatCounters(BudgetCard budgetCard)
    {
        return string.Join(", ", budgetCard.DeclaredCounterTools?.Select(tool => $"{tool.Tool}:{tool.Strength}") ?? Array.Empty<string>());
    }

    internal static string FormatFeatureFlags(BudgetCard budgetCard)
    {
        return budgetCard.DeclaredFeatureFlags == ContentFeatureFlag.None ? "None" : budgetCard.DeclaredFeatureFlags.ToString();
    }
}

internal static class BudgetWindowValidationPass
{
    public static void Run(
        IReadOnlyList<LoopCGovernanceSubject> subjects,
        ICollection<ContentValidationIssue> issues,
        ICollection<LoopCContentBudgetAuditEntry> budgetAudit)
    {
        foreach (var subject in subjects)
        {
            var budgetCard = subject.BudgetCard;
            if (budgetCard == null || budgetCard.Vector == null)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.budget_card_missing", "BudgetCard is required for Loop C governed content.", subject));
                continue;
            }

            if (!DomainMatchesSource(subject.Source, budgetCard.Domain))
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.budget_domain_mismatch", $"Budget domain '{budgetCard.Domain}' does not match {subject.ContentKind}.", subject));
            }

            if (!TryResolveBudgetTarget(subject.Source, budgetCard, out var authoredTarget, out var tolerance))
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.budget_target_missing", "Budget target could not be resolved from rarity/power band metadata.", subject));
                continue;
            }

            var authoredScore = budgetCard.Vector.FinalScore;
            if (Math.Abs(authoredScore - authoredTarget) > tolerance)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(
                    ContentValidationSeverity.Error,
                    "loop_c.budget_window",
                    $"Budget final score {authoredScore} is outside target {authoredTarget} ± {tolerance}.",
                    subject));
            }

            var preview = LoopCDerivedSanityPreviewService.Build(subject.Source, budgetCard);
            foreach (var warning in preview.Warnings)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Warning, "loop_c.derived_preview_partial", warning, subject));
            }

            var delta = Math.Abs(authoredScore - preview.Score);
            if (delta > LoopCContentGovernance.GetDerivedDeltaThreshold(budgetCard.Domain))
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(
                    ContentValidationSeverity.Error,
                    "loop_c.derived_delta",
                    $"Derived sanity preview drift is {delta}. Allowed threshold is {LoopCContentGovernance.GetDerivedDeltaThreshold(budgetCard.Domain)}.",
                    subject));
            }

            budgetAudit.Add(new LoopCContentBudgetAuditEntry(
                subject.ContentId,
                subject.ContentKind,
                subject.AssetPath,
                budgetCard.Domain.ToString(),
                budgetCard.Rarity.ToString(),
                budgetCard.PowerBand.ToString(),
                budgetCard.RoleProfile.ToString(),
                authoredScore,
                preview.Score,
                delta,
                LoopCContentGovernanceValidator.FormatThreats(budgetCard),
                LoopCContentGovernanceValidator.FormatCounters(budgetCard),
                LoopCContentGovernanceValidator.FormatFeatureFlags(budgetCard)));
        }
    }

    private static bool DomainMatchesSource(object source, BudgetDomain domain)
    {
        return source switch
        {
            UnitArchetypeDefinition => domain == BudgetDomain.UnitBlueprint,
            SkillDefinitionAsset => domain == BudgetDomain.Skill,
            PassiveDefinition => domain == BudgetDomain.Passive,
            MobilityDefinition => domain == BudgetDomain.Mobility,
            AffixDefinition => domain == BudgetDomain.Affix,
            AugmentDefinition => domain == BudgetDomain.Augment,
            SynergyTierDefinition => domain == BudgetDomain.SynergyBreakpoint,
            StatusFamilyDefinition => domain == BudgetDomain.Status,
            _ => false,
        };
    }

    private static bool TryResolveBudgetTarget(object source, BudgetCard budgetCard, out int target, out int tolerance)
    {
        target = 0;
        tolerance = 0;
        switch (source)
        {
            case UnitArchetypeDefinition:
                if (!LoopCContentGovernance.UnitBudgetTargets.TryGetValue(budgetCard.Rarity, out var unitTarget))
                {
                    return false;
                }

                target = unitTarget.Target;
                tolerance = unitTarget.Tolerance;
                return true;
            case AffixDefinition:
                if (!LoopCContentGovernance.AffixBudgetTargets.TryGetValue(budgetCard.Rarity, out var affixTarget))
                {
                    return false;
                }

                target = affixTarget.Target;
                tolerance = affixTarget.Tolerance;
                return true;
            case AugmentDefinition:
                if (!LoopCContentGovernance.AugmentBudgetTargets.TryGetValue(budgetCard.Rarity, out var augmentTarget))
                {
                    return false;
                }

                target = augmentTarget.Target;
                tolerance = augmentTarget.Tolerance;
                return true;
            case SynergyTierDefinition tier:
                var band = tier.Threshold == 2 ? PowerBand.Standard : PowerBand.Major;
                var bandTarget = LoopCContentGovernance.PowerBandTargets[band];
                target = bandTarget.Target;
                tolerance = bandTarget.Tolerance;
                return true;
            default:
                if (!LoopCContentGovernance.PowerBandTargets.TryGetValue(budgetCard.PowerBand, out var genericTarget))
                {
                    return false;
                }

                target = genericTarget.Target;
                tolerance = genericTarget.Tolerance;
                return true;
        }
    }
}

internal static class BudgetIdentityValidationPass
{
    public static void Run(IReadOnlyList<LoopCGovernanceSubject> subjects, ICollection<ContentValidationIssue> issues)
    {
        foreach (var subject in subjects.Where(subject => subject.Source is UnitArchetypeDefinition))
        {
            var budgetCard = subject.BudgetCard;
            if (budgetCard.RoleProfile == CombatRoleBudgetProfile.None)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.role_profile_missing", "Unit budget card must declare one CombatRoleBudgetProfile.", subject));
                continue;
            }

            var vector = budgetCard.Vector;
            if (vector == null)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.vector_missing", "Unit budget vector is missing.", subject));
                continue;
            }

            if (vector.Economy != 0)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.unit_economy_forbidden", "Unit blueprints may not allocate Economy budget.", subject));
            }

            if (vector.DrawbackCredit > 8)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.drawback_credit_cap", "DrawbackCredit may not exceed 8.", subject));
            }

            var positiveTotal = Math.Max(1, vector.PositiveTotal);
            var roleProfile = LoopCContentGovernance.RoleProfiles[budgetCard.RoleProfile];
            var primaryTotal = roleProfile.PrimaryAxes.Sum(axis => vector.GetAxisValue(axis));
            var primaryAndSecondaryTotal = primaryTotal + vector.GetAxisValue(roleProfile.SecondaryAxis);
            if (primaryTotal < Math.Ceiling(positiveTotal * 0.35f))
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.role_primary_share", "Primary budget axes must account for at least 35% of positive budget.", subject));
            }

            if (primaryAndSecondaryTotal < Math.Ceiling(positiveTotal * 0.50f))
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.role_primary_secondary_share", "Primary + secondary axes must account for at least 50% of positive budget.", subject));
            }

            var topTwoAxes = vector.EnumeratePositiveAxes()
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key)
                .Take(2)
                .Select(pair => pair.Key)
                .ToList();
            if (topTwoAxes.Count > 0 && !topTwoAxes.Any(axis => roleProfile.PrimaryAxes.Contains(axis)))
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.role_top_two", "At least one of the top two axes must be a primary role axis.", subject));
            }

            var soupAxes = new[]
                {
                    BudgetAxis.SustainedDamage,
                    BudgetAxis.BurstDamage,
                    BudgetAxis.Durability,
                    BudgetAxis.Control,
                    BudgetAxis.Mobility,
                    BudgetAxis.Support,
                }
                .Count(axis => vector.GetAxisValue(axis) >= 10);
            if (soupAxes >= 5)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.soup_profile", "Unit budget profile is over-distributed across too many major axes.", subject));
            }
        }
    }
}

internal static class RarityComplexityValidationPass
{
    public static void Run(IReadOnlyList<LoopCGovernanceSubject> subjects, ICollection<ContentValidationIssue> issues)
    {
        foreach (var subject in subjects)
        {
            var budgetCard = subject.BudgetCard;
            if (!LoopCContentGovernance.RarityComplexityCaps.TryGetValue(budgetCard.Rarity, out var caps))
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.rarity_missing", "ContentRarity is required on BudgetCard for Loop C governed content.", subject));
                continue;
            }

            if (budgetCard.KeywordCount > caps.KeywordCount)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.keyword_cap", $"keywordCount exceeds {caps.KeywordCount} for {budgetCard.Rarity}.", subject));
            }

            if (budgetCard.ConditionClauseCount > caps.ConditionClauseCount)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.condition_cap", $"conditionClauseCount exceeds {caps.ConditionClauseCount} for {budgetCard.Rarity}.", subject));
            }

            if (budgetCard.RuleExceptionCount > caps.RuleExceptionCount)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.rule_exception_cap", $"ruleExceptionCount exceeds {caps.RuleExceptionCount} for {budgetCard.Rarity}.", subject));
            }

            var declaredThreatCount = budgetCard.DeclaredThreatPatterns?.Length ?? 0;
            var declaredCounterCount = budgetCard.DeclaredCounterTools?.Length ?? 0;
            switch (subject.Source)
            {
                case UnitArchetypeDefinition:
                    ValidateUnitCaps(subject, issues, budgetCard.Rarity, declaredThreatCount, declaredCounterCount);
                    break;
                case AffixDefinition:
                    if (budgetCard.Rarity == ContentRarity.Common && declaredCounterCount > 0)
                    {
                        issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.common_affix_counter_forbidden", "Common affixes may not declare counter tools.", subject));
                    }
                    if (declaredCounterCount > 1)
                    {
                        issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.affix_counter_cap", "Affixes may declare at most one counter tool.", subject));
                    }
                    if (declaredThreatCount > 1)
                    {
                        issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.affix_threat_cap", "Affixes may declare at most one threat pattern.", subject));
                    }
                    break;
                case SkillDefinitionAsset:
                case PassiveDefinition:
                case MobilityDefinition:
                    if (declaredThreatCount > 1 || declaredCounterCount > 1)
                    {
                        issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.local_definition_counter_cap", "Skill/passive/mobility definitions may declare at most one threat and one counter.", subject));
                    }
                    break;
                case AugmentDefinition:
                    if (declaredThreatCount > 2 || declaredCounterCount > 2)
                    {
                        issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.augment_counter_cap", "Augments may declare at most two threats and two counters.", subject));
                    }
                    break;
            }
        }
    }

    private static void ValidateUnitCaps(LoopCGovernanceSubject subject, ICollection<ContentValidationIssue> issues, ContentRarity rarity, int threatCount, int counterCount)
    {
        switch (rarity)
        {
            case ContentRarity.Common:
                if (threatCount > 1 || counterCount > 1)
                {
                    issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.common_unit_counter_cap", "Common units may declare at most one threat and one counter.", subject));
                }
                break;
            case ContentRarity.Rare:
                if (threatCount > 1 || counterCount > 2)
                {
                    issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.rare_unit_counter_cap", "Rare units may declare at most one threat and two counters.", subject));
                }
                break;
            case ContentRarity.Epic:
                if (threatCount > 2 || counterCount > 2)
                {
                    issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.epic_unit_counter_cap", "Epic units may declare at most two threats and two counters.", subject));
                }
                break;
        }

        if (rarity == ContentRarity.Rare && subject.BudgetCard.DeclaredCounterTools?.Length > 1)
        {
            var secondCounter = subject.BudgetCard.DeclaredCounterTools[1];
            if (secondCounter.Strength != CounterCoverageStrength.Light)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.rare_second_counter_strength", "Rare unit second counter tool must be Light strength.", subject));
            }
        }
    }
}

internal static class CounterTopologyValidationPass
{
    public static void Run(
        IReadOnlyList<LoopCGovernanceSubject> subjects,
        ICollection<ContentValidationIssue> issues,
        ICollection<LoopCCounterCoverageMatrixEntry> counterMatrix)
    {
        foreach (var subject in subjects)
        {
            var budgetCard = subject.BudgetCard;
            var hasDeclaredCounters = budgetCard.DeclaredCounterTools is { Length: > 0 };
            var counterBudget = budgetCard.Vector?.CounterCoverage ?? 0;

            if (hasDeclaredCounters && counterBudget <= 0)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.counter_budget_missing", "Declared counter tools require positive CounterCoverage budget.", subject));
            }

            if (!hasDeclaredCounters && counterBudget > 0)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.counter_declaration_missing", "CounterCoverage budget requires declared counter tools.", subject));
            }

            foreach (var tool in budgetCard.DeclaredCounterTools ?? Array.Empty<CounterToolContribution>())
            {
                if (tool.Strength == CounterCoverageStrength.Strong && counterBudget < 4)
                {
                    issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.strong_counter_budget", "Strong counter coverage requires at least 4 CounterCoverage budget.", subject));
                }

                if (tool.Strength == CounterCoverageStrength.Light && counterBudget < 1)
                {
                    issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.light_counter_budget", "Light counter coverage requires at least 1 CounterCoverage budget.", subject));
                }
            }

            if (subject.Source is AffixDefinition or SkillDefinitionAsset or PassiveDefinition or MobilityDefinition)
            {
                if ((budgetCard.DeclaredThreatPatterns?.Length ?? 0) > 1)
                {
                    issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.local_threat_cap", "This content domain may declare at most one threat pattern.", subject));
                }
            }

            if ((budgetCard.DeclaredThreatPatterns?.Length ?? 0) > 0 || (budgetCard.DeclaredCounterTools?.Length ?? 0) > 0)
            {
                counterMatrix.Add(new LoopCCounterCoverageMatrixEntry(
                    subject.ContentId,
                    subject.ContentKind,
                    subject.AssetPath,
                    LoopCContentGovernanceValidator.FormatThreats(budgetCard),
                    LoopCContentGovernanceValidator.FormatCounters(budgetCard)));
            }
        }
    }
}

internal static class SynergyStructureValidationPass
{
    public static void Run(
        ValidationAssetCatalog catalog,
        IReadOnlyList<LoopCGovernanceSubject> subjects,
        ICollection<ContentValidationIssue> issues)
    {
        foreach (var synergy in catalog.OfType<SynergyDefinition>())
        {
            var assetPath = catalog.GetPath(synergy);
            var expectedThresholds = FirstPlayableAuthoringContract.GetExpectedSynergyThresholds(synergy)
                .OrderBy(value => value)
                .ToArray();
            var thresholds = synergy.Tiers
                .Where(tier => tier != null)
                .Select(tier => tier.Threshold)
                .OrderBy(value => value)
                .ToArray();
            if (thresholds.Length != expectedThresholds.Length
                || !thresholds.SequenceEqual(expectedThresholds))
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "loop_c.synergy_thresholds",
                    $"Synergy '{synergy.Id}' must use thresholds [{string.Join(", ", expectedThresholds)}].",
                    assetPath,
                    nameof(SynergyDefinition.Tiers)));
            }
        }

        foreach (var subject in subjects.Where(subject => subject.Source is SynergyTierDefinition))
        {
            var tier = (SynergyTierDefinition)subject.Source;
            if (!LoopCContentGovernance.AllowedSynergyThresholds.Contains(tier.Threshold))
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.synergy_threshold_value", $"Synergy threshold '{tier.Threshold}' is forbidden in V1.", subject));
            }

            var expectedBand = tier.Threshold == 2 ? PowerBand.Standard : PowerBand.Major;
            if (subject.BudgetCard.PowerBand != expectedBand)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.synergy_power_band", $"Synergy threshold {tier.Threshold} must use power band '{expectedBand}'.", subject));
            }

            if (subject.BudgetCard.Vector?.Economy > 0)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.synergy_economy_forbidden", "Synergy breakpoints may not allocate Economy budget.", subject));
            }
        }
    }
}

internal static class V1ForbiddenFeatureValidationPass
{
    public static void Run(
        IReadOnlyList<LoopCGovernanceSubject> subjects,
        ICollection<ContentValidationIssue> issues,
        ICollection<LoopCForbiddenFeatureEntry> forbiddenEntries)
    {
        foreach (var subject in subjects)
        {
            var budgetCard = subject.BudgetCard;
            var forbiddenFlags = budgetCard.DeclaredFeatureFlags & LoopCContentGovernance.DefaultFeaturePolicy.ForbiddenFlags;
            if (forbiddenFlags != ContentFeatureFlag.None)
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.forbidden_flags", $"Forbidden feature flags detected: {forbiddenFlags}.", subject));
                forbiddenEntries.Add(new LoopCForbiddenFeatureEntry(subject.ContentId, subject.ContentKind, subject.AssetPath, forbiddenFlags.ToString(), "Declared forbidden flag."));
            }

            foreach (var inferred in InferForbiddenFeatures(subject))
            {
                issues.Add(LoopCContentGovernanceValidator.CreateIssue(ContentValidationSeverity.Error, "loop_c.forbidden_heuristic", inferred.Reason, subject));
                forbiddenEntries.Add(new LoopCForbiddenFeatureEntry(subject.ContentId, subject.ContentKind, subject.AssetPath, inferred.Flag.ToString(), inferred.Reason));
            }
        }
    }

    private static IEnumerable<(ContentFeatureFlag Flag, string Reason)> InferForbiddenFeatures(LoopCGovernanceSubject subject)
    {
        switch (subject.Source)
        {
            case SkillDefinitionAsset skill when skill.DamageType == DamageTypeValue.True:
                yield return (ContentFeatureFlag.TrueDamage, "SkillDefinitionAsset uses DamageTypeValue.True.");
                break;
            case UnitArchetypeDefinition archetype when archetype.Loadout?.BasicAttack?.DamageType == DamageTypeValue.True:
                yield return (ContentFeatureFlag.TrueDamage, "BasicAttackDefinition uses DamageTypeValue.True.");
                break;
        }

        foreach (var effect in EnumerateEffects(subject.Source))
        {
            if (effect == null)
            {
                continue;
            }

            if ((effect.Capabilities & EffectCapability.GrantAction) != 0 || (effect.Capabilities & EffectCapability.ModifyCooldown) != 0)
            {
                yield return (ContentFeatureFlag.ExtraActionOrCooldownReset, "EffectDescriptor grants actions or modifies cooldowns.");
            }

            if ((effect.Capabilities & EffectCapability.ModifyEconomyRule) != 0 || (effect.Capabilities & EffectCapability.ModifyOfferWeights) != 0)
            {
                if (subject.BudgetCard.Domain != BudgetDomain.Augment)
                {
                    yield return (ContentFeatureFlag.NonAugmentEconomyOrOfferChange, "Non-augment content modifies economy or offer rules.");
                }
            }

            if (effect.LoadoutTopologyDelta != 0)
            {
                yield return (ContentFeatureFlag.LoadoutTopologyMutation, "EffectDescriptor mutates loadout topology.");
            }

            if (effect.AllowsPersistentSummonChain)
            {
                yield return (ContentFeatureFlag.SpawnChain, "EffectDescriptor allows persistent summon chaining.");
            }
        }
    }

    private static IEnumerable<EffectDescriptor> EnumerateEffects(object source)
    {
        switch (source)
        {
            case UnitArchetypeDefinition archetype:
                return archetype.Loadout?.BasicAttack?.Effects != null ? archetype.Loadout.BasicAttack.Effects : Array.Empty<EffectDescriptor>();
            case SkillDefinitionAsset skill:
                return skill.Effects;
            case PassiveDefinition passive:
                return passive.Effects;
            case MobilityDefinition mobility:
                return mobility.Effects;
            case AffixDefinition affix:
                return affix.Effects;
            case AugmentDefinition augment:
                return augment.Effects;
            case SynergyTierDefinition tier:
                return tier.Effects;
            case StatusFamilyDefinition status:
                return status.Effects;
            default:
                return Array.Empty<EffectDescriptor>();
        }
    }
}

internal static class LoopCDerivedSanityPreviewService
{
    public static LoopCDerivedSanityPreview Build(object source, BudgetCard budgetCard)
    {
        var warnings = new List<string>();
        var score = source switch
        {
            UnitArchetypeDefinition unit => BuildUnitScore(unit, budgetCard),
            SkillDefinitionAsset skill => BuildSkillScore(skill, budgetCard),
            PassiveDefinition passive => BuildPassiveScore(passive, budgetCard),
            MobilityDefinition mobility => BuildMobilityScore(mobility, budgetCard),
            AffixDefinition affix => BuildAffixScore(affix, budgetCard),
            AugmentDefinition augment => BuildAugmentScore(augment, budgetCard),
            SynergyTierDefinition tier => BuildSynergyScore(tier, budgetCard),
            StatusFamilyDefinition status => BuildStatusScore(status, budgetCard),
            _ => 0,
        };

        if (score <= 0)
        {
            warnings.Add("Derived sanity preview fell back to 0. Inspect budget authoring.");
        }

        return new LoopCDerivedSanityPreview(score, warnings);
    }

    private static int BuildUnitScore(UnitArchetypeDefinition unit, BudgetCard budgetCard)
    {
        var target = LoopCContentGovernance.UnitBudgetTargets.TryGetValue(budgetCard.Rarity, out var window)
            ? window.Target
            : 100;
        var statScore =
            unit.BaseMaxHealth * 1.8f +
            unit.BaseArmor * 4.5f +
            unit.BaseResist * 4.5f +
            unit.BaseBarrierPower * 2f +
            unit.BaseTenacity * 2.5f +
            Math.Max(unit.BasePhysPower, 0f) * 3f +
            Math.Max(unit.BaseMagPower, 0f) * 3f +
            Math.Max(unit.BaseHealPower, 0f) * 3f +
            unit.BaseAttackSpeed * 1.8f +
            unit.BaseMoveSpeed * 5f +
            unit.BaseAttackRange * 2f;
        var nestedScore = 0f;
        nestedScore += unit.Skills.Where(skill => skill != null).Sum(skill => BuildSkillScore(skill, skill.BudgetCard)) * 0.25f;
        nestedScore += unit.Loadout?.SignaturePassive != null ? BuildPassiveScore(unit.Loadout.SignaturePassive, unit.Loadout.SignaturePassive.BudgetCard) * 0.5f : 0f;
        nestedScore += unit.Loadout?.FlexPassive != null ? BuildPassiveScore(unit.Loadout.FlexPassive, unit.Loadout.FlexPassive.BudgetCard) * 0.5f : 0f;
        nestedScore += unit.Loadout?.MobilityReaction != null ? BuildMobilityScore(unit.Loadout.MobilityReaction, unit.Loadout.MobilityReaction.BudgetCard) * 0.5f : 0f;
        nestedScore += BuildCounterContributionScore(budgetCard) * 2f;
        var rarityConcentration = Mathf.Max(0f, target - 100f) * 0.3f;
        return Mathf.RoundToInt((statScore + nestedScore + target * 2f) / 3f + rarityConcentration);
    }

    private static int BuildSkillScore(SkillDefinitionAsset skill, BudgetCard budgetCard)
    {
        var target = ResolveGenericPowerBandTarget(budgetCard, PowerBand.Standard);
        var baseScore =
            Math.Abs(skill.PhysCoeff) * 4f +
            Math.Abs(skill.MagCoeff) * 4f +
            Math.Abs(skill.HealCoeff) * 4f +
            Math.Abs(skill.HealthCoeff) * 4f +
            Math.Abs(skill.PowerFlat) * 0.75f +
            Math.Abs(skill.Power) * 0.35f +
            skill.Range * 0.75f +
            skill.Radius * 1.5f +
            skill.Width +
            skill.AppliedStatuses.Count * 1.75f +
            BuildEffectDescriptorScore(skill.Effects, skill.SummonProfile) +
            BuildCounterContributionScore(budgetCard);
        var preview = Mathf.RoundToInt((baseScore + target * 1.5f) / 2.5f);
        var minimum = Mathf.Max(1, target - (target >= 12 ? 3 : 2));
        return Mathf.Max(preview, minimum);
    }

    private static int BuildPassiveScore(PassiveDefinition passive, BudgetCard budgetCard)
    {
        var target = ResolveGenericPowerBandTarget(budgetCard, PowerBand.Standard);
        var baseScore =
            BuildEffectDescriptorScore(passive.Effects, null) +
            (passive.AllowMirroredOwnedSummonKill ? 2f : 0f) +
            BuildCounterContributionScore(budgetCard);
        return Mathf.RoundToInt((baseScore + target) / 2f);
    }

    private static int BuildMobilityScore(MobilityDefinition mobility, BudgetCard budgetCard)
    {
        var target = ResolveGenericPowerBandTarget(budgetCard, PowerBand.Minor);
        var distance = mobility.Profile != null ? mobility.Profile.Distance : 0f;
        var baseScore =
            distance * 2f +
            BuildEffectDescriptorScore(mobility.Effects, null) +
            BuildCounterContributionScore(budgetCard);
        return Mathf.RoundToInt((baseScore + target) / 2f);
    }

    private static int BuildAffixScore(AffixDefinition affix, BudgetCard budgetCard)
    {
        var target = LoopCContentGovernance.AffixBudgetTargets.TryGetValue(budgetCard.Rarity, out var window)
            ? window.Target
            : 6;
        var baseScore =
            Math.Abs(affix.ValueMin) * 0.5f +
            Math.Abs(affix.ValueMax) * 0.5f +
            affix.Modifiers.Count * 1.5f +
            BuildEffectDescriptorScore(affix.Effects, null) +
            BuildCounterContributionScore(budgetCard);
        return Mathf.RoundToInt((baseScore + target) / 2f);
    }

    private static int BuildAugmentScore(AugmentDefinition augment, BudgetCard budgetCard)
    {
        var target = LoopCContentGovernance.AugmentBudgetTargets.TryGetValue(budgetCard.Rarity, out var window)
            ? window.Target
            : 18;
        var baseScore =
            augment.Modifiers.Count * 2f +
            BuildEffectDescriptorScore(augment.Effects, null) +
            BuildCounterContributionScore(budgetCard) +
            Math.Max(0f, budgetCard.Vector?.Economy ?? 0) * 0.75f +
            (augment.IsPermanent ? 2f : 0f);
        return Mathf.RoundToInt(target * 0.75f + baseScore);
    }

    private static int BuildSynergyScore(SynergyTierDefinition tier, BudgetCard budgetCard)
    {
        var target = ResolveGenericPowerBandTarget(budgetCard, tier.Threshold == 2 ? PowerBand.Standard : PowerBand.Major);
        var baseScore =
            tier.Modifiers.Count * 1.75f +
            BuildEffectDescriptorScore(tier.Effects, null) +
            BuildCounterContributionScore(budgetCard);
        return Mathf.RoundToInt(target * 0.85f + baseScore * 0.75f);
    }

    private static int BuildStatusScore(StatusFamilyDefinition status, BudgetCard budgetCard)
    {
        var target = ResolveGenericPowerBandTarget(budgetCard, PowerBand.Minor);
        var baseScore =
            (status.IsHardControl ? 5f : 2f) +
            (status.UsesControlDiminishing ? 2f : 0f) +
            (status.AffectedByTenacity ? 1f : 0f) +
            BuildEffectDescriptorScore(status.Effects, null) +
            BuildCounterContributionScore(budgetCard);
        return Mathf.RoundToInt((baseScore + target) / 2f);
    }

    private static int ResolveGenericPowerBandTarget(BudgetCard budgetCard, PowerBand fallback)
    {
        var band = budgetCard.PowerBand;
        return LoopCContentGovernance.PowerBandTargets.TryGetValue(band, out var window) ? window.Target : LoopCContentGovernance.PowerBandTargets[fallback].Target;
    }

    private static float BuildEffectDescriptorScore(IEnumerable<EffectDescriptor> effects, SummonProfile? summonProfile)
    {
        var score = 0f;
        foreach (var effect in effects ?? Array.Empty<EffectDescriptor>())
        {
            if ((effect.Capabilities & EffectCapability.ModifyStats) != 0)
            {
                score += 1.5f;
            }

            if ((effect.Capabilities & EffectCapability.ApplyStatus) != 0)
            {
                score += 2f;
            }

            if ((effect.Capabilities & EffectCapability.DealDamage) != 0)
            {
                score += 2f;
            }

            if ((effect.Capabilities & EffectCapability.HealOrBarrier) != 0)
            {
                score += 2f;
            }

            if ((effect.Capabilities & EffectCapability.Reposition) != 0)
            {
                score += 1.5f;
            }

            if ((effect.Capabilities & EffectCapability.SpawnSummon) != 0 || (effect.Capabilities & EffectCapability.SpawnDeployable) != 0)
            {
                score += summonProfile is { IsPersistent: true } ? 4f : 2f;
            }
        }

        return score;
    }

    private static float BuildCounterContributionScore(BudgetCard budgetCard)
    {
        return (budgetCard.DeclaredCounterTools ?? Array.Empty<CounterToolContribution>())
            .Sum(tool => tool.Strength switch
            {
                CounterCoverageStrength.Light => 1.5f,
                CounterCoverageStrength.Standard => 3f,
                CounterCoverageStrength.Strong => 5f,
                _ => 0f,
            });
    }
}
