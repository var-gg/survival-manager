using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>Coverage/Competent policy choice, census medoid ablation, healer replacement pair를 만든다.</summary>
internal static class H100FormationCaseFactory
{
    public static IReadOnlyList<H100FormationBattleCase> Build(
        RuntimeCombatContentLookup lookup,
        BuildSpaceCensus census,
        H100FormationRunSettings settings)
    {
        var cases = new List<H100FormationBattleCase>();
        var medoids = census.Medoids.OrderBy(value => value.Placement.Signature, StringComparer.Ordinal).ToArray();
        for (var seedIndex = 0; seedIndex < settings.SeedCount; seedIndex++)
        {
            var decisionSeed = settings.SeedBase + seedIndex;
            var battleSeed = H100SessionDriver.DeriveSeed("h100-stage4-formation", decisionSeed);
            var session = H100SessionDriver.CreateSession(lookup, settings.PairingProfileId(decisionSeed));
            var observation = H100PolicyObservationBuilder.Build(session, lookup, decisionSeed);
            var coverageDecision = Decide(observation, HeadlessPolicyFactory.CoverageId);
            var competentDecision = Decide(observation, settings.CompetentPolicyId);

            AddPolicyCases(
                cases,
                observation,
                coverageDecision,
                HeadlessPolicyFactory.CoverageId,
                seedIndex,
                battleSeed,
                medoids);
            AddPolicyCases(
                cases,
                observation,
                competentDecision,
                settings.CompetentPolicyId,
                seedIndex,
                battleSeed,
                medoids);
            AddHealerCases(
                cases,
                observation,
                coverageDecision,
                competentDecision,
                settings.CompetentPolicyId,
                seedIndex,
                battleSeed);
        }

        return cases.OrderBy(value => value.CaseId, StringComparer.Ordinal).ToArray();
    }

    private static HeadlessDeploymentDecision Decide(
        HeadlessPolicyObservation observation,
        string policyId)
    {
        var policy = HeadlessPolicyFactory.Create(policyId);
        var decision = policy.DecideDeployment(observation);
        HeadlessPolicyGuard.ValidateDeploymentDecision(observation, decision);
        return decision;
    }

