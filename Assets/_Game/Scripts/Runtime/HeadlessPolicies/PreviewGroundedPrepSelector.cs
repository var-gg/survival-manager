using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessPolicies;

internal sealed class PreviewGroundedPrepSelection
{
    public PreviewGroundedPrepSelection(
        IReadOnlyList<HeadlessPlacement> placements,
        IReadOnlyList<HeadlessEquipmentAssignment> equipmentAssignments,
        double estimatedValue,
        string rationale,
        IReadOnlyList<string> evidenceSignals)
    {
        Placements = placements;
        EquipmentAssignments = equipmentAssignments;
        EstimatedValue = estimatedValue;
        Rationale = rationale;
        EvidenceSignals = evidenceSignals;
    }

    public IReadOnlyList<HeadlessPlacement> Placements { get; }
    public IReadOnlyList<HeadlessEquipmentAssignment> EquipmentAssignments { get; }
    public double EstimatedValue { get; }
    public string Rationale { get; }
    public IReadOnlyList<string> EvidenceSignals { get; }
}

/// <summary>현재 공개 preview와 현재 편성만으로 bounded prep 후보를 비교한다.</summary>
internal static class PreviewGroundedPrepSelector
{
    public static PreviewGroundedPrepSelection Select(HeadlessPolicyObservation observation)
    {
        var current = observation.CurrentPlacements.OrderBy(value => value.Anchor).ToArray();
        var currentIds = current.Select(value => value.HeroId).ToHashSet(StringComparer.Ordinal);
        var currentHeroes = observation.Roster.Where(value => currentIds.Contains(value.HeroId)).ToArray();
        var profile = EnemyThreatProfileParser.Parse(
            EnemyThreatObservation.FromVisiblePreview(observation.EnemyPreview));
        if (!observation.EnemyPreview.IsAvailable || profile.Tags.Count == 0)
        {
            return Hold(
                observation,
                current,
                "preview_unavailable_or_no_visible_threat",
                PreviewGroundedEquipmentSelector.Select(observation, current));
        }

        var heroSets = new List<IReadOnlyList<HeadlessHeroObservation>> { currentHeroes };
        var bench = observation.Roster.Where(value => !currentIds.Contains(value.HeroId)).ToArray();
        foreach (var removed in currentHeroes)
        foreach (var added in bench)
        {
            heroSets.Add(currentHeroes.Where(value => value.HeroId != removed.HeroId).Append(added).ToArray());
        }

        Candidate best = null;
        foreach (var heroes in heroSets)
        {
            var connections = PreviewGroundedCounterRules.Connect(profile, heroes);
            var target = PreviewGroundedFormationSelector.Select(profile, heroes, observation.Anchors, connections);
            var targetByHero = target.Placements.ToDictionary(value => value.HeroId, value => value.Anchor, StringComparer.Ordinal);
            foreach (var placements in HeadlessPolicyScoring.EnumeratePlacements(heroes, observation.Anchors))
            {
                var byHero = placements.ToDictionary(value => value.HeroId, value => value.Anchor, StringComparer.Ordinal);
                var retainedMoves = current
                    .Where(value => byHero.ContainsKey(value.HeroId))
                    .Count(value => byHero[value.HeroId] != value.Anchor);
                if (retainedMoves > HeadlessPrepPolicyGuard.MaximumFormationEdits)
                {
                    continue;
                }

                var replacements = current.Count(value => !byHero.ContainsKey(value.HeroId));
                if (replacements > HeadlessPrepPolicyGuard.MaximumBenchSwaps)
                {
                    continue;
                }

                var targetMatches = placements.Count(value => targetByHero.TryGetValue(value.HeroId, out var anchor) && anchor == value.Anchor);
                var coveredThreats = connections.Select(value => value.ThreatTag).Distinct(StringComparer.Ordinal).Count();
                var targetMatchValue = target.RuleId == PreviewGroundedFormationSelector.BacklineDiveScreenRule
                    ? 24d
                    : 6d;
                var visibleValue = HeadlessPolicyScoring.EvaluateDeployment(observation, heroes, placements)
                                   + (coveredThreats * 24d)
                                   + (targetMatches * targetMatchValue);
                var candidate = new Candidate(
                    placements.OrderBy(value => value.Anchor).ToArray(),
                    visibleValue,
                    coveredThreats,
                    targetMatches,
                    replacements,
                    retainedMoves,
                    target.RuleId,
                    connections);
                if (best == null || candidate.IsBetterThan(best))
                {
                    best = candidate;
                }
            }
        }

        if (best == null || SameFormation(current, best.Placements))
        {
            return Hold(
                observation,
                current,
                "bounded_search_prefers_hold",
                PreviewGroundedEquipmentSelector.Select(observation, current));
        }

        var equipment = PreviewGroundedEquipmentSelector.Select(observation, best.Placements);
        var signals = best.Connections
            .SelectMany(value => new[] { value.ThreatEvidenceSignalKey, value.HeroEvidenceSignalKey })
            .Append(HeadlessPolicyEvidence.DeploymentSurfaceSignal)
            .Append(HeadlessPolicyEvidence.RosterSurfaceSignal)
            .Append(HeadlessPolicyEvidence.EnemyPreviewSignal)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new PreviewGroundedPrepSelection(
            best.Placements,
            equipment.Assignments,
            best.VisibleValue + equipment.EstimatedValue,
            AppendEquipmentDetail(
                $"threats={string.Join(",", profile.Tags)};covered={best.CoveredThreats};formation_rule={best.FormationRule};formation_edits={best.FormationEdits};bench_swaps={best.BenchSwaps}",
                equipment),
            signals);
    }

