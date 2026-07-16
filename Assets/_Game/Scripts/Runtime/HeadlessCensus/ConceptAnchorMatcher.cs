using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.HeadlessCensus;

/// <summary>owner fantasy id를 recipe가 아닌 시스템 motif predicate에 매핑한다.</summary>
internal static class ConceptAnchorMatcher
{
    public static IReadOnlyList<ConceptCandidate> Match(
        OwnerConceptAnchor anchor,
        IEnumerable<ConceptCandidate> candidates)
    {
        Func<ConceptCandidate, bool> predicate = anchor.AnchorId switch
        {
            "anchor_reaper_legion" => candidate => Doctrine(candidate, TeamRuleSet.DeathTollRuleId),
            "anchor_wither_spiral" => candidate => Effect(candidate)
                                                       && Amplifies(candidate, "status_potency")
                                                       && StatusPayoff(candidate, "bleed", "burn", "marked", "slow", "sunder"),
            "anchor_iron_line" => candidate => Doctrine(candidate, TeamRuleSet.BulwarkRuleId, TeamRuleSet.PhalanxRuleId)
                                                  && candidate.ProtectedSlotCount >= 1,
            "anchor_spearpoint" => candidate => Doctrine(candidate, TeamRuleSet.ExecuteRuleId)
                                                   && candidate.Fingerprint.FormationProfile == ConceptFormationProfile.ForwardSpear,
            "anchor_arrow_storm" => candidate => Doctrine(candidate, TeamRuleSet.KillzoneRuleId)
                                                    && candidate.Fingerprint.FormationProfile is ConceptFormationProfile.FortifiedLine
                                                        or ConceptFormationProfile.ScreenedBackline,
            "anchor_decisive_blow" => candidate => Doctrine(candidate, TeamRuleSet.ExecuteRuleId),
            "anchor_snare_net" => candidate => Effect(candidate)
                                                  && StatusPayoff(candidate, "root", "silence", "slow", "stun"),
            "anchor_undying_light" => candidate => Effect(candidate)
                                                      && Payoff(candidate, "barrier", "healing")
                                                      && candidate.ProtectedSlotCount >= 1,
            "anchor_bait_and_trap" => candidate => Doctrine(candidate, TeamRuleSet.PhalanxRuleId)
                                                       && candidate.Fingerprint.FormationProfile is ConceptFormationProfile.BaitedGap
                                                           or ConceptFormationProfile.ScreenedBackline,
            "anchor_all_in_carry" => candidate => Effect(candidate)
                                                     && Payoff(candidate, "damage")
                                                     && (candidate.Fingerprint.AmplifierShape.Contains("source=item", StringComparison.Ordinal)
                                                         || candidate.Fingerprint.AmplifierShape.Contains("source=passive", StringComparison.Ordinal)),
            _ => _ => false,
        };
        return (candidates ?? Array.Empty<ConceptCandidate>())
            .Where(predicate)
            .OrderBy(candidate => candidate.Fingerprint.Signature, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Recipe.RecipeId, StringComparer.Ordinal)
            .ToArray();
    }

    public static string MappingLabel(ConceptFingerprint fingerprint)
    {
        if (!string.IsNullOrWhiteSpace(fingerprint.DoctrineRuleId))
        {
            return $"{fingerprint.MotifKind}:{fingerprint.ThresholdTag}@{fingerprint.Threshold}->{fingerprint.DoctrineRuleId}->{fingerprint.PayoffWitness}";
        }

        return $"{fingerprint.MotifKind}:{fingerprint.EnablerShape}->{fingerprint.AmplifierShape}->{fingerprint.PayoffShape}";
    }

    private static bool Doctrine(ConceptCandidate candidate, params string[] ruleIds)
        => candidate.Fingerprint.MotifKind == ConceptMotifEnumerator.ThresholdDoctrineMotif
           && ruleIds.Contains(candidate.Fingerprint.DoctrineRuleId, StringComparer.Ordinal);

    private static bool Effect(ConceptCandidate candidate)
        => candidate.Fingerprint.MotifKind == ConceptMotifEnumerator.EnablerAmplifierMotif;

    private static bool Amplifies(ConceptCandidate candidate, string statId)
        => candidate.Fingerprint.AmplifierShape.StartsWith($"stat:{statId};", StringComparison.Ordinal);

    private static bool Payoff(ConceptCandidate candidate, params string[] targetIds)
        => targetIds.Any(target => candidate.Fingerprint.PayoffShape.EndsWith($":{target}", StringComparison.Ordinal));

    private static bool StatusPayoff(ConceptCandidate candidate, params string[] statusIds)
        => candidate.Fingerprint.PayoffShape.StartsWith("produces:status:", StringComparison.Ordinal)
           && Payoff(candidate, statusIds);
}
