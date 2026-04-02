using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Meta.Model;

namespace SM.Editor.Validation;

public static class LoopDContentHealthAnalysisService
{
    public static bool ProvidesUniqueCoverage(ContentGovernanceSummary? governance)
    {
        return governance != null
               && ((governance.DeclaredThreatPatterns?.Count ?? 0) > 0
                   || (governance.DeclaredCounterTools?.Count ?? 0) > 0);
    }

    public static ContentIdentityFingerprint BuildFingerprint(CombatArchetypeTemplate template)
    {
        return new ContentIdentityFingerprint
        {
            PrimaryTagA = template.RaceId,
            PrimaryTagB = template.ClassId,
            RoleProfileId = template.Governance?.RoleProfile ?? string.Empty,
            RarityId = template.Governance?.Rarity ?? string.Empty,
            PowerBandId = template.Governance?.PowerBand ?? string.Empty,
            ActivationModelId = "unit",
            RangeDisciplineId = template.Behavior?.RangeDiscipline.ToString() ?? string.Empty,
            UsesSummon = template.Skills.Any(skill => skill.SummonProfile != null),
            UsesDeployable = template.Skills.Any(skill => skill.SummonProfile?.EntityKind == CombatEntityKind.Deployable),
            UsesBarrier = template.Skills.Any(skill =>
                skill.HealCoeff > 0f
                || (skill.EffectDescriptors?.Any(effect => effect.Capabilities.HasFlag(EffectCapability.HealOrBarrier)) ?? false)),
            UsesGuardBreak = (template.Governance?.DeclaredCounterTools ?? Array.Empty<CompiledCounterToolContribution>())
                .Any(tool => string.Equals(tool.Tool, CounterTool.GuardBreakMultiHit.ToString(), StringComparison.Ordinal)),
            ThreatPatterns = template.Governance?.DeclaredThreatPatterns?.ToArray() ?? Array.Empty<string>(),
            CounterTools = (template.Governance?.DeclaredCounterTools ?? Array.Empty<CompiledCounterToolContribution>())
                .Select(tool => tool.Tool)
                .ToArray(),
        };
    }

    public static float CalculateIdentitySimilarity(ContentIdentityFingerprint left, ContentIdentityFingerprint right)
    {
        var score = 0f;
        score += Match(left.RoleProfileId, right.RoleProfileId) ? 0.20f : 0f;
        score += Match(left.ActivationModelId, right.ActivationModelId) ? 0.10f : 0f;
        score += Match(left.RangeDisciplineId, right.RangeDisciplineId) ? 0.10f : 0f;
        score += Match(left.PrimaryTagA, right.PrimaryTagA) ? 0.05f : 0f;
        score += Match(left.PrimaryTagB, right.PrimaryTagB) ? 0.05f : 0f;
        score += Jaccard(left.ThreatPatterns, right.ThreatPatterns) * 0.20f;
        score += Jaccard(left.CounterTools, right.CounterTools) * 0.20f;
        score += BoolMatch(left.UsesSummon, right.UsesSummon) * 0.04f;
        score += BoolMatch(left.UsesDeployable, right.UsesDeployable) * 0.03f;
        score += BoolMatch(left.UsesBarrier, right.UsesBarrier) * 0.02f;
        score += BoolMatch(left.UsesGuardBreak, right.UsesGuardBreak) * 0.01f;
        return Math.Clamp(score, 0f, 1f);
    }

    public static float CalculateHighestIdentitySimilarity(ContentIdentityFingerprint subject, IEnumerable<ContentIdentityFingerprint> others)
    {
        return others
            .Where(other => !ReferenceEquals(other, subject))
            .Select(other => CalculateIdentitySimilarity(subject, other))
            .DefaultIfEmpty(0f)
            .Max();
    }