    private static PreviewGroundedPrepSelection Hold(
        HeadlessPolicyObservation observation,
        IReadOnlyList<HeadlessPlacement> current,
        string reason,
        PreviewGroundedEquipmentSelection equipment = null)
        => new(
            current,
            (equipment ?? PreviewGroundedEquipmentSelection.None).Assignments,
            HeadlessPolicyScoring.EvaluateDeployment(
                observation,
                observation.Roster.Where(hero => current.Any(value => value.HeroId == hero.HeroId)).ToArray(),
                current) + (equipment ?? PreviewGroundedEquipmentSelection.None).EstimatedValue,
            AppendEquipmentDetail(reason, equipment ?? PreviewGroundedEquipmentSelection.None),
            new[]
            {
                HeadlessPolicyEvidence.DeploymentSurfaceSignal,
                HeadlessPolicyEvidence.RosterSurfaceSignal,
                HeadlessPolicyEvidence.EnemyPreviewSignal,
            });

    private static string AppendEquipmentDetail(
        string rationale,
        PreviewGroundedEquipmentSelection equipment)
        => equipment.Assignments.Count == 0
            ? rationale
            : $"{rationale};gear_counter={equipment.Detail}";

    private static bool SameFormation(
        IReadOnlyList<HeadlessPlacement> left,
        IReadOnlyList<HeadlessPlacement> right)
        => string.Equals(
            HeadlessPolicyScoring.PlacementSignature(left),
            HeadlessPolicyScoring.PlacementSignature(right),
            StringComparison.Ordinal);

    private sealed class Candidate
    {
        public Candidate(
            IReadOnlyList<HeadlessPlacement> placements,
            double visibleValue,
            int coveredThreats,
            int targetMatches,
            int benchSwaps,
            int formationEdits,
            string formationRule,
            IReadOnlyList<PreviewCounterConnection> connections)
        {
            Placements = placements;
            VisibleValue = visibleValue;
            CoveredThreats = coveredThreats;
            TargetMatches = targetMatches;
            BenchSwaps = benchSwaps;
            FormationEdits = formationEdits;
            FormationRule = formationRule;
            Connections = connections;
        }

        public IReadOnlyList<HeadlessPlacement> Placements { get; }
        public double VisibleValue { get; }
        public int CoveredThreats { get; }
        public int TargetMatches { get; }
        public int BenchSwaps { get; }
        public int FormationEdits { get; }
        public string FormationRule { get; }
        public IReadOnlyList<PreviewCounterConnection> Connections { get; }

        public bool IsBetterThan(Candidate other)
            => VisibleValue > other.VisibleValue + 0.000001d
               || Math.Abs(VisibleValue - other.VisibleValue) <= 0.000001d
               && (CoveredThreats > other.CoveredThreats
                   || CoveredThreats == other.CoveredThreats && BenchSwaps < other.BenchSwaps
                   || CoveredThreats == other.CoveredThreats && BenchSwaps == other.BenchSwaps
                   && string.CompareOrdinal(
                       HeadlessPolicyScoring.PlacementSignature(Placements),
                       HeadlessPolicyScoring.PlacementSignature(other.Placements)) < 0);
    }
}
