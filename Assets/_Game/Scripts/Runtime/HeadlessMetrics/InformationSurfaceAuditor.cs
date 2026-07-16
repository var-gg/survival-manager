using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>actionable truth edge와 E01 visible semantic surface를 fail-closed로 대조한다.</summary>
public static class InformationSurfaceAuditor
{
    public static InformationSurfaceAuditResult Audit(InformationSurfaceAuditInput input)
    {
        input ??= InformationSurfaceAuditInput.Empty;
        var actionable = (input.TruthEdges ?? Array.Empty<BuildGrammarAuditEdge>())
            .Where(edge => edge != null && edge.Actionable)
            .OrderBy(edge => edge.EdgeId, StringComparer.Ordinal)
            .ToArray();
        var visible = (input.VisibleSemantics ?? Array.Empty<PlayerVisibleBuildGrammarSemantic>())
            .Where(semantic => semantic != null)
            .OrderBy(semantic => semantic.SubjectKind, StringComparer.Ordinal)
            .ThenBy(semantic => semantic.SubjectId, StringComparer.Ordinal)
            .ThenBy(semantic => semantic.Relation, StringComparer.Ordinal)
            .ThenBy(semantic => semantic.TargetKind, StringComparer.Ordinal)
            .ThenBy(semantic => semantic.TargetId, StringComparer.Ordinal)
            .ThenBy(semantic => semantic.SourceFactId, StringComparer.Ordinal)
            .ToArray();

        var missingEdges = new List<BuildGrammarAuditEdge>();
        var mismatches = new List<BuildGrammarAuditEdge>();
        foreach (var edge in actionable)
        {
            var matches = visible.Where(semantic => semantic.AvailableBeforeChoice && SameIdentity(edge, semantic))
                .ToArray();
            if (matches.Length == 0)
            {
                missingEdges.Add(edge);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(edge.TruthValue)
                && matches.All(semantic => !string.Equals(
                    semantic.VisibleValue,
                    edge.TruthValue,
                    StringComparison.Ordinal)))
            {
                mismatches.Add(edge);
            }
        }

        var gaps = new List<InformationSurfaceGap>();
        foreach (var group in missingEdges.GroupBy(
                     edge => $"{edge.SubjectKind}|{edge.SubjectId}",
                     StringComparer.Ordinal)
                 .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var first = group.OrderBy(edge => edge.EdgeId, StringComparer.Ordinal).First();
            var missing = string.Join(
                ", ",
                group.Select(edge => $"{edge.Relation}:{edge.TargetKind}:{edge.TargetId}")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal));
            gaps.Add(new InformationSurfaceGap(
                InformationSurfaceGapKind.ActionableOfferMissingSemantics,
                first.SubjectId,
                $"pre-choice surface lacks {missing}",
                Remedy(first.SubjectKind, "pre-choice mechanics and acquisition semantics")));
        }

        foreach (var edge in missingEdges.Where(edge => string.Equals(
                     edge.Relation,
                     "requires",
                     StringComparison.Ordinal)))
        {
            var truthDetail = string.IsNullOrWhiteSpace(edge.TruthValue)
                ? string.Empty
                : $" ({edge.TruthValue})";
            gaps.Add(new InformationSurfaceGap(
                InformationSurfaceGapKind.HiddenPrerequisite,
                edge.SubjectId,
                $"required {edge.TargetKind}:{edge.TargetId}{truthDetail} is absent before selection",
                Remedy(edge.SubjectKind, "prerequisite condition and threshold")));
        }

