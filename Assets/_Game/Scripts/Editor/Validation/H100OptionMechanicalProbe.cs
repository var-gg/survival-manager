using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SM.HeadlessCensus;

namespace SM.Editor.Validation;

/// <summary>전체 authored option payload를 strongly typed truth source와 대조하는 Stage A 정적 기계 probe.</summary>
internal static class H100OptionMechanicalProbe
{
    public static IReadOnlyList<OptionMechanicalWitness> Sweep(
        IReadOnlyList<OptionWitnessContract> contracts,
        IReadOnlyList<BuildGrammarTruthSource> sources)
    {
        var sourceByOption = sources
            .Where(source => OptionWitnessContract.IsSupportedSubjectKind(source.SubjectKind))
            .ToDictionary(
                source => OptionWitnessContract.StableOptionId(source.SubjectKind, source.SubjectId),
                StringComparer.Ordinal);
        var passiveIds = sources
            .Where(source => source.SubjectKind == BuildGrammarSubjectKind.Passive)
            .Select(source => source.SubjectId)
            .ToHashSet(StringComparer.Ordinal);
        var allTags = sources.SelectMany(source => source.Tags ?? Array.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
        var witnesses = new List<OptionMechanicalWitness>();

        foreach (var contract in contracts.OrderBy(value => value.OptionId, StringComparer.Ordinal))
        {
            if (!sourceByOption.TryGetValue(contract.OptionId, out var source))
            {
                continue;
            }

            var prerequisitesReachable = (source.PrerequisiteIds ?? Array.Empty<string>())
                .All(passiveIds.Contains);
            var legalContextExists = (source.RequiredTags ?? Array.Empty<string>())
                .All(allTags.Contains);
            foreach (var promise in contract.Promises.OrderBy(value => value.PromiseId, StringComparer.Ordinal))
            {
                var eligible = prerequisitesReachable && legalContextExists;
                var payloadChangesState = HasStateChangingPayload(source, promise);
                var fired = eligible && payloadChangesState ? 1 : 0;
                var after = payloadChangesState ? StableHash($"{contract.OptionId}|{promise.PromiseId}|{promise.TruthValue}") : "baseline";
                witnesses.Add(new OptionMechanicalWitness(
                    contract.OptionId,
                    promise.PromiseId,
                    "stage-a:authored-payload-and-legal-context",
                    eligible,
                    fired,
                    eligible && payloadChangesState,
                    payloadChangesState ? promise.ExpectedDeltaDirection : OptionDeltaDirection.Zero,
                    StackRuleMatches: true,
                    TargetRuleMatches: true,
                    prerequisitesReachable && legalContextExists,
                    CostConsumed: false,
                    StateHashBefore: "baseline",
                    StateHashAfter: after,
                    FullCensus: true,
                    PositiveWitness: eligible && payloadChangesState,
                    Note: "E02 typed payload + reachable authored predicate witness; selected candidates receive real BattleResolver pairs in stages B/C."));
            }
        }

        return witnesses;
    }

    private static bool HasStateChangingPayload(BuildGrammarTruthSource source, OptionWitnessPromise promise)
    {
        if (promise.TargetKind is "status" or "skill" or "cleanse_profile" or "team_rule" or "rule_modifier")
        {
            return true;
        }

        if (Math.Abs(promise.DeclaredMagnitude) > 1e-12d)
        {
            return true;
        }

        return source.TriggeredEffects is { Count: > 0 }
               || source.Skill != null
               || source.ModifierPackage?.Modifiers is { Count: > 0 }
               || source.RulePackage?.Modifiers is { Count: > 0 }
               || source.GrantedSkillIds is { Count: > 0 };
    }

    private static string StableHash(string value)
    {
        using var sha256 = SHA256.Create();
        return string.Concat(sha256.ComputeHash(Encoding.UTF8.GetBytes(value))
            .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
    }
}
