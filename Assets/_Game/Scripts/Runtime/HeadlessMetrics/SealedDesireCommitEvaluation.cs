using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

internal sealed record SealedDesireCommitCohortEvaluation(
    int SuppliedRunCount,
    bool CohortConsistent,
    string CohortFailureReason,
    IReadOnlyList<SealedDesireCommitRunEvaluation> Runs);

internal sealed record SealedDesireCommitRunEvaluation(
    SealedScoredRunInput Input,
    bool Valid,
    bool SpontaneousIntent,
    string FirstQualifyingIntentId,
    int FirstQualifyingIntentIndex,
    IReadOnlyList<Bt5CommitWitness> Commits,
    Bt5RunDiagnostics Diagnostics,
    bool VisibleUniverseAvailable,
    IReadOnlyCollection<string> VisibleIds,
    IReadOnlyCollection<string> AllDeclaredTrackIds,
    IReadOnlyCollection<string> PursuedIds)
{
    public Bt5RunEvidence ToBt5Evidence()
        => new(
            Input?.Run?.Shape?.RunId ?? string.Empty,
            Valid,
            Input?.Run?.Shape?.FailureReason ?? "decoded_run_missing",
            Input?.Run?.Shape?.HasTerminalFailure == true,
            SpontaneousIntent,
            FirstQualifyingIntentId,
            FirstQualifyingIntentIndex,
            Commits.Count > 0,
            Commits,
            Diagnostics);
}

/// <summary>BT5와 BT10이 공유하는 desire/commit 단일 truth 구현.</summary>
internal static class SealedDesireCommitEvaluation
{
    private static readonly HashSet<string> CommitSeams = new(StringComparer.Ordinal)
    {
        "reward",
        "recruit",
        "level_node",
        "refit",
    };

    public static SealedDesireCommitCohortEvaluation Evaluate(
        IReadOnlyList<SealedScoredRunInput> inputs,
        string expectedPromptSchemaHash)
    {
        var supplied = inputs?.Count ?? 0;
        var source = inputs ?? Array.Empty<SealedScoredRunInput>();
        var deduplicated = new List<SealedScoredRunInput>();
        var seenByRunId = new Dictionary<string, string>(StringComparer.Ordinal);
        var cohortFailure = string.Empty;

        foreach (var input in source)
        {
            if (input?.Run?.Shape == null)
            {
                cohortFailure = FirstFailure(cohortFailure, "decoded_run_missing");
                continue;
            }

            var runId = input.Run.Shape.RunId ?? string.Empty;
            string canonicalTrace;
            try
            {
                canonicalTrace = input.Run.Trace == null
                    ? "<null-trace>"
                    : HeadlessMetricJson.Serialize(input.Run.Trace);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                canonicalTrace = $"<unserializable:{exception.Message}>";
            }

            if (seenByRunId.TryGetValue(runId, out var previous))
            {
                if (!string.Equals(previous, canonicalTrace, StringComparison.Ordinal))
                {
                    cohortFailure = FirstFailure(
                        cohortFailure,
                        $"run_id_fork:{runId}");
                }

                continue;
            }

            seenByRunId.Add(runId, canonicalTrace);
            deduplicated.Add(input);
        }

        if (deduplicated.Count > 1)
        {
            var baseline = deduplicated[0].Run.Shape;
            for (var index = 1; index < deduplicated.Count; index++)
            {
                var candidate = deduplicated[index].Run.Shape;
                if (!SameCohortHashes(baseline, candidate))
                {
                    cohortFailure = FirstFailure(cohortFailure, "cohort_header_hash_mismatch");
                    break;
                }
            }
        }

        if (expectedPromptSchemaHash != null
            && deduplicated.Any(input => !string.Equals(
                input.Run.Shape.PromptSchemaHash,
                expectedPromptSchemaHash,
                StringComparison.Ordinal)))
        {
            cohortFailure = FirstFailure(cohortFailure, "expected_prompt_schema_hash_mismatch");
        }

        var runs = deduplicated.Select(EvaluateRun).ToArray();
        return new SealedDesireCommitCohortEvaluation(
            supplied,
            cohortFailure.Length == 0,
            cohortFailure,
            runs);
    }