        foreach (var edge in mismatches)
        {
            var observed = visible.Where(semantic => semantic.AvailableBeforeChoice && SameIdentity(edge, semantic))
                .Select(semantic => semantic.VisibleValue)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal);
            gaps.Add(new InformationSurfaceGap(
                InformationSurfaceGapKind.DescriptionBehaviorMismatch,
                edge.SubjectId,
                $"truth={edge.TruthValue}; visible={string.Join("|", observed)}",
                Remedy(edge.SubjectKind, "visible mechanics value aligned to runtime truth")));
        }

        var tokens = (input.VisibleTokens ?? Array.Empty<PlayerVisibleTokenUse>())
            .Where(token => token != null
                            && !string.IsNullOrWhiteSpace(token.TokenKind)
                            && !string.IsNullOrWhiteSpace(token.TokenId))
            .OrderBy(token => token.TokenKind, StringComparer.Ordinal)
            .ThenBy(token => token.TokenId, StringComparer.Ordinal)
            .ThenBy(token => token.SourceFactId, StringComparer.Ordinal)
            .ToArray();
        var definitions = tokens.Where(token => token.IsDefinition)
            .Select(token => $"{token.TokenKind}|{token.TokenId}")
            .ToHashSet(StringComparer.Ordinal);
        foreach (var group in tokens.Where(token => !token.IsDefinition)
                     .GroupBy(token => $"{token.TokenKind}|{token.TokenId}", StringComparer.Ordinal)
                     .Where(group => !definitions.Contains(group.Key))
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var token = group.First();
            gaps.Add(new InformationSurfaceGap(
                InformationSurfaceGapKind.UndefinedVisibleToken,
                token.TokenId,
                $"visible {token.TokenKind}:{token.TokenId} has no reachable definition",
                "owner=content-ui; add a player-facing token definition or replace the raw id"));
        }

        var feedbackEdges = actionable.Where(edge => edge.FeedbackRequired).ToArray();
        var availableWitnesses = (input.AvailableFeedbackWitnesses ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        var witnessed = feedbackEdges.Where(edge => !string.IsNullOrWhiteSpace(edge.ExpectedFeedbackWitness)
                                                    && availableWitnesses.Contains(edge.ExpectedFeedbackWitness))
            .ToArray();
        foreach (var edge in feedbackEdges.Except(witnessed).OrderBy(edge => edge.EdgeId, StringComparer.Ordinal))
        {
            gaps.Add(new InformationSurfaceGap(
                InformationSurfaceGapKind.InteractionFeedbackMissing,
                edge.SubjectId,
                string.IsNullOrWhiteSpace(edge.ExpectedFeedbackWitness)
                    ? $"{edge.Relation}:{edge.TargetKind}:{edge.TargetId} has no attributed feedback channel"
                    : $"feedback witness unavailable: {edge.ExpectedFeedbackWitness}",
                $"owner=combat-presentation/{edge.SubjectKind}; attribute a post-attempt combat log or recap witness"));
        }

        var orderedGaps = gaps
            .OrderBy(gap => gap.Kind, StringComparer.Ordinal)
            .ThenBy(gap => gap.SubjectId, StringComparer.Ordinal)
            .ThenBy(gap => gap.Missing, StringComparer.Ordinal)
            .ToArray();
        return new InformationSurfaceAuditResult
        {
            ActionableEdgeCount = actionable.Length,
            ActionableSubjectCount = actionable.Select(edge => $"{edge.SubjectKind}|{edge.SubjectId}")
                .Distinct(StringComparer.Ordinal)
                .Count(),
            VisibleSemanticCount = visible.Length,
            VisibleTokenCount = tokens.Length,
            ActionableOfferMissingSemantics = orderedGaps.Count(gap => gap.Kind == InformationSurfaceGapKind.ActionableOfferMissingSemantics),
            UndefinedVisibleToken = orderedGaps.Count(gap => gap.Kind == InformationSurfaceGapKind.UndefinedVisibleToken),
            HiddenPrerequisite = orderedGaps.Count(gap => gap.Kind == InformationSurfaceGapKind.HiddenPrerequisite),
            DescriptionBehaviorMismatchCount = orderedGaps.Count(gap => gap.Kind == InformationSurfaceGapKind.DescriptionBehaviorMismatch),
            FeedbackRequiredEdgeCount = feedbackEdges.Length,
            FeedbackWitnessedEdgeCount = witnessed.Length,
            InteractionFeedbackCoverage = feedbackEdges.Length == 0
                ? 1d
                : Math.Round((double)witnessed.Length / feedbackEdges.Length, 6, MidpointRounding.AwayFromZero),
            Gaps = orderedGaps,
        };
    }

    private static bool SameIdentity(
        BuildGrammarAuditEdge edge,
        PlayerVisibleBuildGrammarSemantic semantic)
        => string.Equals(edge.SubjectKind, semantic.SubjectKind, StringComparison.Ordinal)
           && string.Equals(edge.SubjectId, semantic.SubjectId, StringComparison.Ordinal)
           && string.Equals(edge.Relation, semantic.Relation, StringComparison.Ordinal)
           && string.Equals(edge.TargetKind, semantic.TargetKind, StringComparison.Ordinal)
           && string.Equals(edge.TargetId, semantic.TargetId, StringComparison.Ordinal);

    private static string Remedy(string subjectKind, string detail)
        => $"owner=content-ui/{subjectKind}; expose {detail} in the selectable-offer surface";
}
