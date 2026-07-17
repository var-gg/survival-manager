using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.HeadlessCensus;
using SM.Meta.Model;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>동일 real-content fixture에서 option source 하나만 바꿔 BattleResolver pair를 만든다.</summary>
internal static class H100OptionTrapBattleRunner
{
    public static IReadOnlyList<OptionPairedCounterfactual> Run(
        RuntimeCombatContentLookup lookup,
        CombatContentSnapshot snapshot,
        BuildSpaceCensus census,
        IReadOnlyList<OptionWitnessContract> contracts,
        IReadOnlyList<BuildGrammarTruthSource> sources,
        IReadOnlyCollection<string> optionIds,
        H100OptionTrapRunSettings settings,
        bool fullCensus)
    {
        var contractsById = contracts.ToDictionary(value => value.OptionId, StringComparer.Ordinal);
        var sourcesById = sources
            .Where(value => OptionWitnessContract.IsSupportedSubjectKind(value.SubjectKind))
            .ToDictionary(
                value => OptionWitnessContract.StableOptionId(value.SubjectKind, value.SubjectId),
                StringComparer.Ordinal);
        var pairs = new List<OptionPairedCounterfactual>();
        var fixtureCache = new Dictionary<string, BattleFixture>(StringComparer.Ordinal);
        foreach (var optionId in optionIds.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!contractsById.TryGetValue(optionId, out var contract)
                || !sourcesById.TryGetValue(optionId, out var optionSource))
            {
                continue;
            }

            var comparatorIds = new[] { contract.BaselineComparatorId }
                .Concat(contract.ComparatorOptionIds.Where(sourcesById.ContainsKey).Take(1))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var cases = H100OptionTrapCaseFactory.Build(
                contract,
                optionSource,
                snapshot,
                census,
                settings,
                fullCensus);
            foreach (var battleCase in cases)
            {
                var fixtureKey = $"{battleCase.BuildId}|{battleCase.MedoidSignature}|{battleCase.Seed}";
                if (!fixtureCache.TryGetValue(fixtureKey, out var fixture)
                    && !TryBuildFixture(lookup, battleCase, out fixture))
                {
                    continue;
                }

                fixtureCache[fixtureKey] = fixture;

                var targetIndex = ResolveTargetIndex(fixture.Allies, optionSource);
                var legalAllies = AddIntendedPredicateContext(fixture.Allies, targetIndex, optionSource);
                var optionAllies = ApplySource(legalAllies, targetIndex, optionSource, snapshot);
                var optionRun = RunBattle(fixture with { Allies = optionAllies }, contract, optionSource, settings.MaxBattleSteps);
                foreach (var comparatorId in comparatorIds)
                {
                    sourcesById.TryGetValue(comparatorId, out var comparatorSource);
                    var comparatorAllies = comparatorSource == null
                        ? legalAllies
                        : ApplySource(legalAllies, targetIndex, comparatorSource, snapshot);
                    var comparatorRun = RunBattle(fixture with { Allies = comparatorAllies }, contract, comparatorSource, settings.MaxBattleSteps);
                    pairs.Add(new OptionPairedCounterfactual(
                        optionId,
                        comparatorId,
                        battleCase.BuildId,
                        battleCase.Seed,
                        battleCase.MedoidSignature,
                        IntendedContext: true,
                        FullCensus: fullCensus,
                        ExplicitTradeoffVisible: contract.HasVisibleTradeoff,
                        optionRun.Outcome,
                        comparatorRun.Outcome,
                        optionRun.ReplayHash,
                        comparatorRun.ReplayHash));
                }
            }
        }

