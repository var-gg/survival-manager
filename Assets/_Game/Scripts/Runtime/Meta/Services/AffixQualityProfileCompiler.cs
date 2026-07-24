using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using SM.Core.Content;

namespace SM.Meta.Services;

/// <summary>
/// Production generated-affix state graph를 fixed-point로 완전 순회해 exact-support Q0.64 profile을 만든다.
/// </summary>
public sealed class AffixQualityProfileCompiler
{
    public const int ScoreScale = 1_000;
    public const int SpawnWeightScale = 1_000_000;
    public const int SelectorRulesVersion = 1;

    private static readonly BigInteger ProbabilityOne = new(ulong.MaxValue);

    public AffixQualityProfile Compile(
        ISessionContentLookup lookup,
        string itemBaseId,
        ItemRarityTierValue grade,
        float gradeStepBudgetScore,
        string affixCatalogVersion,
        out AffixQualityProfileCompilationMetrics metrics)
    {
        if (lookup == null)
        {
            throw new ArgumentNullException(nameof(lookup));
        }

        if (string.IsNullOrWhiteSpace(itemBaseId))
        {
            throw new ArgumentException("Item base id is required.", nameof(itemBaseId));
        }

        if (string.IsNullOrWhiteSpace(affixCatalogVersion))
        {
            throw new ArgumentException("Affix catalog version is required.", nameof(affixCatalogVersion));
        }

        var stopwatch = Stopwatch.StartNew();
        if (!GeneratedItemAffixStateGraph.TryCreate(lookup, itemBaseId, out var graph))
        {
            throw new ArgumentException($"Unknown item base id '{itemBaseId}'.", nameof(itemBaseId));
        }

        var targetBudgetScoreQ = ToBudgetScoreQ(gradeStepBudgetScore);
        var compiledCandidates = graph.Candidates
            .Select(candidate => new CompiledCandidate(
                candidate,
                ToFixedPointExact(
                    candidate.Template.BudgetScore,
                    ScoreScale,
                    $"{candidate.Template.Id}.BudgetScore"),
                Math.Max(
                    100,
                    ToFixedPointExact(
                        candidate.Template.SpawnWeight,
                        SpawnWeightScale,
                        $"{candidate.Template.Id}.SpawnWeight"))))
            .ToDictionary(candidate => candidate.Candidate.Ordinal);

        var session = new CompiledProfileGraph(
            graph,
            grade,
            Math.Max(10, targetBudgetScoreQ),
            compiledCandidates);
        var root = session.Compile(CompiledSelectorState.Root);
        var scores = root.MassByScoreQ
            .Where(entry => entry.Value > 0UL)
            .Select(entry => entry.Key)
            .OrderBy(score => score)
            .ToArray();
        var masses = scores.Select(score => root.MassByScoreQ[score]).ToArray();
        var cdf = BuildCdf(masses);
        stopwatch.Stop();

        var profile = new AffixQualityProfile(
            new QualityProfileKey(
                itemBaseId,
                graph.Item.SlotType,
                grade,
                targetBudgetScoreQ,
                SelectorRulesVersion,
                affixCatalogVersion),
            scores,
            masses,
            cdf,
            session);
        session.ObserveMemory();
        var processPeak = Process.GetCurrentProcess().PeakWorkingSet64;
        metrics = new AffixQualityProfileCompilationMetrics(
            session.MemoizedStateCount,
            root.TerminalSequences,
            stopwatch.Elapsed,
            Math.Max(processPeak, session.PeakManagedMemoryBytes),
            scores.Length);
        return profile;
    }

    public static int ToBudgetScoreQ(float value)
    {
        return ToFixedPointExact(value, ScoreScale, nameof(value));
    }