    private static SealedDesireCommitRunEvaluation EvaluateRun(SealedScoredRunInput input)
    {
        var run = input.Run;
        var valid = run?.Shape?.Valid == true;
        var diagnostics = new DiagnosticBuilder();
        var ledger = new LedgerContext(input, run?.Shape?.RunId ?? string.Empty);
        var universes = BuildVisibleUniverses(run);
        var desireCommitEligible = valid && universes.Available;
        var qualifications = new Dictionary<int, IntentQualification>();
        var allTrackIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var decision in run?.Decisions ?? Array.Empty<DecodedSealedDecision>())
        {
            var intent = decision?.Response?.DeclaredIntent;
            foreach (var token in intent?.TrackTokenIds ?? Array.Empty<string>())
            {
                var id = SealedCommitJoinEvaluator.TrackTokenIdPart(token);
                if (id.Length > 0)
                {
                    allTrackIds.Add(id);
                }
            }

            var universe = universes.ByDecisionIndex.TryGetValue(
                decision.SeamKey.DecisionIndex,
                out var foundUniverse)
                ? foundUniverse
                : null;
            qualifications[decision.SeamKey.DecisionIndex] = QualifyIntent(
                desireCommitEligible,
                decision,
                intent,
                universe,
                ledger,
                diagnostics);
        }

        var firstQualifying = qualifications
            .Where(pair => pair.Value.Qualifies)
            .OrderBy(pair => pair.Key)
            .Select(pair => new { Index = pair.Key, pair.Value.Intent })
            .FirstOrDefault();
        var commits = new List<Bt5CommitWitness>();
        var pursuedIds = new HashSet<string>(StringComparer.Ordinal);

        if (desireCommitEligible)
        {
            foreach (var decision in run.Decisions.OrderBy(value => value.SeamKey.DecisionIndex))
            {
                if (!CommitSeams.Contains(decision.SeamType)
                    || decision.TerminalFailure)
                {
                    continue;
                }

                var selectedAction = EntrySelectedAction(run, decision.SeamKey.DecisionIndex);
                if (string.Equals(selectedAction, "skip", StringComparison.Ordinal))
                {
                    continue;
                }

                var applied = SealedCommitJoinEvaluator.Resolve(decision, selectedAction);
                ApplyJoinIssue(applied.Issue, diagnostics);
                if (applied.Targets.Count == 0)
                {
                    continue;
                }

                if (!TryBindIntent(
                        run.Decisions,
                        decision,
                        qualifications,
                        diagnostics,
                        out var binding))
                {
                    continue;
                }

                var exact = SealedCommitJoinEvaluator.FirstExactMatch(
                    binding.Intent.TrackTokenIds,
                    applied.Targets);
                if (exact == null)
                {
                    if (SealedCommitJoinEvaluator.HasFamilyMatch(
                            binding.Intent.TrackTokenIds,
                            applied.FamilyIds))
                    {
                        diagnostics.FamilyOnlyCommitCount++;
                    }

                    continue;
                }

                commits.Add(new Bt5CommitWitness(
                    decision.SeamKey.DecisionIndex,
                    decision.SeamType,
                    selectedAction,
                    exact.TrackToken,
                    exact.Target.WireToken,
                    binding.Intent.IntentId,
                    binding.DeclaredAtIndex));
                if (binding.DeclaredAtIndex == decision.SeamKey.DecisionIndex)
                {
                    diagnostics.SameDecisionFormationCommitCount++;
                }
                else
                {
                    diagnostics.PriorDeclarationCommitCount++;
                }

                foreach (var token in binding.Intent.TrackTokenIds ?? Array.Empty<string>())
                {
                    var id = SealedCommitJoinEvaluator.TrackTokenIdPart(token);
                    if (id.Length > 0)
                    {
                        pursuedIds.Add(id);
                    }
                }

                foreach (var target in applied.Targets)
                {
                    pursuedIds.Add(target.Id);
                }
            }
        }