        return pairs;
    }

    private static bool TryBuildFixture(
        RuntimeCombatContentLookup lookup,
        H100BattleScreeningCase battleCase,
        out BattleFixture fixture)
    {
        try
        {
            var session = H100ScreeningSessionFactory.Create(lookup, battleCase.CaseId, battleCase.Members, "trap");
            session.BeginNewExpedition();
            if (!session.TryBuildSelectedBattleState(out _, out var encounter, out var allySnapshot, out _))
            {
                fixture = null!;
                return false;
            }

            var seeded = encounter with { Context = encounter.Context with { BattleSeed = battleCase.Seed } };
            if (!session.TryComposeBattleState(allySnapshot, seeded, out var state, out _))
            {
                fixture = null!;
                return false;
            }

            fixture = new BattleFixture(
                state.Allies.Select(value => value.Definition).ToArray(),
                state.Enemies.Select(value => value.Definition).ToArray(),
                state.AllyPosture,
                state.EnemyPosture,
                state.FixedStepSeconds,
                battleCase.Seed,
                state.StatusRules);
            return true;
        }
        catch
        {
            fixture = null!;
            return false;
        }
    }

    private static int ResolveTargetIndex(
        IReadOnlyList<BattleUnitLoadout> allies,
        BuildGrammarTruthSource source)
    {
        if (source.Skill != null)
        {
            var existing = allies.Select((ally, index) => new { ally, index })
                .FirstOrDefault(value => value.ally.Skills.Any(skill => skill.Id == source.Skill.Id));
            if (existing != null) return existing.index;
        }

        var required = (source.RequiredTags ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        var excluded = (source.ExcludedTags ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        var compatible = allies.Select((ally, index) => new { ally, index })
            .FirstOrDefault(value => required.IsSubsetOf(value.ally.CompileTags ?? Array.Empty<string>())
                                     && !excluded.Overlaps(value.ally.CompileTags ?? Array.Empty<string>()));
        return compatible?.index ?? 0;
    }

    private static IReadOnlyList<BattleUnitLoadout> AddIntendedPredicateContext(
        IReadOnlyList<BattleUnitLoadout> allies,
        int targetIndex,
        BuildGrammarTruthSource source)
    {
        var result = allies.ToArray();
        var target = result[targetIndex];
        result[targetIndex] = target with
        {
            CompileTags = StableIds((target.CompileTags ?? Array.Empty<string>())
                .Concat(source.RequiredTags ?? Array.Empty<string>()))
        };
        return result;
    }

    private static IReadOnlyList<BattleUnitLoadout> ApplySource(
        IReadOnlyList<BattleUnitLoadout> allies,
        int targetIndex,
        BuildGrammarTruthSource source,
        CombatContentSnapshot snapshot)
    {
        var result = allies.ToArray();
        var target = result[targetIndex];
        var numeric = (target.Packages ?? Array.Empty<CombatModifierPackage>()).ToList();
        if (source.ModifierPackage != null) numeric.Add(source.ModifierPackage);
        var rules = (target.RulePackages ?? Array.Empty<CombatRuleModifierPackage>()).ToList();
        if (source.RulePackage != null) rules.Add(source.RulePackage);
        var triggers = (target.TriggeredEffects ?? Array.Empty<CombatTriggeredEffect>())
            .Concat(source.TriggeredEffects ?? Array.Empty<CombatTriggeredEffect>())
            .ToList();
        var skills = target.Skills.ToList();
        var signatureActive = target.SignatureActive;
        var flexActive = target.FlexActive;
        if (source.Skill != null)
        {
            var slot = source.Skill.EffectiveSlotKind;
            skills.RemoveAll(value => value.EffectiveSlotKind == slot);
            skills.Add(source.Skill);
            triggers.AddRange(source.Skill.TriggeredEffects ?? Array.Empty<CombatTriggeredEffect>());
            if (slot == ActionSlotKind.SignatureActive) signatureActive = source.Skill;
            if (slot == ActionSlotKind.FlexActive) flexActive = source.Skill;
        }

        foreach (var grantedId in source.GrantedSkillIds ?? Array.Empty<string>())
        {
            if (!snapshot.SkillCatalog.TryGetValue(grantedId, out var granted)) continue;
            if (skills.All(value => value.Id != granted.Id)) skills.Add(granted);
            triggers.AddRange(granted.TriggeredEffects ?? Array.Empty<CombatTriggeredEffect>());
        }

        result[targetIndex] = target with
        {
            Packages = numeric,
            RulePackages = rules,
            TriggeredEffects = triggers,
            Skills = skills.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray(),
            SignatureActive = signatureActive,
            FlexActive = flexActive,
            CompileTags = StableIds((target.CompileTags ?? Array.Empty<string>())
                .Concat(source.Tags ?? Array.Empty<string>())
                .Append($"{source.SubjectKind}:{source.SubjectId}")),
        };
        return result;
    }

    private static BattleRun RunBattle(
        BattleFixture fixture,
        OptionWitnessContract contract,
        BuildGrammarTruthSource? source,
        int maxBattleSteps)
    {
        var state = BattleFactory.Create(
            fixture.Allies,
            fixture.Enemies,
            fixture.AllyPosture,
            fixture.EnemyPosture,
            fixture.FixedStepSeconds,
            fixture.Seed,
            statusRules: fixture.StatusRules);
        var result = BattleResolver.Run(state, maxBattleSteps);
        var allies = result.FinalUnits.Where(value => value.Side == TeamSide.Ally).ToArray();
        var hp = Fraction(allies.Sum(value => value.CurrentHealth), allies.Sum(value => value.MaxHealth));
        var resource = Fraction(allies.Sum(value => value.CurrentEnergy), allies.Sum(value => value.MaxEnergy));
        var milestones = contract.Promises.Count(promise => HasFeedback(result, promise, null));
        var unique = source == null ? 0 : contract.Promises.Count(promise => HasFeedback(result, promise, source.SubjectId));
        var win = result.Winner == TeamSide.Ally ? 1d : result.Winner == TeamSide.Enemy ? 0d : 0.5d;
        return new BattleRun(
            new OptionOutcomeVector(win, hp, resource, milestones, unique, 0d),
            BattleStateCanonicalHash.Compute(state));
    }

    private static bool HasFeedback(BattleResult result, OptionWitnessPromise promise, string? sourceId)
    {
        var kind = promise.ExpectedFeedbackWitness switch
        {
            "telemetry.skill_cast_resolved" => TelemetryEventKind.SkillCastResolved,
            "telemetry.status_applied" => TelemetryEventKind.StatusApplied,
            "telemetry.status_removed" => TelemetryEventKind.StatusRemoved,
            "telemetry.damage_applied" => TelemetryEventKind.DamageApplied,
            "telemetry.healing_applied" => TelemetryEventKind.HealingApplied,
            "telemetry.barrier_applied" => TelemetryEventKind.BarrierApplied,
            _ => (TelemetryEventKind?)null,
        };
        if (!kind.HasValue) return false;
        return (result.TelemetryEvents ?? Array.Empty<TelemetryEventRecord>()).Any(value =>
            value.EventKind == kind.Value && (sourceId == null || References(value, sourceId)));
    }

    private static bool References(TelemetryEventRecord value, string sourceId)
        => value.Explain?.SourceContentId == sourceId
           || value.SkillId == sourceId
           || value.PassiveId == sourceId
           || value.AffixId == sourceId
           || value.AugmentId == sourceId;

    private static double Fraction(double numerator, double denominator)
        => denominator <= 0d ? 0d : Math.Clamp(numerator / denominator, 0d, 1d);

    private static string[] StableIds(IEnumerable<string> values)
        => values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private sealed record BattleFixture(
        IReadOnlyList<BattleUnitLoadout> Allies,
        IReadOnlyList<BattleUnitLoadout> Enemies,
        TeamPostureType AllyPosture,
        TeamPostureType EnemyPosture,
        float FixedStepSeconds,
        int Seed,
        CombatStatusRules StatusRules);

    private sealed record BattleRun(OptionOutcomeVector Outcome, string ReplayHash);
}