    public static ContentHealthGrade ResolveGrade(ContentHealthCard card)
    {
        if (card.Reasons.Contains(PruneReason.ForbiddenLeak) || card.Debt.Total > 50)
        {
            return ContentHealthGrade.Broken;
        }

        return card.Debt.Total switch
        {
            <= 15 => ContentHealthGrade.Healthy,
            <= 30 => ContentHealthGrade.Watch,
            <= 50 => ContentHealthGrade.AtRisk,
            _ => ContentHealthGrade.Broken,
        };
    }

    public static PruneDisposition ResolveDisposition(ContentHealthCard card)
    {
        if (card.Reasons.Contains(PruneReason.ForbiddenLeak))
        {
            return PruneDisposition.MoveOutOfV1;
        }

        if (card.UnexplainedEffectShare > 0.08f
            || card.ContributionToSalienceOverload > 0.10f
            || card.Reasons.Contains(PruneReason.ReadabilityDebt))
        {
            return PruneDisposition.SimplifyReadability;
        }

        if (card.HighestIdentitySimilarity >= 0.90f && !card.ProvidesUniqueCoverage)
        {
            return PruneDisposition.MergeWithSibling;
        }

        if (card.PickRate <= 0.05f && !card.ProvidesUniqueCoverage)
        {
            return PruneDisposition.MoveOutOfV1;
        }

        if (card.PickRate >= 0.38f && card.PresenceWinDelta > 0.06f)
        {
            return PruneDisposition.RetuneNumbers;
        }

        if (card.Grade == ContentHealthGrade.Broken && !card.ProvidesUniqueCoverage)
        {
            return PruneDisposition.MoveOutOfV1;
        }

        return card.Debt.PowerDebt >= 8 ? PruneDisposition.RetuneNumbers : PruneDisposition.Keep;
    }

    public static ContentKind ResolveContentKind(CombatContentSnapshot snapshot, FirstPlayableSliceDefinition slice, string contentId)
    {
        if (slice.UnitBlueprintIds.Contains(contentId, StringComparer.Ordinal) || snapshot.Archetypes.ContainsKey(contentId))
        {
            return ContentKind.UnitBlueprint;
        }

        if (slice.SignatureActiveIds.Contains(contentId, StringComparer.Ordinal))
        {
            return ContentKind.SignatureActive;
        }

        if (slice.SignaturePassiveIds.Contains(contentId, StringComparer.Ordinal))
        {
            return ContentKind.SignaturePassive;
        }

        if (slice.FlexActiveIds.Contains(contentId, StringComparer.Ordinal))
        {
            return ContentKind.FlexActive;
        }

        if (slice.FlexPassiveIds.Contains(contentId, StringComparer.Ordinal))
        {
            return ContentKind.FlexPassive;
        }

        if (slice.AffixIds.Contains(contentId, StringComparer.Ordinal) || snapshot.AffixPackages.ContainsKey(contentId))
        {
            return ContentKind.Affix;
        }

        if (slice.SynergyFamilyIds.Contains(contentId, StringComparer.Ordinal))
        {
            return ContentKind.SynergyFamily;
        }

        return ContentKind.TemporaryAugment;
    }

    private static bool Match(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left)
               && !string.IsNullOrWhiteSpace(right)
               && string.Equals(left, right, StringComparison.Ordinal);
    }

    private static float BoolMatch(bool left, bool right)
    {
        return left == right ? 1f : 0f;
    }

    private static float Jaccard(IEnumerable<string> left, IEnumerable<string> right)
    {
        var leftSet = left.Where(value => !string.IsNullOrWhiteSpace(value)).ToHashSet(StringComparer.Ordinal);
        var rightSet = right.Where(value => !string.IsNullOrWhiteSpace(value)).ToHashSet(StringComparer.Ordinal);
        if (leftSet.Count == 0 && rightSet.Count == 0)
        {
            return 1f;
        }

        if (leftSet.Count == 0 || rightSet.Count == 0)
        {
            return 0f;
        }

        var intersection = leftSet.Intersect(rightSet, StringComparer.Ordinal).Count();
        var union = leftSet.Union(rightSet, StringComparer.Ordinal).Count();
        return union == 0 ? 0f : intersection / (float)union;
    }
}