        return new SealedDesireCommitRunEvaluation(
            input,
            valid,
            valid && firstQualifying != null,
            firstQualifying?.Intent?.IntentId ?? string.Empty,
            firstQualifying?.Index ?? -1,
            commits,
            diagnostics.Build(),
            universes.Available,
            universes.Last,
            allTrackIds,
            pursuedIds);
    }

    private static IntentQualification QualifyIntent(
        bool validRun,
        DecodedSealedDecision decision,
        LlmDeclaredIntentV1 intent,
        HashSet<string> visibleIds,
        LedgerContext ledger,
        DiagnosticBuilder diagnostics)
    {
        if (intent == null)
        {
            return new IntentQualification(null, false);
        }

        if (intent.Confidence > 1d || intent.Confidence < 0d)
        {
            diagnostics.ConfidenceOutOfUnitRangeCount++;
        }

        var tokens = intent.TrackTokenIds ?? Array.Empty<string>();
        var allTokensAdmissible = tokens.Count > 0;
        foreach (var token in tokens)
        {
            if (!SealedCommitJoinEvaluator.TrackTokenAdmissible(
                    token,
                    visibleIds,
                    out var unknownKind))
            {
                diagnostics.InadmissibleTrackTokenCount++;
                diagnostics.UnknownKindTrackTokenCount += unknownKind ? 1 : 0;
                allTokensAdmissible = false;
            }
        }

        var evidence = intent.EvidenceFactIds ?? Array.Empty<string>();
        var evidenceResolved = evidence.Count > 0;
        if (!ledger.TryDecisionPoint(
                decision.SeamKey.DecisionIndex,
                decision.SeamType,
                out var decisionPoint))
        {
            diagnostics.LedgerMappingFailureCount++;
            evidenceResolved = false;
        }
        else
        {
            foreach (var factId in evidence)
            {
                if (!ledger.Resolves(factId, decisionPoint))
                {
                    diagnostics.EvidenceJoinFailureCount++;
                    evidenceResolved = false;
                }
            }
        }

        var qualifies = validRun
                        && SealedWireSubstanceRules.Substantive(intent.IntentId)
                        && allTokensAdmissible
                        && SealedWireSubstanceRules.Substantive(intent.ExpectedPayoff)
                        && evidenceResolved
                        && SealedWireSubstanceRules.Substantive(intent.NextAcquisitionPlan)
                        && !string.Equals(
                            SealedWireSubstanceRules.Normalize(intent.NextAcquisitionPlan),
                            SealedWireSubstanceRules.Normalize(intent.ExpectedPayoff),
                            StringComparison.Ordinal)
                        && (intent.AllowedSubstitutions ?? Array.Empty<string>())
                            .Any(SealedWireSubstanceRules.Substantive)
                        && (intent.PivotConditions ?? Array.Empty<string>())
                            .Any(SealedWireSubstanceRules.Substantive)
                        && intent.Confidence > 0d;
        return new IntentQualification(intent, qualifies);
    }

    private static bool TryBindIntent(
        IReadOnlyList<DecodedSealedDecision> decisions,
        DecodedSealedDecision current,
        IReadOnlyDictionary<int, IntentQualification> qualifications,
        DiagnosticBuilder diagnostics,
        out IntentBinding binding)
    {
        var response = current.Response;
        var currentIntent = response?.DeclaredIntent;
        if (currentIntent != null
            && (!SealedWireSubstanceRules.Substantive(response.IntentRef)
                || string.Equals(response.IntentRef, currentIntent.IntentId, StringComparison.Ordinal)))
        {
            var index = current.SeamKey.DecisionIndex;
            if (qualifications.TryGetValue(index, out var qualification) && qualification.Qualifies)
            {
                binding = new IntentBinding(currentIntent, index);
                return true;
            }

            binding = null;
            return false;
        }

        var prior = (decisions ?? Array.Empty<DecodedSealedDecision>())
            .Where(candidate => candidate.SeamKey.DecisionIndex <= current.SeamKey.DecisionIndex)
            .OrderByDescending(candidate => candidate.SeamKey.DecisionIndex)
            .FirstOrDefault(candidate => string.Equals(
                candidate.Response?.DeclaredIntent?.IntentId,
                response?.IntentRef,
                StringComparison.Ordinal));
        if (prior == null)
        {
            diagnostics.DanglingIntentRefCount++;
            binding = null;
            return false;
        }

        var declaredAt = prior.SeamKey.DecisionIndex;
        if (!qualifications.TryGetValue(declaredAt, out var priorQualification)
            || !priorQualification.Qualifies)
        {
            binding = null;
            return false;
        }

        binding = new IntentBinding(prior.Response.DeclaredIntent, declaredAt);
        return true;
    }

    private static void ApplyJoinIssue(
        SealedCommitJoinIssue issue,
        DiagnosticBuilder diagnostics)
    {
        switch (issue)
        {
            case SealedCommitJoinIssue.ActionJoinFailure:
                diagnostics.ActionJoinFailureCount++;
                break;
            case SealedCommitJoinIssue.NoTargetRewardPick:
                diagnostics.NoTargetRewardPickCount++;
                break;
            case SealedCommitJoinIssue.RewardMechanicsHole:
                diagnostics.RewardMechanicsHoleCount++;
                break;
        }
    }

    private static VisibleUniverseResult BuildVisibleUniverses(DecodedSealedRun run)
    {
        var current = new HashSet<string>(StringComparer.Ordinal);
        var byIndex = new Dictionary<int, HashSet<string>>();
        var available = true;
        foreach (var decision in (run?.Decisions ?? Array.Empty<DecodedSealedDecision>())
                     .OrderBy(value => value.SeamKey.DecisionIndex))
        {
            if (decision.Join?.Available != true)
            {
                available = false;
            }

            if (available)
            {
                current.UnionWith(decision.Join.VisibleIds ?? Array.Empty<string>());
                byIndex[decision.SeamKey.DecisionIndex] = new HashSet<string>(current, StringComparer.Ordinal);
            }
            else
            {
                byIndex[decision.SeamKey.DecisionIndex] = null;
            }
        }

        return new VisibleUniverseResult(
            available,
            byIndex,
            available
                ? current.OrderBy(value => value, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>());
    }

    private static string EntrySelectedAction(DecodedSealedRun run, int decisionIndex)
    {
        if (run?.Trace?.Entries == null
            || decisionIndex < 0
            || decisionIndex >= run.Trace.Entries.Count)
        {
            return string.Empty;
        }

        return run.Trace.Entries[decisionIndex]?.SelectedAction ?? string.Empty;
    }

    private static bool SameCohortHashes(SealedRunShape left, SealedRunShape right)
        => string.Equals(left.BuildManifestHash, right.BuildManifestHash, StringComparison.Ordinal)
           && string.Equals(left.ContentManifestHash, right.ContentManifestHash, StringComparison.Ordinal)
           && string.Equals(left.VisibleSurfaceHash, right.VisibleSurfaceHash, StringComparison.Ordinal)
           && string.Equals(left.PromptSchemaHash, right.PromptSchemaHash, StringComparison.Ordinal)
           && string.Equals(left.ScorerConfigHash, right.ScorerConfigHash, StringComparison.Ordinal);

    private static string FirstFailure(string current, string candidate)
        => current.Length == 0 ? candidate : current;

    private sealed class LedgerContext
    {
        private readonly IReadOnlyList<PlayerVisibleFactRecord> _facts;
        private readonly IReadOnlyList<PlayerVisibleDecisionRecord> _decisions;
        private readonly bool _usable;

        public LedgerContext(SealedScoredRunInput input, string runId)
        {
            _facts = input?.Facts ?? Array.Empty<PlayerVisibleFactRecord>();
            _decisions = input?.Decisions ?? Array.Empty<PlayerVisibleDecisionRecord>();
            var allRows = _facts.Select(fact => (fact?.CampaignId, fact?.RunId, Present: fact != null))
                .Concat(_decisions.Select(decision =>
                    (decision?.CampaignId, decision?.RunId, Present: decision != null)))
                .ToArray();
            _usable = _facts.Count > 0
                      && _decisions.Count > 0
                      && allRows.All(row => row.Present)
                      && allRows.Select(row => row.CampaignId ?? string.Empty)
                          .Distinct(StringComparer.Ordinal)
                          .Count() == 1
                      && allRows.All(row => string.Equals(
                          row.RunId,
                          runId,
                          StringComparison.Ordinal));
        }

        public bool TryDecisionPoint(
            int decisionIndex,
            string seamType,
            out PlayerVisibleTimelinePoint point)
        {
            if (!_usable)
            {
                point = null;
                return false;
            }

            var matches = _decisions.Where(decision =>
                    decision?.DecidedAt != null
                    && decision.DecidedAt.DecisionIndex == decisionIndex
                    && string.Equals(decision.DecisionKind, seamType, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                point = null;
                return false;
            }

            point = matches[0].DecidedAt;
            return true;
        }

        public bool Resolves(string factId, PlayerVisibleTimelinePoint decisionPoint)
            => _usable
               && factId != null
               && decisionPoint != null
               && _facts.Any(fact =>
                   fact?.ObservedAt != null
                   && string.Equals(fact.FactId, factId, StringComparison.Ordinal)
                   && fact.ObservedAt.CompareTo(decisionPoint) <= 0);
    }

    private sealed class DiagnosticBuilder
    {
        public int InadmissibleTrackTokenCount;
        public int UnknownKindTrackTokenCount;
        public int ConfidenceOutOfUnitRangeCount;
        public int EvidenceJoinFailureCount;
        public int LedgerMappingFailureCount;
        public int NoTargetRewardPickCount;
        public int RewardMechanicsHoleCount;
        public int ActionJoinFailureCount;
        public int DanglingIntentRefCount;
        public int SameDecisionFormationCommitCount;
        public int PriorDeclarationCommitCount;
        public int FamilyOnlyCommitCount;

        public Bt5RunDiagnostics Build()
            => new(
                InadmissibleTrackTokenCount,
                UnknownKindTrackTokenCount,
                ConfidenceOutOfUnitRangeCount,
                EvidenceJoinFailureCount,
                LedgerMappingFailureCount,
                NoTargetRewardPickCount,
                RewardMechanicsHoleCount,
                ActionJoinFailureCount,
                DanglingIntentRefCount,
                SameDecisionFormationCommitCount,
                PriorDeclarationCommitCount,
                FamilyOnlyCommitCount);
    }

    private sealed record IntentQualification(LlmDeclaredIntentV1 Intent, bool Qualifies);
    private sealed record IntentBinding(LlmDeclaredIntentV1 Intent, int DeclaredAtIndex);

    private sealed record VisibleUniverseResult(
        bool Available,
        IReadOnlyDictionary<int, HashSet<string>> ByDecisionIndex,
        IReadOnlyCollection<string> Last);
}
