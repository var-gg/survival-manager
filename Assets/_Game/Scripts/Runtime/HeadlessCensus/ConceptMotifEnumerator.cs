using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SM.HeadlessCensus;

/// <summary>truth graph motif를 495 build와 360 formation의 합법 공간에 결합한다.</summary>
internal static class ConceptMotifEnumerator
{
    public const string ThresholdDoctrineMotif = "threshold_doctrine_tactical_payoff";
    public const string EnablerAmplifierMotif = "enabler_amplifier_payoff";

    private static readonly IReadOnlyDictionary<string, string[]> RelevantAmplifiers =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["barrier"] = new[] { "barrier_power", "max_health", "skill_haste" },
            ["damage"] = new[]
            {
                "attack_range", "attack_speed", "crit_chance", "crit_multiplier", "mag_pen", "mag_power",
                "phys_pen", "phys_power", "skill_haste",
            },
            ["healing"] = new[] { "heal_power", "skill_haste", "status_potency" },
            ["status"] = new[] { "skill_haste", "status_potency" },
            ["status_removed"] = new[] { "heal_power", "skill_haste", "status_potency" },
        };

    public static ConceptMotifEnumerationResult Enumerate(
        BuildSpaceCensus census,
        BuildGrammarTruthGraph graph,
        IEnumerable<string> observableWitnesses)
    {
        if (census == null)
        {
            throw new ArgumentNullException(nameof(census));
        }

        if (graph == null)
        {
            throw new ArgumentNullException(nameof(graph));
        }

        var witnessSet = (observableWitnesses ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        var nodes = graph.Edges
            .GroupBy(edge => SubjectKey(edge.SubjectKind, edge.SubjectId), StringComparer.Ordinal)
            .Where(group => group.Any(edge => edge.Actionable))
            .Select(group => new SubjectNode(
                group.First().SubjectKind,
                group.First().SubjectId,
                group.OrderBy(edge => edge.EdgeId, StringComparer.Ordinal).ToArray()))
            .OrderBy(node => node.Key, StringComparer.Ordinal)
            .ToArray();
        var nodesByKey = nodes.ToDictionary(node => node.Key, StringComparer.Ordinal);
        var formationGroups = BuildFormationGroups(census);
        var candidates = new List<ConceptCandidate>();
        var unreachableThresholds = EnumerateThresholdMotifs(
            candidates,
            census,
            nodes,
            nodesByKey,
            formationGroups,
            witnessSet);
        EnumerateEffectMotifs(candidates, census, nodes, nodesByKey, formationGroups, witnessSet);

        var rawStatOnly = nodes.Count(node => node.Edges.Any(IsRawStatAmplifier)
                                               && node.Edges.All(edge => edge.Relation is not BuildGrammarRelation.Produces
                                                   and not BuildGrammarRelation.PaysOff));
        var unobservable = graph.Edges
            .Where(IsPotentialPayoff)
            .Where(edge => !string.IsNullOrWhiteSpace(edge.ExpectedFeedbackWitness)
                           && !witnessSet.Contains(edge.ExpectedFeedbackWitness))
            .Select(edge => edge.EdgeId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        return new ConceptMotifEnumerationResult(
            candidates.OrderBy(candidate => candidate.Fingerprint.Signature, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Recipe.RecipeId, StringComparer.Ordinal)
                .ToArray(),
            rawStatOnly,
            unobservable,
            unreachableThresholds);
    }

    private static int EnumerateThresholdMotifs(
        ICollection<ConceptCandidate> candidates,
        BuildSpaceCensus census,
        IEnumerable<SubjectNode> nodes,
        IReadOnlyDictionary<string, SubjectNode> nodesByKey,
        IReadOnlyList<FormationGroup> formationGroups,
        HashSet<string> witnesses)
    {
        var unreachable = 0;
        foreach (var node in nodes.Where(node => node.SubjectKind == BuildGrammarSubjectKind.Synergy))
        {
            var requirements = node.Edges
                .Where(edge => edge.Relation == BuildGrammarRelation.Requires && edge.TargetKind == "tag")
                .Select(edge => new { Edge = edge, Threshold = ParseThreshold(edge.TruthValue) })
                .Where(value => value.Threshold > 0)
                .OrderBy(value => value.Edge.TargetId, StringComparer.Ordinal)
                .ThenBy(value => value.Threshold)
                .ToArray();
            var payoffs = node.Edges
                .Where(edge => edge.Relation == BuildGrammarRelation.PaysOff && edge.TargetKind == "team_rule")
                .Where(edge => witnesses.Contains(edge.ExpectedFeedbackWitness))
                .OrderBy(edge => edge.TargetId, StringComparer.Ordinal)
                .ToArray();
            foreach (var requirement in requirements)
            {
                var maximum = census.Combinations.Max(build => CountTag(build, requirement.Edge.TargetId));
                if (maximum < requirement.Threshold)
                {
                    unreachable++;
                    continue;
                }

                var matchingBuilds = census.Combinations
                    .Where(build => CountTag(build, requirement.Edge.TargetId) >= requirement.Threshold)
                    .OrderBy(build => build.BuildId, StringComparer.Ordinal)
                    .ToArray();
                foreach (var payoff in payoffs)
                {
                    foreach (var build in matchingBuilds)
                    {
                        var substitutions = ResolveBuildSubstitutions(build, nodesByKey);
                        var flexSlots = ResolveBuildFlexSlots(build, nodesByKey);
                        foreach (var formationGroup in formationGroups)
                        {
                            var fingerprint = new ConceptFingerprint(
                                ThresholdDoctrineMotif,
                                $"counted_tag:{requirement.Edge.TargetId}",
                                "threshold",
                                $"team_rule:{payoff.TargetId}",
                                payoff.ExpectedFeedbackWitness,
                                requirement.Edge.TargetId,
                                requirement.Threshold,
                                payoff.TargetId,
                                formationGroup.Profile);
                            var components = new[]
                                {
                                    node.Key,
                                    $"team_rule:{payoff.TargetId}",
                                }
                                .Concat(build.Members.Select(member => $"archetype:{member.ArchetypeId}"))
                                .OrderBy(value => value, StringComparer.Ordinal)
                                .ToArray();
                            var recipe = Recipe(build, formationGroup.Representative, components);
                            var contract = new ConceptContract(
                                new[]
                                {
                                    $"build.count_tag({requirement.Edge.TargetId})>={requirement.Threshold}",
                                    $"build.team_rule={payoff.TargetId}",
                                    ConceptFormationProfile.Predicate(formationGroup.Profile),
                                },
                                ThresholdMilestones(requirement.Edge.TargetId, requirement.Threshold, payoff.TargetId),
                                payoff.ExpectedFeedbackWitness,
                                substitutions,
                                flexSlots,
                                CounterAffordances(payoff.TargetId, formationGroup.Profile),
                                requirement.Threshold >= 3
                                    ? ConceptAvailabilityTier.Aspirational
                                    : ConceptAvailabilityTier.Core,
                                ResolvePivotConditions(node, requirement.Edge.TargetId, threshold: requirement.Threshold));
                            candidates.Add(new ConceptCandidate(
                                fingerprint,
                                recipe,
                                contract,
                                formationGroup.FormationCount,
                                components.Concat(build.Members.Select(member => $"member:{member.ArchetypeId}"))
                                    .Append($"formation:{formationGroup.Representative.Signature}")
                                    .Distinct(StringComparer.Ordinal)
                                    .OrderBy(value => value, StringComparer.Ordinal)
                                    .ToArray(),
                                formationGroup.Representative.Features.FrontlineCount,
                                formationGroup.Representative.Features.ProtectedSlotCount,
                                formationGroup.Representative.Features.BacklineAccessibility,
                                formationGroup.Representative.Features.FlankRearExposureScore));
                        }
                    }
                }
            }
        }

        return unreachable;
    }

    private static void EnumerateEffectMotifs(
        ICollection<ConceptCandidate> candidates,
        BuildSpaceCensus census,
        IReadOnlyList<SubjectNode> nodes,
        IReadOnlyDictionary<string, SubjectNode> nodesByKey,
        IReadOnlyList<FormationGroup> formationGroups,
        HashSet<string> witnesses)
    {
        var knownBuildTags = census.Combinations.SelectMany(build => build.Members)
            .SelectMany(member => new[] { member.ArchetypeId, member.RaceId, member.ClassId })
            .ToHashSet(StringComparer.Ordinal);
        var routes = BuildPayoffRoutes(nodes, nodesByKey, witnesses);
        var amplifierGroups = BuildAmplifierGroups(nodes);
        foreach (var route in routes)
        {
            var relevantStats = ResolveRelevantAmplifiers(route.PayoffEdge);
            foreach (var amplifier in amplifierGroups.Where(option => relevantStats.Contains(option.Edge.TargetId)))
            {
                if (route.Enabler.Key == amplifier.Node.Key
                    || route.PayoffNode.Key == amplifier.Node.Key
                    || Conflicts(route.Enabler, amplifier.Node)
                    || Conflicts(route.PayoffNode, amplifier.Node))
                {
                    continue;
                }

                var componentNodes = new[] { route.Enabler, route.PayoffNode, amplifier.Node }
                    .GroupBy(node => node.Key, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(node => node.Key, StringComparer.Ordinal)
                    .ToArray();
                var requiredKnownTags = componentNodes.SelectMany(RequiredTags)
                    .Where(knownBuildTags.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var matchingBuilds = census.Combinations
                    .Where(build => requiredKnownTags.All(tag => CountTag(build, tag) > 0))
                    .OrderBy(build => build.BuildId, StringComparer.Ordinal)
                    .ToArray();
                if (matchingBuilds.Length == 0)
                {
                    continue;
                }

                var formationGroup = SelectEffectFormationGroup(route.PayoffEdge, formationGroups);
                var representativeBuild = matchingBuilds[0];
                var components = componentNodes.Select(node => node.Key)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var recipe = Recipe(representativeBuild, formationGroup.Representative, components);
                var paths = componentNodes.SelectMany(AcquisitionPaths)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var fingerprint = new ConceptFingerprint(
                    EnablerAmplifierMotif,
                    route.EnablerShape,
                    $"{amplifier.Edge.TargetKind}:{amplifier.Edge.TargetId};source={amplifier.Node.SubjectKind};paths={string.Join("+", AcquisitionPaths(amplifier.Node))}",
                    $"{route.PayoffEdge.Relation}:{route.PayoffEdge.TargetKind}:{route.PayoffEdge.TargetId}",
                    route.PayoffEdge.ExpectedFeedbackWitness,
                    string.Empty,
                    0,
                    string.Empty,
                    formationGroup.Profile);
                var substitutions = ResolveNodeSubstitutions(componentNodes);
                var flexSlots = componentNodes.Where(node => ResolveNodeSubstitutions(new[] { node }).Count > 0)
                    .Select(node => node.Key)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var identity = componentNodes.Select(node => $"owned:{node.Key}")
                    .Concat(requiredKnownTags.Select(tag => $"build.contains_tag:{tag}"))
                    .Append($"effect.ready:{route.PayoffEdge.TargetKind}:{route.PayoffEdge.TargetId}")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var aspirational = componentNodes.Length >= 3
                                  || paths.Contains("level_node", StringComparer.Ordinal)
                                  && paths.Contains("refit", StringComparer.Ordinal);
                var contract = new ConceptContract(
                    identity,
                    componentNodes.Select(node => $"acquire:{node.Key}")
                        .Append($"activate:{route.PayoffEdge.TargetKind}:{route.PayoffEdge.TargetId}")
                        .ToArray(),
                    route.PayoffEdge.ExpectedFeedbackWitness,
                    substitutions,
                    flexSlots,
                    CounterAffordances(route.PayoffEdge.TargetId, formationGroup.Profile),
                    aspirational ? ConceptAvailabilityTier.Aspirational : ConceptAvailabilityTier.Core,
                    componentNodes.SelectMany(node => ResolvePivotConditions(node, string.Empty, threshold: 0))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray());
                var equivalentCount = ClampRecipeCount(
                    (long)matchingBuilds.Length * formationGroup.FormationCount * amplifier.OptionCount);
                candidates.Add(new ConceptCandidate(
                    fingerprint,
                    recipe,
                    contract,
                    equivalentCount,
                    components.Concat(representativeBuild.Members.Select(member => $"member:{member.ArchetypeId}"))
                        .Append($"formation:{formationGroup.Representative.Signature}")
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    formationGroup.Representative.Features.FrontlineCount,
                    formationGroup.Representative.Features.ProtectedSlotCount,
                    formationGroup.Representative.Features.BacklineAccessibility,
                    formationGroup.Representative.Features.FlankRearExposureScore));
            }
        }
    }

    private static IReadOnlyList<PayoffRoute> BuildPayoffRoutes(
        IEnumerable<SubjectNode> nodes,
        IReadOnlyDictionary<string, SubjectNode> nodesByKey,
        HashSet<string> witnesses)
    {
        var routes = new List<PayoffRoute>();
        foreach (var node in nodes)
        {
            foreach (var payoff in node.Edges.Where(IsPotentialPayoff)
                         .Where(edge => witnesses.Contains(edge.ExpectedFeedbackWitness)))
            {
                routes.Add(new PayoffRoute(
                    node,
                    node,
                    payoff,
                    payoff.Relation == BuildGrammarRelation.Produces
                        ? $"produces:{payoff.TargetKind}"
                        : $"activates:{node.SubjectKind}"));
            }

            foreach (var producesSkill in node.Edges
                         .Where(edge => edge.Relation == BuildGrammarRelation.Produces && edge.TargetKind == "skill"))
            {
                if (!nodesByKey.TryGetValue(SubjectKey(BuildGrammarSubjectKind.Skill, producesSkill.TargetId), out var skill))
                {
                    continue;
                }

                foreach (var payoff in skill.Edges.Where(IsPotentialPayoff)
                             .Where(edge => witnesses.Contains(edge.ExpectedFeedbackWitness)))
                {
                    routes.Add(new PayoffRoute(node, skill, payoff, "produces:skill"));
                }
            }
        }

        return routes.GroupBy(
                route => $"{route.Enabler.Key}|{route.PayoffNode.Key}|{route.PayoffEdge.EdgeId}|{route.EnablerShape}",
                StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(route => route.Enabler.Key, StringComparer.Ordinal)
            .ThenBy(route => route.PayoffNode.Key, StringComparer.Ordinal)
            .ThenBy(route => route.PayoffEdge.EdgeId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<AmplifierOption> BuildAmplifierGroups(IEnumerable<SubjectNode> nodes)
    {
        return nodes.SelectMany(node => node.Edges.Where(IsRawStatAmplifier)
                .Select(edge => new AmplifierOption(node, edge, 1)))
            .GroupBy(option => string.Join("|", new[]
            {
                option.Node.SubjectKind,
                option.Edge.TargetKind,
                option.Edge.TargetId,
                string.Join("+", AcquisitionPaths(option.Node)),
                string.Join("+", RequiredTags(option.Node)),
                string.Join("+", ConflictTargets(option.Node)),
            }), StringComparer.Ordinal)
            .Select(group =>
            {
                var representative = group.OrderBy(option => option.Node.Key, StringComparer.Ordinal)
                    .ThenBy(option => option.Edge.EdgeId, StringComparer.Ordinal)
                    .First();
                return representative with { OptionCount = group.Count() };
            })
            .OrderBy(option => option.Edge.TargetId, StringComparer.Ordinal)
            .ThenBy(option => option.Node.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static HashSet<string> ResolveRelevantAmplifiers(BuildGrammarTruthEdge payoff)
    {
        var key = payoff.TargetKind == "status"
            ? "status"
            : payoff.TargetId == "cleanse_profile"
                ? "status_removed"
                : payoff.TargetId;
        return RelevantAmplifiers.TryGetValue(key, out var values)
            ? values.ToHashSet(StringComparer.Ordinal)
            : new[] { "attack_speed", "mag_power", "phys_power", "skill_haste", "status_potency" }
                .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<FormationGroup> BuildFormationGroups(BuildSpaceCensus census)
    {
        var medoidSignatures = census.Medoids.Select(medoid => medoid.Placement.Signature)
            .ToHashSet(StringComparer.Ordinal);
        return census.Formations
            .GroupBy(formation => ConceptFormationProfile.Classify(formation.Features), StringComparer.Ordinal)
            .Select(group => new FormationGroup(
                group.Key,
                group.OrderByDescending(formation => medoidSignatures.Contains(formation.Signature))
                    .ThenBy(formation => formation.Signature, StringComparer.Ordinal)
                    .First(),
                group.Count()))
            .OrderBy(group => group.Profile, StringComparer.Ordinal)
            .ToArray();
    }

    private static FormationGroup SelectEffectFormationGroup(
        BuildGrammarTruthEdge payoff,
        IReadOnlyList<FormationGroup> groups)
    {
        var preferred = payoff.TargetId switch
        {
            "barrier" or "healing" => ConceptFormationProfile.FortifiedLine,
            "damage" => ConceptFormationProfile.ForwardSpear,
            _ when payoff.TargetKind == "status" => ConceptFormationProfile.BaitedGap,
            _ => ConceptFormationProfile.ScreenedBackline,
        };
        return groups.FirstOrDefault(group => group.Profile == preferred)
               ?? groups.First(group => group.Profile == ConceptFormationProfile.OpenSkirmish);
    }

    private static ConceptRecipe Recipe(
        BuildCombination build,
        FormationPlacement formation,
        IReadOnlyList<string> components)
    {
        var recipeId = ConceptStableId.Create(
            "recipe",
            build.BuildId,
            formation.Signature,
            string.Join("+", components));
        return new ConceptRecipe(recipeId, build.BuildId, formation.Signature, components);
    }

    private static IReadOnlyList<string> ThresholdMilestones(string tag, int threshold, string ruleId)
    {
        var start = Math.Max(1, threshold - 2);
        return Enumerable.Range(start, threshold - start + 1)
            .Select(value => $"build.count_tag({tag})={value}/{threshold}")
            .Append($"build.team_rule={ruleId}")
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveBuildSubstitutions(
        BuildCombination build,
        IReadOnlyDictionary<string, SubjectNode> nodes)
        => build.Members.Select(member => SubjectKey(BuildGrammarSubjectKind.Archetype, member.ArchetypeId))
            .Where(nodes.ContainsKey)
            .SelectMany(key => ResolveNodeSubstitutions(new[] { nodes[key] }))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> ResolveBuildFlexSlots(
        BuildCombination build,
        IReadOnlyDictionary<string, SubjectNode> nodes)
        => build.Members
            .Where(member => nodes.TryGetValue(
                                 SubjectKey(BuildGrammarSubjectKind.Archetype, member.ArchetypeId),
                                 out var node)
                             && ResolveNodeSubstitutions(new[] { node }).Count > 0)
            .Select(member => $"archetype:{member.ArchetypeId}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> ResolveNodeSubstitutions(IEnumerable<SubjectNode> nodes)
        => nodes.SelectMany(node => node.Edges
                .Where(edge => edge.Relation == BuildGrammarRelation.Substitutes)
                .Select(edge => $"{edge.TargetKind}:{edge.TargetId}"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> ResolvePivotConditions(
        SubjectNode node,
        string thresholdTag,
        int threshold)
    {
        var values = new List<string>();
        if (threshold > 0)
        {
            values.Add($"remaining_roster_slots<missing_counted_tag:{thresholdTag}");
        }

        foreach (var path in AcquisitionPaths(node))
        {
            values.Add($"acquisition_path_unavailable:{path}");
        }

        values.AddRange(ConflictTargets(node).Select(value => $"conflict_active:{value}"));
        return values.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> CounterAffordances(string payoffId, string formationProfile)
    {
        var values = new List<string>
        {
            $"formation_reposition:preserve_profile={formationProfile}",
        };
        if (payoffId is "healing" or "barrier" || payoffId.Contains("bulwark", StringComparison.Ordinal))
        {
            values.Add("enemy_threat:burst -> flex:defensive_amplifier_or_protected_slot");
        }
        else if (payoffId is "root" or "slow" or "silence" || payoffId.Contains("resonance", StringComparison.Ordinal))
        {
            values.Add("enemy_threat:mobility -> flex:control_substitute");
        }
        else if (payoffId == "damage" || payoffId.Contains("execute", StringComparison.Ordinal)
                                      || payoffId.Contains("killzone", StringComparison.Ordinal))
        {
            values.Add("enemy_threat:backline -> flex:target_access_or_range");
        }
        else
        {
            values.Add("enemy_threat:counter -> flex:substitute_without_dropping_identity");
        }

        return values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static bool Conflicts(SubjectNode left, SubjectNode right)
    {
        if (left.Key == right.Key)
        {
            return false;
        }

        if (left.Edges.Any(edge => edge.Relation == BuildGrammarRelation.Conflicts
                                   && SubjectKey(edge.TargetKind, edge.TargetId) == right.Key)
            || right.Edges.Any(edge => edge.Relation == BuildGrammarRelation.Conflicts
                                      && SubjectKey(edge.TargetKind, edge.TargetId) == left.Key))
        {
            return true;
        }

        var leftGroups = left.Edges.Where(edge => edge.Relation == BuildGrammarRelation.Conflicts
                                                  && edge.TargetKind == "conflict_group")
            .Select(edge => edge.TargetId)
            .ToHashSet(StringComparer.Ordinal);
        var rightGroups = right.Edges.Where(edge => edge.Relation == BuildGrammarRelation.Conflicts
                                                    && edge.TargetKind == "conflict_group")
            .Select(edge => edge.TargetId);
        if (rightGroups.Any(leftGroups.Contains))
        {
            return true;
        }

        var leftBlockedTags = left.Edges.Where(edge => edge.Relation == BuildGrammarRelation.Conflicts
                                                       && edge.TargetKind == "tag")
            .Select(edge => edge.TargetId)
            .ToHashSet(StringComparer.Ordinal);
        return RequiredTags(right).Any(leftBlockedTags.Contains)
               || RequiredTags(left).Any(tag => right.Edges.Any(edge => edge.Relation == BuildGrammarRelation.Conflicts
                                                                       && edge.TargetKind == "tag"
                                                                       && edge.TargetId == tag));
    }

    private static IReadOnlyList<string> AcquisitionPaths(SubjectNode node)
        => node.Edges.Where(edge => edge.Relation == BuildGrammarRelation.AcquiredBy)
            .Select(edge => edge.TargetId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> RequiredTags(SubjectNode node)
        => node.Edges.Where(edge => edge.Relation == BuildGrammarRelation.Requires && edge.TargetKind == "tag")
            .Select(edge => edge.TargetId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> ConflictTargets(SubjectNode node)
        => node.Edges.Where(edge => edge.Relation == BuildGrammarRelation.Conflicts)
            .Select(edge => $"{edge.TargetKind}:{edge.TargetId}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static bool IsPotentialPayoff(BuildGrammarTruthEdge edge)
        => edge.Relation == BuildGrammarRelation.PaysOff && edge.TargetKind != "team_rule"
           || edge.Relation == BuildGrammarRelation.Produces && edge.TargetKind == "status";

    private static bool IsRawStatAmplifier(BuildGrammarTruthEdge edge)
        => edge.Relation == BuildGrammarRelation.Amplifies && edge.TargetKind == "stat";

    private static int CountTag(BuildCombination build, string tag)
        => build.Members.Count(member => string.Equals(member.ArchetypeId, tag, StringComparison.Ordinal)
                                         || string.Equals(member.RaceId, tag, StringComparison.Ordinal)
                                         || string.Equals(member.ClassId, tag, StringComparison.Ordinal));

    private static int ParseThreshold(string truthValue)
    {
        const string prefix = "threshold=";
        return truthValue != null && truthValue.StartsWith(prefix, StringComparison.Ordinal)
               && int.TryParse(truthValue[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static int ClampRecipeCount(long count) => (int)Math.Min(int.MaxValue, Math.Max(0L, count));

    private static string SubjectKey(string subjectKind, string subjectId) => $"{subjectKind}:{subjectId}";

    private sealed record SubjectNode(
        string SubjectKind,
        string SubjectId,
        IReadOnlyList<BuildGrammarTruthEdge> Edges)
    {
        public string Key => SubjectKey(SubjectKind, SubjectId);
    }

    private sealed record FormationGroup(
        string Profile,
        FormationPlacement Representative,
        int FormationCount);

    private sealed record PayoffRoute(
        SubjectNode Enabler,
        SubjectNode PayoffNode,
        BuildGrammarTruthEdge PayoffEdge,
        string EnablerShape);

    private sealed record AmplifierOption(
        SubjectNode Node,
        BuildGrammarTruthEdge Edge,
        int OptionCount);
}
