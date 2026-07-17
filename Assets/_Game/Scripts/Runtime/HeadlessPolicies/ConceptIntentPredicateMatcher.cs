using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.HeadlessPolicies;

/// <summary>문자열 intent 술어를 player-visible observation에만 대조하는 정책 내부 matcher.</summary>
internal static class ConceptIntentPredicateMatcher
{
    private static readonly HashSet<string> GenericSemanticTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "build", "count", "count_tag", "contains", "contains_tag", "contains_status", "owned",
        "effect", "ready", "acquire", "activate", "deploy", "formation", "status", "combat_effect",
        "telemetry", "beat", "rule", "team", "visible", "identity", "milestone", "any", "legal",
    };

    public static int IdentityProgress(
        HeadlessConceptIntent intent,
        IReadOnlyList<HeadlessHeroObservation> heroes,
        HeadlessPolicyObservation observation,
        IReadOnlyList<HeadlessPlacement> placements)
        => intent.IdentityPredicates.Sum(predicate => PredicateProgress(predicate, heroes, observation, placements));

    public static IReadOnlyList<string> CompletedMilestones(
        HeadlessConceptIntent intent,
        IReadOnlyList<HeadlessHeroObservation> heroes,
        HeadlessPolicyObservation observation,
        IReadOnlyList<HeadlessPlacement> placements)
        => intent.ProgressMilestones
            .Where(milestone => MilestoneSatisfied(milestone, heroes, observation, placements))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    public static int SubstitutionMatches(
        HeadlessConceptIntent intent,
        IReadOnlyList<HeadlessHeroObservation> heroes)
        => intent.AllowedSubstitutions.Count(token => heroes.Any(hero => HeroMatchesToken(hero, token)));

    public static bool IsAnnihilationRisk(HeadlessPolicyObservation observation)
    {
        if (observation.EnemyPreview.IsAvailable && observation.EnemyPreview.ThreatSkulls >= 4)
        {
            return true;
        }

        var deployedWithHealth = observation.Roster
            .Where(hero => hero.IsDeployed && hero.MaxHp > 0)
            .ToArray();
        return deployedWithHealth.Length > 0
               && deployedWithHealth.Count(hero => (double)hero.CurrentHp / hero.MaxHp <= 0.25d)
               * 2 >= deployedWithHealth.Length;
    }

    public static int CounterSafety(
        IReadOnlyList<HeadlessHeroObservation> heroes,
        HeadlessEnemyPreview preview)
    {
        var score = heroes.Count(hero => string.Equals(hero.ClassId, "vanguard", StringComparison.Ordinal)
                                         || ContainsAny(hero.RoleTag, "support", "healer", "anchor"));
        if (!preview.IsAvailable)
        {
            return score;
        }

        foreach (var hero in heroes)
        {
            foreach (var enemy in preview.Units)
            {
                score += (hero.ClassId, enemy.ClassId) switch
                {
                    ("mystic", "vanguard") => 1,
                    ("vanguard", "duelist") => 1,
                    ("duelist", "ranger") => 1,
                    ("ranger", "mystic") => 1,
                    _ => 0,
                };
            }
        }

        return score;
    }

    public static int RewardPrimaryMatches(HeadlessConceptIntent intent, HeadlessRewardOption option)
        => CountMechanicsMatches(
            intent.IdentityPredicates
                .Concat(intent.ProgressMilestones)
                .Append(intent.PayoffWitnessId),
            option);

    public static int RewardSubstitutionMatches(HeadlessConceptIntent intent, HeadlessRewardOption option)
        => CountMechanicsMatches(intent.AllowedSubstitutions, option);

    public static int RewardCounterMatches(HeadlessConceptIntent intent, HeadlessRewardOption option)
    {
        var matches = CountMechanicsMatches(intent.CounterAffordances, option);
        if (matches > 0)
        {
            return matches;
        }

        var mechanics = RewardMechanics(option);
        return mechanics.Count(value => ContainsAny(value, "guard", "shield", "barrier", "cleanse", "control", "range", "dive", "sustain"));
    }

    public static IReadOnlyList<string> RewardCompletedMilestones(
        HeadlessConceptIntent intent,
        HeadlessRewardOption option)
    {
        var mechanics = RewardMechanics(option);
        return intent.ProgressMilestones
            .Where(milestone => milestone.StartsWith("acquire:", StringComparison.Ordinal)
                                && SemanticTokens(new[] { milestone }).Any(token => MechanicsContains(mechanics, token)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static int PredicateProgress(
        string predicate,
        IReadOnlyList<HeadlessHeroObservation> heroes,
        HeadlessPolicyObservation observation,
        IReadOnlyList<HeadlessPlacement> placements)
    {
        if (TryParseCountIdentity(predicate, out var tag, out var threshold))
        {
            return Math.Min(threshold, heroes.Count(hero => HasTag(hero, tag)));
        }

        const string containsTag = "build.contains_tag:";
        if (predicate.StartsWith(containsTag, StringComparison.Ordinal))
        {
            return heroes.Any(hero => HasTag(hero, predicate.Substring(containsTag.Length))) ? 1 : 0;
        }

        const string containsStatus = "build.contains_status:";
        if (predicate.StartsWith(containsStatus, StringComparison.Ordinal))
        {
            return heroes.Any(hero => HeroHasStatus(hero, predicate.Substring(containsStatus.Length))) ? 1 : 0;
        }

        if (predicate.StartsWith("owned:", StringComparison.Ordinal))
        {
            return observation.Roster.Any(hero => HeroMatchesToken(hero, predicate.Substring("owned:".Length))) ? 1 : 0;
        }

        const string effectReady = "effect.ready:";
        if (predicate.StartsWith(effectReady, StringComparison.Ordinal))
        {
            var token = predicate.Substring(effectReady.Length);
            return heroes.Any(hero => HeroMatchesToken(hero, token)) ? 1 : 0;
        }

        const string teamRule = "build.team_rule=";
        if (predicate.StartsWith(teamRule, StringComparison.Ordinal))
        {
            return TeamRuleSatisfied(predicate.Substring(teamRule.Length), heroes, observation) ? 1 : 0;
        }

        if (PolicyFormationEvaluator.IsFormationPredicate(predicate))
        {
            return PolicyFormationEvaluator.Satisfies(predicate, heroes, placements) ? 1 : 0;
        }

        return 0;
    }

    private static bool MilestoneSatisfied(
        string milestone,
        IReadOnlyList<HeadlessHeroObservation> heroes,
        HeadlessPolicyObservation observation,
        IReadOnlyList<HeadlessPlacement> placements)
    {
        if (TryParseCountMilestone(milestone, out var tag, out var required))
        {
            return heroes.Count(hero => HasTag(hero, tag)) >= required;
        }

        if (milestone.StartsWith("build.team_rule=", StringComparison.Ordinal))
        {
            return TeamRuleSatisfied(milestone.Substring("build.team_rule=".Length), heroes, observation);
        }

        if (milestone.StartsWith("acquire:", StringComparison.Ordinal))
        {
            return observation.Roster.Any(hero => HeroMatchesToken(hero, milestone.Substring("acquire:".Length)));
        }

        if (milestone.StartsWith("deploy.status:", StringComparison.Ordinal))
        {
            return heroes.Any(hero => HeroHasStatus(hero, milestone.Substring("deploy.status:".Length)));
        }

        if (PolicyFormationEvaluator.IsFormationPredicate(milestone))
        {
            return PolicyFormationEvaluator.Satisfies(milestone, heroes, placements);
        }

        // activate:*는 전투 payoff 이후에만 참이 될 수 있으므로 선택 시점에는 완료로 추정하지 않는다.
        return false;
    }

    private static bool TeamRuleSatisfied(
        string ruleId,
        IReadOnlyList<HeadlessHeroObservation> heroes,
        HeadlessPolicyObservation observation)
        => observation.SynergyCatalog.Any(synergy =>
            synergy.Tiers.Any(tier => string.Equals(tier.GrantedTeamRuleId, ruleId, StringComparison.Ordinal)
                                      && heroes.Count(hero => HasTag(hero, synergy.CountedTagId)) >= tier.Threshold));

    private static bool TryParseCountIdentity(string value, out string tag, out int threshold)
    {
        tag = string.Empty;
        threshold = 0;
        const string prefix = "build.count_tag(";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var close = value.IndexOf(')', prefix.Length);
        if (close < 0 || !value.Substring(close + 1).StartsWith(">=", StringComparison.Ordinal))
        {
            return false;
        }

        tag = value.Substring(prefix.Length, close - prefix.Length);
        return int.TryParse(value.Substring(close + 3), out threshold);
    }

    private static bool TryParseCountMilestone(string value, out string tag, out int required)
    {
        tag = string.Empty;
        required = 0;
        const string prefix = "build.count_tag(";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var close = value.IndexOf(')', prefix.Length);
        if (close < 0 || close + 1 >= value.Length || value[close + 1] != '=')
        {
            return false;
        }

        tag = value.Substring(prefix.Length, close - prefix.Length);
        var countText = value.Substring(close + 2).Split('/')[0];
        return int.TryParse(countText, out required);
    }

    private static bool HeroMatchesToken(HeadlessHeroObservation hero, string token)
    {
        var normalized = token.StartsWith("owned:", StringComparison.Ordinal)
            ? token.Substring("owned:".Length)
            : token;
        var separator = normalized.IndexOf(':');
        var kind = separator >= 0 ? normalized.Substring(0, separator) : string.Empty;
        var id = separator >= 0 ? normalized.Substring(separator + 1) : normalized;
        return kind switch
        {
            "archetype" => string.Equals(hero.ArchetypeId, id, StringComparison.Ordinal),
            "skill" => hero.SkillCards.Any(skill => string.Equals(skill.SkillId, id, StringComparison.Ordinal)),
            "passive" => hero.SelectedPassiveNodeIds.Contains(id, StringComparer.Ordinal)
                         || string.Equals(hero.FlexPassiveSkillId, id, StringComparison.Ordinal),
            "item" => hero.EquippedItems.Any(item => string.Equals(item.ItemId, id, StringComparison.Ordinal)),
            "affix" => hero.EquippedItems.SelectMany(item => item.Affixes)
                .Any(affix => string.Equals(affix.AffixId, id, StringComparison.Ordinal)),
            "status" => HeroHasStatus(hero, id),
            "combat_effect" => HeroContainsSemantic(hero, id),
            _ => HasTag(hero, id) || HeroContainsSemantic(hero, id),
        };
    }

    private static bool HeroContainsSemantic(HeadlessHeroObservation hero, string token)
        => hero.SkillCards.Any(skill => ContainsAny(skill.SkillId, token)
                                       || skill.AppliedStatuses.Any(status => ContainsAny(status.StatusId, token)))
           || hero.EquippedItems.Any(item => ContainsAny(item.ItemId, token)
                                             || item.Tags.Any(value => ContainsAny(value, token))
                                             || item.StatModifiers.Any(value => ContainsAny(value.StatId, token, value.TagId)));

    private static bool HeroHasStatus(HeadlessHeroObservation hero, string statusId)
        => hero.SkillCards.SelectMany(skill => skill.AppliedStatuses)
            .Any(status => string.Equals(status.StatusId, statusId, StringComparison.Ordinal));

    private static bool HasTag(HeadlessHeroObservation hero, string tag)
        => string.Equals(hero.RaceId, tag, StringComparison.Ordinal)
           || string.Equals(hero.ClassId, tag, StringComparison.Ordinal)
           || string.Equals(hero.ArchetypeId, tag, StringComparison.Ordinal)
           || string.Equals(hero.RoleTag, tag, StringComparison.Ordinal);

    private static int CountMechanicsMatches(IEnumerable<string> claims, HeadlessRewardOption option)
    {
        var mechanics = RewardMechanics(option);
        return SemanticTokens(claims).Count(token => MechanicsContains(mechanics, token));
    }

    private static IReadOnlyList<string> SemanticTokens(IEnumerable<string> claims)
        => (claims ?? Array.Empty<string>())
            .SelectMany(value => (value ?? string.Empty).Split(
                new[] { ':', '(', ')', '>', '<', '=', '/', ';', ',', ' ', '-', '+' },
                StringSplitOptions.RemoveEmptyEntries))
            .Select(value => value.Trim())
            .Where(value => value.Length >= 3 && !int.TryParse(value, out _) && !GenericSemanticTokens.Contains(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> RewardMechanics(HeadlessRewardOption option)
    {
        var values = new List<string>
        {
            option.PayloadId ?? string.Empty,
            option.Kind.ToString(),
        };
        var item = option.Mechanics?.Item;
        if (item != null)
        {
            values.Add(item.ItemId);
            values.Add(item.WeaponFamilyTag);
            values.AddRange(item.Tags);
            values.AddRange(item.StatModifiers.SelectMany(value => new[] { value.StatId, value.Operation, value.TagId }));
            values.AddRange(item.Affixes.SelectMany(affix => new[] { affix.AffixId }
                .Concat(affix.CompileTags)
                .Concat(affix.RequiredTags)
                .Concat(affix.StatModifiers.SelectMany(value => new[] { value.StatId, value.TagId }))));
            values.AddRange(item.GrantedSkills.SelectMany(skill => new[] { skill.SkillId }
                .Concat(skill.AppliedStatuses.Select(status => status.StatusId))));
        }

        var augment = option.Mechanics?.TemporaryAugment;
        if (augment != null)
        {
            values.AddRange(new[] { augment.AugmentId, augment.Category, augment.FamilyId });
            values.AddRange(augment.Tags);
            values.AddRange(augment.BuildBiasTags);
            values.AddRange(augment.StatModifiers.SelectMany(value => new[] { value.StatId, value.Operation, value.TagId }));
            values.AddRange(augment.RuleModifiers.SelectMany(value => new[] { value.Kind, value.Value }));
            values.AddRange(augment.TriggeredEffects.SelectMany(value => new[]
            {
                value.Trigger, value.Operation, value.Scope, value.StatusId,
            }));
        }

        return values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool MechanicsContains(IReadOnlyList<string> mechanics, string token)
        => mechanics.Any(value => ContainsAny(value, token));

    private static bool ContainsAny(string value, params string[] tokens)
        => !string.IsNullOrWhiteSpace(value)
           && tokens.Any(token => !string.IsNullOrWhiteSpace(token)
                                  && value.Contains(token, StringComparison.OrdinalIgnoreCase));
}