    private static void AddPolicyCases(
        ICollection<H100FormationBattleCase> cases,
        HeadlessPolicyObservation observation,
        HeadlessDeploymentDecision policyDecision,
        string policyId,
        int seedIndex,
        int battleSeed,
        IReadOnlyList<FormationMedoid> medoids)
    {
        var heroesById = observation.Roster.ToDictionary(hero => hero.HeroId, StringComparer.Ordinal);
        var selected = policyDecision.Placements.Select(value => heroesById[value.HeroId])
            .GroupBy(hero => hero.HeroId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var selectedObservation = SubsetObservation(observation, selected);
        var defaultDecision = new GreedyPolicy().DecideDeployment(selectedObservation);
        var buildId = string.Join("+", selected.Select(hero => hero.ArchetypeId).OrderBy(id => id, StringComparer.Ordinal));
        var placementSetId = $"{policyId}|build:{buildId}";
        var pairingId = $"{placementSetId}|seed:{battleSeed}";
        var containsHealer = selected.Any(IsHealer);

        AddCase("default", defaultDecision.Placements, isDefault: true, isPolicyChoice: false);
        AddCase("policy-choice", policyDecision.Placements, isDefault: false, isPolicyChoice: true);
        var orderedHeroes = selected.OrderBy(RoleRank).ThenBy(hero => hero.HeroId, StringComparer.Ordinal).ToArray();
        for (var medoidIndex = 0; medoidIndex < medoids.Count; medoidIndex++)
        {
            var anchors = medoids[medoidIndex].Placement.AnchorsByMemberIndex;
            var placements = orderedHeroes
                .Select((hero, index) => new HeadlessPlacement(anchors[index], hero.HeroId))
                .ToArray();
            AddCase($"medoid-{medoidIndex:D2}", placements, isDefault: false, isPolicyChoice: false);
        }

        void AddCase(
            string variantId,
            IReadOnlyList<HeadlessPlacement> placements,
            bool isDefault,
            bool isPolicyChoice)
        {
            cases.Add(new H100FormationBattleCase(
                $"policy-{PolicySlug(policyId)}-seed-{seedIndex:D2}-{variantId}",
                pairingId,
                placementSetId,
                variantId,
                policyId,
                battleSeed,
                isDefault,
                isPolicyChoice,
                false,
                string.Empty,
                containsHealer,
                false,
                BuildMembers(placements, heroesById),
                string.Empty));
        }

        if (string.Equals(policyId, HeadlessPolicyFactory.CoverageId, StringComparison.Ordinal))
        {
            var coverageMembers = BuildMembers(policyDecision.Placements, heroesById);
            foreach (var channelId in FormationChannelIds.All)
            {
                cases.Add(new H100FormationBattleCase(
                    $"policy-coverage-seed-{seedIndex:D2}-probe-{channelId}",
                    string.Empty,
                    string.Empty,
                    $"coverage-probe-{channelId}",
                    policyId,
                    battleSeed,
                    false,
                    true,
                    false,
                    string.Empty,
                    containsHealer,
                    false,
                    coverageMembers,
                    channelId));
            }
        }
    }

    private static void AddHealerCases(
        ICollection<H100FormationBattleCase> cases,
        HeadlessPolicyObservation observation,
        HeadlessDeploymentDecision coverageDecision,
        HeadlessDeploymentDecision competentDecision,
        string competentPolicyId,
        int seedIndex,
        int battleSeed)
    {
        var heroesById = observation.Roster.ToDictionary(hero => hero.HeroId, StringComparer.Ordinal);
        var selectedIds = coverageDecision.Placements.Select(value => value.HeroId).ToHashSet(StringComparer.Ordinal);
        var healerPlacement = coverageDecision.Placements
            .FirstOrDefault(value => IsHealer(heroesById[value.HeroId]));
        var replacement = observation.Roster
            .Where(hero => !selectedIds.Contains(hero.HeroId) && !IsHealer(hero))
            .OrderByDescending(ReadinessScore)
            .ThenBy(hero => hero.HeroId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (healerPlacement == null || replacement == null)
        {
            return;
        }

        var withoutHealer = coverageDecision.Placements
            .Select(value => string.Equals(value.HeroId, healerPlacement.HeroId, StringComparison.Ordinal)
                ? new HeadlessPlacement(value.Anchor, replacement.HeroId)
                : value)
            .ToArray();
        var competentSelectedHealer = competentDecision.Placements.Any(value => IsHealer(heroesById[value.HeroId]));
        var commonIds = coverageDecision.Placements
            .Where(value => !string.Equals(value.HeroId, healerPlacement.HeroId, StringComparison.Ordinal))
            .Select(value => heroesById[value.HeroId].ArchetypeId)
            .OrderBy(id => id, StringComparer.Ordinal);
        var comparisonId = $"healer:{string.Join("+", commonIds)}|{heroesById[healerPlacement.HeroId].ArchetypeId}->{replacement.ArchetypeId}";
        var pairingId = $"{comparisonId}|seed:{battleSeed}";

        cases.Add(BuildHealerCase("with-healer", coverageDecision.Placements, true));
        cases.Add(BuildHealerCase("without-healer", withoutHealer, false));

        H100FormationBattleCase BuildHealerCase(
            string variantId,
            IReadOnlyList<HeadlessPlacement> placements,
            bool containsHealer)
            => new(
                $"healer-seed-{seedIndex:D2}-{variantId}",
                pairingId,
                string.Empty,
                variantId,
                competentPolicyId,
                battleSeed,
                false,
                false,
                true,
                comparisonId,
                containsHealer,
                competentSelectedHealer,
                BuildMembers(placements, heroesById),
                string.Empty);
    }

    private static HeadlessPolicyObservation SubsetObservation(
        HeadlessPolicyObservation source,
        IReadOnlyList<HeadlessHeroObservation> roster)
        => new(
            source.DecisionSeed,
            source.DeployCapacity,
            source.ChapterId,
            source.SiteId,
            roster,
            source.Anchors,
            source.EnemyPreview,
            source.RewardOptions);

    private static IReadOnlyList<H100BattleScreeningMember> BuildMembers(
        IEnumerable<HeadlessPlacement> placements,
        IReadOnlyDictionary<string, HeadlessHeroObservation> heroesById)
        => placements.OrderBy(value => value.Anchor)
            .Select(value => new H100BattleScreeningMember(heroesById[value.HeroId].ArchetypeId, value.Anchor))
            .ToArray();

    private static int RoleRank(HeadlessHeroObservation hero)
        => hero.ClassId switch
        {
            "vanguard" => 0,
            "duelist" => 1,
            "ranger" => 2,
            "mystic" => 3,
            _ => 4,
        };

    private static bool IsHealer(HeadlessHeroObservation hero)
        => string.Equals(hero.ClassId, "mystic", StringComparison.Ordinal)
           || hero.RoleTag.Contains("heal", StringComparison.OrdinalIgnoreCase)
           || hero.RoleTag.Contains("support", StringComparison.OrdinalIgnoreCase);

    private static double ReadinessScore(HeadlessHeroObservation hero)
        => (hero.Level * 2d)
           + (hero.EquippedItemCount * 4d)
           + (hero.MaxHp <= 0 ? 8d : (double)hero.CurrentHp / hero.MaxHp * 8d);

    private static string PolicySlug(string policyId)
        => policyId.Replace("competent-", string.Empty, StringComparison.Ordinal)
            .Replace("qa-", string.Empty, StringComparison.Ordinal)
            .Replace("-v1", string.Empty, StringComparison.Ordinal);
}
