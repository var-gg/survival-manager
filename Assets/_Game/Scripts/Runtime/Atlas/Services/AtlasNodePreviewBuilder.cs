using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;

namespace SM.Atlas.Services;

public sealed class AtlasNodePreviewBuilder
{
    private const string BoundaryNote = "Sigil changes node reward, threat, and affinity surfaces only.";

    public AtlasNodePreview Build(
        AtlasRegionDefinition region,
        AtlasRegionNode node,
        AtlasNodeModifierStack stack,
        string stageCandidatePathHash,
        string runId,
        string chapterId,
        string siteId,
        string encounterId,
        string cycleSalt,
        string squadSnapshotId)
    {
        var nodeOverlayHash = AtlasContextHasher.BuildNodeOverlayHash(region.RegionId, node, cycleSalt, stack.Influences);
        var battleContextHash = AtlasContextHasher.BuildBattleContextHash(
            runId,
            chapterId,
            siteId,
            node.SiteNodeIndex >= 0 ? node.SiteNodeIndex : 0,
            encounterId,
            stageCandidatePathHash,
            nodeOverlayHash,
            squadSnapshotId);

        return new AtlasNodePreview(
            node.NodeId,
            node.Label,
            BuildJudgementLine(node, stack),
            node.EnemyPreview,
            BuildRewardPreview(node, stack),
            BoundaryNote,
            stack.ResolvedModifiers,
            Recommend(region.Roster, node, stack),
            stageCandidatePathHash,
            nodeOverlayHash,
            battleContextHash);
    }

    private static string BuildJudgementLine(AtlasRegionNode node, AtlasNodeModifierStack stack)
    {
        var risk = stack.ThreatPressurePercent >= 32 || node.Kind == AtlasNodeKind.Boss
            ? "High risk"
            : stack.ThreatPressurePercent >= 15 || node.Kind == AtlasNodeKind.Elite
                ? "Risky"
                : "Stable";
        var reward = stack.RewardBiasPercent > 0 ? $"Reward +{stack.RewardBiasPercent}%" : "baseline reward";
        var answer = string.IsNullOrWhiteSpace(node.AnswerLane) ? "flex answer" : node.AnswerLane;
        return $"{risk} / {reward} / {answer}";
    }

    private static string BuildRewardPreview(AtlasRegionNode node, AtlasNodeModifierStack stack)
    {
        var family = string.IsNullOrWhiteSpace(node.RewardFamily) ? "standard cache" : node.RewardFamily;
        return stack.RewardBiasPercent > 0
            ? $"{family} biased by +{stack.RewardBiasPercent}%"
            : family;
    }

    private static IReadOnlyList<AtlasRecommendedCharacter> Recommend(
        IReadOnlyList<AtlasCharacterPreview> roster,
        AtlasRegionNode node,
        AtlasNodeModifierStack stack)
    {
        return roster
            .Select(character =>
            {
                var score = 10;
                var reasons = new List<string>();
                if (string.Equals(character.AnswerLane, node.AnswerLane, StringComparison.OrdinalIgnoreCase))
                {
                    score += 35;
                    reasons.Add("answer lane");
                }

                foreach (var affinity in character.Affinities)
                {
                    if (ContainsToken(node.RewardFamily, affinity) || ContainsToken(node.EnemyPreview, affinity) || ContainsToken(node.Label, affinity))
                    {
                        score += 12;
                        reasons.Add(affinity);
                    }
                }

                if (stack.AffinityBoostPercent > 0)
                {
                    score += stack.AffinityBoostPercent / 2;
                    reasons.Add("sigil affinity");
                }

                if (node.Kind == AtlasNodeKind.Boss && ContainsToken(character.Role, "guard"))
                {
                    score += 8;
                    reasons.Add("boss anchor");
                }

                return new AtlasRecommendedCharacter(
                    character.CharacterId,
                    character.DisplayName,
                    character.Role,
                    reasons.Count == 0 ? "flex pick" : string.Join(", ", reasons.Distinct(StringComparer.OrdinalIgnoreCase)),
                    score);
            })
            .OrderByDescending(character => character.Score)
            .ThenBy(character => character.CharacterId, StringComparer.Ordinal)
            .Take(3)
            .ToArray();
    }

    private static bool ContainsToken(string value, string token)
    {
        return !string.IsNullOrWhiteSpace(value)
               && !string.IsNullOrWhiteSpace(token)
               && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