    private static int ToFixedPointExact(float value, int scale, string field)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new ArgumentException($"{field} must be finite.", field);
        }

        if (!decimal.TryParse(
                value.ToString("R", CultureInfo.InvariantCulture),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var decimalValue))
        {
            throw new ArgumentException($"{field} cannot be converted to fixed point.", field);
        }

        var scaled = decimalValue * scale;
        if (scaled != decimal.Truncate(scaled)
            || scaled < int.MinValue
            || scaled > int.MaxValue)
        {
            throw new ArgumentException(
                $"{field}={value.ToString("R", CultureInfo.InvariantCulture)} "
                + $"is not exactly representable at scale {scale}.",
                field);
        }

        return decimal.ToInt32(scaled);
    }

    private static ulong[] BuildCdf(IReadOnlyList<ulong> masses)
    {
        var result = new ulong[masses.Count];
        var cumulative = BigInteger.Zero;
        for (var index = 0; index < masses.Count; index++)
        {
            cumulative += masses[index];
            if (cumulative > ProbabilityOne)
            {
                throw new InvalidOperationException("Compiled probability mass exceeds one.");
            }

            result[index] = (ulong)cumulative;
        }

        if (result.Length == 0 || result[^1] != ulong.MaxValue)
        {
            throw new InvalidOperationException("Compiled probability mass must sum exactly to Q0.64 one.");
        }

        return result;
    }

    internal sealed class CompiledProfileGraph
    {
        private readonly GeneratedItemAffixStateGraph _graph;
        private readonly IReadOnlyList<string> _gradeStepTiers;
        private readonly int _targetBudgetScoreQ;
        private readonly IReadOnlyDictionary<int, CompiledCandidate> _candidates;
        private readonly Dictionary<CompiledSelectorState, StateResult> _memo = new();
        private long _peakManagedMemoryBytes = GC.GetTotalMemory(false);

        internal CompiledProfileGraph(
            GeneratedItemAffixStateGraph graph,
            ItemRarityTierValue grade,
            int targetBudgetScoreQ,
            IReadOnlyDictionary<int, CompiledCandidate> candidates)
        {
            _graph = graph;
            _gradeStepTiers = GeneratedItemAffixStateGraph.GetGradeStepTiers(grade);
            _targetBudgetScoreQ = targetBudgetScoreQ;
            _candidates = candidates;
        }

        internal int MemoizedStateCount => _memo.Count;
        internal long PeakManagedMemoryBytes => _peakManagedMemoryBytes;

        internal StateResult Compile(CompiledSelectorState state)
        {
            if (_memo.TryGetValue(state, out var cached))
            {
                return cached;
            }

            StateResult result;
            switch (state.Phase)
            {
                case CompiledSelectorPhase.BeforeImplicit:
                    result = CompileImplicit(state);
                    break;
                case CompiledSelectorPhase.BeforeGradeStep:
                    result = CompileBeforeGradeStep(state);
                    break;
                case CompiledSelectorPhase.DrawingGradeStep:
                    result = CompileDrawingGradeStep(state);
                    break;
                case CompiledSelectorPhase.Terminal:
                    result = StateResult.Terminal(state.TotalScoreQ);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }

            _memo.Add(state, result);
            if ((_memo.Count & 1023) == 0)
            {
                ObserveMemory();
            }

            return result;
        }

        internal void ObserveMemory()
        {
            _peakManagedMemoryBytes = Math.Max(
                _peakManagedMemoryBytes,
                GC.GetTotalMemory(false));
        }

        internal IReadOnlyList<string> SelectBudgetWeightedConditioned(
            int exactFinalScoreQ,
            Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (!_memo[CompiledSelectorState.Root].MassByScoreQ.TryGetValue(
                    exactFinalScoreQ,
                    out var rootMass)
                || rootMass == 0UL)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exactFinalScoreQ),
                    $"Score {exactFinalScoreQ} has zero completion mass in this profile.");
            }

            var selected = new List<string>();
            var state = CompiledSelectorState.Root;
            while (state.Phase != CompiledSelectorPhase.Terminal)
            {
                switch (state.Phase)
                {
                    case CompiledSelectorPhase.BeforeImplicit:
                    {
                        var candidates = _graph.GetCandidates(
                            GeneratedItemAffixStateGraph.ImplicitTier,
                            state.SelectedAffixMask,
                            state.OccupiedExclusiveGroupMask);
                        if (candidates.Count == 0)
                        {
                            state = state with { Phase = CompiledSelectorPhase.BeforeGradeStep };
                            break;
                        }

                        state = SelectConditionedCandidate(
                            state,
                            candidates,
                            _targetBudgetScoreQ,
                            exactFinalScoreQ,
                            random,
                            isImplicit: true,
                            selected);
                        break;
                    }
                    case CompiledSelectorPhase.BeforeGradeStep:
                    {
                        if (state.GradeStepIndex >= _gradeStepTiers.Count)
                        {
                            state = state with { Phase = CompiledSelectorPhase.Terminal };
                            break;
                        }

                        var lowerBudget = Math.Max(1, _targetBudgetScoreQ / ScoreScale);
                        var fractionQ = _targetBudgetScoreQ - (lowerBudget * ScoreScale);
                        var lowerState = state with
                        {
                            Phase = CompiledSelectorPhase.DrawingGradeStep,
                            StepBudgetQ = lowerBudget * ScoreScale,
                            AccumulatedStepProgressQ = 0,
                        };
                        if (fractionQ <= 0)
                        {
                            state = lowerState;
                            break;
                        }

                        var upperState = lowerState with
                        {
                            StepBudgetQ = (lowerBudget + 1) * ScoreScale,
                        };
                        state = SelectConditionedTransition(
                            new[]
                            {
                                new StateTransition(
                                    new ExactWeight(ScoreScale - fractionQ, 1),
                                    lowerState,
                                    null),
                                new StateTransition(
                                    new ExactWeight(fractionQ, 1),
                                    upperState,
                                    null),
                            },
                            exactFinalScoreQ,
                            random).NextState;
                        break;
                    }
                    case CompiledSelectorPhase.DrawingGradeStep:
                    {
                        if (state.AccumulatedStepProgressQ >= state.StepBudgetQ)
                        {
                            state = AdvanceGradeStep(state);
                            break;
                        }

                        var tier = _gradeStepTiers[state.GradeStepIndex];
                        var candidates = _graph.GetCandidates(
                            tier,
                            state.SelectedAffixMask,
                            state.OccupiedExclusiveGroupMask);
                        if (candidates.Count == 0)
                        {
                            state = AdvanceGradeStep(state);
                            break;
                        }

                        state = SelectConditionedCandidate(
                            state,
                            candidates,
                            Math.Max(10, state.StepBudgetQ - state.AccumulatedStepProgressQ),
                            exactFinalScoreQ,
                            random,
                            isImplicit: false,
                            selected);
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(state));
                }

                if (!_memo.TryGetValue(state, out var completion)
                    || !completion.MassByScoreQ.TryGetValue(exactFinalScoreQ, out var mass)
                    || mass == 0UL)
                {
                    throw new InvalidOperationException(
                        $"Conditioned traversal entered zero-mass state for score {exactFinalScoreQ}.");
                }
            }

            if (state.TotalScoreQ != exactFinalScoreQ)
            {
                throw new InvalidOperationException(
                    $"Conditioned traversal ended at {state.TotalScoreQ}, expected {exactFinalScoreQ}.");
            }

            return selected;
        }

        private CompiledSelectorState SelectConditionedCandidate(
            CompiledSelectorState state,
            IReadOnlyList<GeneratedItemAffixStateGraph.Candidate> candidates,
            int targetBudgetScoreQ,
            int exactFinalScoreQ,
            Random random,
            bool isImplicit,
            ICollection<string> selected)
        {
            var transitions = new StateTransition[candidates.Count];
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = _candidates[candidates[index].Ordinal];
                var denominator = ScoreScale
                    + BigInteger.Abs(
                        new BigInteger(candidate.BudgetScoreQ)
                        - targetBudgetScoreQ);
                transitions[index] = new StateTransition(
                    new ExactWeight(candidate.SpawnWeightQ, denominator),
                    ApplyCandidate(state, candidate, isImplicit),
                    candidate.Candidate.Template.Id);
            }

            var chosen = SelectConditionedTransition(
                transitions,
                exactFinalScoreQ,
                random);
            selected.Add(chosen.SelectedAffixId!);
            return chosen.NextState;
        }

        private StateTransition SelectConditionedTransition(
            IReadOnlyList<StateTransition> transitions,
            int exactFinalScoreQ,
            Random random)
        {
            var viable = new List<StateTransition>();
            var effectiveWeights = new List<ExactWeight>();
            foreach (var transition in transitions)
            {
                if (!_memo[transition.NextState].MassByScoreQ.TryGetValue(
                        exactFinalScoreQ,
                        out var completionMass)
                    || completionMass == 0UL)
                {
                    continue;
                }

                viable.Add(transition);
                effectiveWeights.Add(new ExactWeight(
                    transition.NaturalWeight.Numerator * completionMass,
                    transition.NaturalWeight.Denominator));
            }

            if (viable.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No transition can complete at exact score {exactFinalScoreQ}.");
            }

            var masses = AllocateQ64(effectiveWeights);
            var chosenIndex = GeneratedItemAffixStateGraph.SelectWeightedIndex(
                masses.Select(mass => (double)mass).ToArray(),
                random);
            return viable[chosenIndex];
        }

        private StateResult CompileImplicit(CompiledSelectorState state)
        {
            var candidates = _graph.GetCandidates(
                GeneratedItemAffixStateGraph.ImplicitTier,
                state.SelectedAffixMask,
                state.OccupiedExclusiveGroupMask);
            if (candidates.Count == 0)
            {
                return Compile(state with { Phase = CompiledSelectorPhase.BeforeGradeStep });
            }

            return CompileWeightedCandidates(
                state,
                candidates,
                _targetBudgetScoreQ,
                isImplicit: true);
        }

        private StateResult CompileBeforeGradeStep(CompiledSelectorState state)
        {
            if (state.GradeStepIndex >= _gradeStepTiers.Count)
            {
                return Compile(state with { Phase = CompiledSelectorPhase.Terminal });
            }

            var lowerBudget = Math.Max(1, _targetBudgetScoreQ / ScoreScale);
            var fractionQ = _targetBudgetScoreQ - (lowerBudget * ScoreScale);
            if (fractionQ <= 0)
            {
                return Compile(state with
                {
                    Phase = CompiledSelectorPhase.DrawingGradeStep,
                    StepBudgetQ = lowerBudget * ScoreScale,
                    AccumulatedStepProgressQ = 0,
                });
            }

            var lowerState = state with
            {
                Phase = CompiledSelectorPhase.DrawingGradeStep,
                StepBudgetQ = lowerBudget * ScoreScale,
                AccumulatedStepProgressQ = 0,
            };
            var upperState = lowerState with { StepBudgetQ = (lowerBudget + 1) * ScoreScale };
            return Combine(
                new[]
                {
                    new WeightedChild(
                        new ExactWeight(ScoreScale - fractionQ, 1),
                        Compile(lowerState)),
                    new WeightedChild(
                        new ExactWeight(fractionQ, 1),
                        Compile(upperState)),
                });
        }

        private StateResult CompileDrawingGradeStep(CompiledSelectorState state)
        {
            if (state.AccumulatedStepProgressQ >= state.StepBudgetQ)
            {
                return Compile(AdvanceGradeStep(state));
            }

            var tier = _gradeStepTiers[state.GradeStepIndex];
            var candidates = _graph.GetCandidates(
                tier,
                state.SelectedAffixMask,
                state.OccupiedExclusiveGroupMask);
            if (candidates.Count == 0)
            {
                return Compile(AdvanceGradeStep(state));
            }

            return CompileWeightedCandidates(
                state,
                candidates,
                Math.Max(10, state.StepBudgetQ - state.AccumulatedStepProgressQ),
                isImplicit: false);
        }

        private StateResult CompileWeightedCandidates(
            CompiledSelectorState state,
            IReadOnlyList<GeneratedItemAffixStateGraph.Candidate> candidates,
            int targetBudgetScoreQ,
            bool isImplicit)
        {
            var children = new WeightedChild[candidates.Count];
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = _candidates[candidates[index].Ordinal];
                var denominator = ScoreScale
                    + BigInteger.Abs(
                        new BigInteger(candidate.BudgetScoreQ)
                        - targetBudgetScoreQ);
                var next = ApplyCandidate(state, candidate, isImplicit);
                children[index] = new WeightedChild(
                    new ExactWeight(candidate.SpawnWeightQ, denominator),
                    Compile(next));
            }

            return Combine(children);
        }

        private static CompiledSelectorState ApplyCandidate(
            CompiledSelectorState state,
            CompiledCandidate candidate,
            bool isImplicit)
        {
            var nextTotal = checked(state.TotalScoreQ + candidate.BudgetScoreQ);
            var next = state with
            {
                TotalScoreQ = nextTotal,
                SelectedAffixMask = state.SelectedAffixMask | candidate.Candidate.IdMask,
                OccupiedExclusiveGroupMask =
                    state.OccupiedExclusiveGroupMask | candidate.Candidate.ExclusiveGroupMask,
            };
            if (isImplicit)
            {
                return next with { Phase = CompiledSelectorPhase.BeforeGradeStep };
            }

            return next with
            {
                AccumulatedStepProgressQ = checked(
                    state.AccumulatedStepProgressQ
                    + Math.Max(10, candidate.BudgetScoreQ)),
            };
        }

        private static CompiledSelectorState AdvanceGradeStep(CompiledSelectorState state)
        {
            return state with
            {
                Phase = CompiledSelectorPhase.BeforeGradeStep,
                GradeStepIndex = checked(state.GradeStepIndex + 1),
                StepBudgetQ = 0,
                AccumulatedStepProgressQ = 0,
            };
        }

        private static StateResult Combine(IReadOnlyList<WeightedChild> children)
        {
            var transitionMasses = AllocateQ64(children.Select(child => child.Weight).ToArray());
            var numeratorsByScore = new SortedDictionary<int, BigInteger>();
            var terminalSequences = BigInteger.Zero;
            for (var index = 0; index < children.Count; index++)
            {
                terminalSequences += children[index].Result.TerminalSequences;
                foreach (var (score, childMass) in children[index].Result.MassByScoreQ)
                {
                    numeratorsByScore.TryGetValue(score, out var current);
                    numeratorsByScore[score] = current
                        + (new BigInteger(transitionMasses[index]) * childMass);
                }
            }

            return new StateResult(
                NormalizeProducts(numeratorsByScore),
                terminalSequences);
        }

        private static ulong[] AllocateQ64(IReadOnlyList<ExactWeight> weights)
        {
            var commonDenominator = BigInteger.One;
            foreach (var weight in weights)
            {
                if (weight.Numerator <= 0 || weight.Denominator <= 0)
                {
                    throw new InvalidOperationException("Transition weights must be positive.");
                }

                commonDenominator *= weight.Denominator;
            }

            var commonNumerators = new BigInteger[weights.Count];
            var total = BigInteger.Zero;
            for (var index = 0; index < weights.Count; index++)
            {
                commonNumerators[index] = weights[index].Numerator
                    * (commonDenominator / weights[index].Denominator);
                total += commonNumerators[index];
            }

            var masses = new ulong[weights.Count];
            var remainders = new BigInteger[weights.Count];
            var allocated = BigInteger.Zero;
            for (var index = 0; index < weights.Count; index++)
            {
                var quotient = BigInteger.DivRem(
                    commonNumerators[index] * ProbabilityOne,
                    total,
                    out var remainder);
                masses[index] = (ulong)quotient;
                remainders[index] = remainder;
                allocated += quotient;
            }

            var leftover = checked((int)(ProbabilityOne - allocated));
            foreach (var index in Enumerable.Range(0, weights.Count)
                         .OrderByDescending(index => remainders[index])
                         .ThenBy(index => index)
                         .Take(leftover))
            {
                masses[index] = checked(masses[index] + 1UL);
            }

            return masses;
        }

        private static IReadOnlyDictionary<int, ulong> NormalizeProducts(
            IReadOnlyDictionary<int, BigInteger> numeratorsByScore)
        {
            var masses = new SortedDictionary<int, ulong>();
            var remainders = new List<(int Score, BigInteger Remainder)>();
            var allocated = BigInteger.Zero;
            foreach (var (score, numerator) in numeratorsByScore)
            {
                var quotient = BigInteger.DivRem(
                    numerator,
                    ProbabilityOne,
                    out var remainder);
                masses.Add(score, (ulong)quotient);
                remainders.Add((score, remainder));
                allocated += quotient;
            }

            var leftover = checked((int)(ProbabilityOne - allocated));
            foreach (var (score, _) in remainders
                         .OrderByDescending(entry => entry.Remainder)
                         .ThenBy(entry => entry.Score)
                         .Take(leftover))
            {
                masses[score] = checked(masses[score] + 1UL);
            }

            return masses;
        }
    }

    internal enum CompiledSelectorPhase
    {
        BeforeImplicit = 0,
        BeforeGradeStep = 1,
        DrawingGradeStep = 2,
        Terminal = 3,
    }

    internal readonly record struct CompiledSelectorState(
        CompiledSelectorPhase Phase,
        int GradeStepIndex,
        int StepBudgetQ,
        int AccumulatedStepProgressQ,
        int TotalScoreQ,
        BigInteger SelectedAffixMask,
        BigInteger OccupiedExclusiveGroupMask)
    {
        internal static CompiledSelectorState Root => new(
            CompiledSelectorPhase.BeforeImplicit,
            0,
            0,
            0,
            0,
            BigInteger.Zero,
            BigInteger.Zero);
    }

    internal sealed record CompiledCandidate(
        GeneratedItemAffixStateGraph.Candidate Candidate,
        int BudgetScoreQ,
        int SpawnWeightQ);

    private readonly record struct ExactWeight(BigInteger Numerator, BigInteger Denominator);
    private sealed record WeightedChild(ExactWeight Weight, StateResult Result);
    private sealed record StateTransition(
        ExactWeight NaturalWeight,
        CompiledSelectorState NextState,
        string? SelectedAffixId);

    internal sealed record StateResult(
        IReadOnlyDictionary<int, ulong> MassByScoreQ,
        BigInteger TerminalSequences)
    {
        internal static StateResult Terminal(int totalScoreQ)
        {
            return new StateResult(
                new SortedDictionary<int, ulong>
                {
                    [totalScoreQ] = ulong.MaxValue,
                },
                BigInteger.One);
        }
    }
}
