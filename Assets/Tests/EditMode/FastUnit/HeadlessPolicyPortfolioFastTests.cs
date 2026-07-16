using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class HeadlessPolicyPortfolioFastTests
{
    [Test]
    public void Factory_ExecutesSixProductionPoliciesAndCoverageDeterministicallyWithReasons()
    {
        var observation = CreateObservation();
        Assert.That(HeadlessPolicyFactory.ProductionPolicyIds.Count, Is.EqualTo(6));
        Assert.That(HeadlessPolicyFactory.AllPolicyIds.Count, Is.EqualTo(7));

        foreach (var policyId in HeadlessPolicyFactory.AllPolicyIds)
        {
            var policy = HeadlessPolicyFactory.Create(policyId);
            var firstDeployment = policy.DecideDeployment(observation);
            var secondDeployment = policy.DecideDeployment(observation);
            var firstReward = policy.DecideReward(observation);
            var secondReward = policy.DecideReward(observation);

            HeadlessPolicyGuard.ValidateDeploymentDecision(observation, firstDeployment);
            HeadlessPolicyGuard.ValidateRewardDecision(observation, firstReward);
            Assert.That(policy.Id, Is.EqualTo(policyId));
            Assert.That(PlacementSignature(firstDeployment), Is.EqualTo(PlacementSignature(secondDeployment)), policyId);
            Assert.That(firstDeployment.EstimatedValue, Is.EqualTo(secondDeployment.EstimatedValue), policyId);
            Assert.That(firstDeployment.Rationale, Is.Not.Empty, policyId);
            Assert.That(firstReward.OptionIndex, Is.EqualTo(secondReward.OptionIndex), policyId);
            Assert.That(firstReward.EstimatedValue, Is.EqualTo(secondReward.EstimatedValue), policyId);
            Assert.That(firstReward.Rationale, Is.EqualTo(secondReward.Rationale), policyId);
            Assert.That(firstReward.Rationale, Is.Not.Empty, policyId);
        }
    }

    [Test]
    public void CoveragePolicy_SamplesFiveChannelsWithHealerAndDoctrineWithoutClaimingCompetence()
    {
        var policy = new CoveragePolicy();
        var sampled = new HashSet<string>(StringComparer.Ordinal);
        for (var seed = 1701; seed <= 1705; seed++)
        {
            var observation = CreateObservation(seed);
            var decision = policy.DecideDeployment(observation);
            HeadlessPolicyGuard.ValidateDeploymentDecision(observation, decision);
            var selected = decision.Placements.Select(value => value.HeroId).ToHashSet(StringComparer.Ordinal);

            Assert.That(selected, Does.Contain("hero-7"), "canonical coverage roster must include the visible healer/support");
            Assert.That(selected, Is.EquivalentTo(new[] { "hero-1", "hero-3", "hero-5", "hero-7" }),
                "same-race role-complete roster supplies healer, doctrine and all formation roles");
            Assert.That(decision.Rationale, Does.Contain("QA coverage only (not competent play)"));
            var sample = decision.Rationale.Split(' ')
                .Single(token => token.StartsWith("sample=", StringComparison.Ordinal))
                .Substring("sample=".Length);
            sampled.Add(sample);
        }

        Assert.That(sampled, Is.EquivalentTo(new[] { "flank", "rear", "screen_block", "save", "backline_dive_kill" }));
    }

    [Test]
    public void GreedyPolicy_PreservesStageOneRosterOrderAndFrontBackPlacement()
    {
        var decision = new GreedyPolicy().DecideDeployment(CreateObservation());

        Assert.That(
            PlacementSignature(decision),
            Is.EqualTo("FrontTop:hero-1|FrontCenter:hero-2|FrontBottom:hero-3|BackTop:hero-4"));
    }

    [Test]
    public void SearchPlanner_VisibleStateValueExceedsGreedyOnCanonicalEightHeroFixture()
    {
        var observation = CreateObservation();
        var greedy = new GreedyPolicy().DecideDeployment(observation);
        var planner = new SearchPlannerPolicy().DecideDeployment(observation);
        var plannedHeroIds = planner.Placements.Select(value => value.HeroId).ToHashSet(StringComparer.Ordinal);

        Assert.That(planner.EstimatedValue, Is.GreaterThan(greedy.EstimatedValue));
        Assert.That(plannedHeroIds, Is.EquivalentTo(new[] { "hero-1", "hero-3", "hero-5", "hero-7" }));
        Assert.That(planner.Rationale, Does.Contain("depth=1"));
    }

    [Test]
    public void RandomLegalPolicy_RebuildsItsChoiceFromObservationSeed()
    {
        var policy = new RandomLegalPolicy();
        var first = policy.DecideDeployment(CreateObservation(1701));
        var repeated = policy.DecideDeployment(CreateObservation(1701));
        var otherSeed = policy.DecideDeployment(CreateObservation(1702));

        Assert.That(PlacementSignature(first), Is.EqualTo(PlacementSignature(repeated)));
        Assert.That(PlacementSignature(otherSeed), Is.Not.EqualTo(PlacementSignature(first)));
    }

    [Test]
    public void ObservationContract_DoesNotExposeSessionContentOrHiddenStateVocabulary()
    {
        var assemblyReferences = typeof(HeadlessPolicyObservation).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name != null)
            .ToArray();
        Assert.That(assemblyReferences, Does.Not.Contain("SM.Content"));
        Assert.That(assemblyReferences, Does.Not.Contain("SM.Meta"));
        Assert.That(assemblyReferences, Does.Not.Contain("SM.Persistence.Abstractions"));
        Assert.That(assemblyReferences, Does.Not.Contain("SM.Unity"));
        Assert.That(assemblyReferences, Does.Not.Contain("SM.Editor"));

        var bannedTokens = new[]
        {
            "Future", "Hidden", "Unrevealed", "RngState", "BaseStats", "ThreatCost", "EnemyStats", "InternalState",
        };
        var contractTypes = typeof(HeadlessPolicyObservation).Assembly.GetExportedTypes()
            .Where(type => string.Equals(type.Namespace, "SM.HeadlessPolicies", StringComparison.Ordinal));
        foreach (var property in contractTypes.SelectMany(type => type.GetProperties()))
        {
            Assert.That(
                bannedTokens.Any(token => property.Name.Contains(token, StringComparison.OrdinalIgnoreCase)),
                Is.False,
                $"No-cheat contract property leaks banned vocabulary: {property.DeclaringType?.Name}.{property.Name}");
        }
    }

    [Test]
    public void EnrichedObservation_ExposesSeededBuildMechanicsWalletAndDeployedSynergyCounts()
    {
        var observation = CreateObservation();

        foreach (var hero in observation.Roster)
        {
            Assert.That(
                hero.SkillCards.Select(skill => skill.SkillId),
                Is.EquivalentTo(new[] { $"{hero.ArchetypeId}-active", $"{hero.ArchetypeId}-passive" }),
                hero.HeroId);
            Assert.That(hero.FlexActiveSkillId, Is.EqualTo($"{hero.ArchetypeId}-active"), hero.HeroId);
            Assert.That(hero.FlexPassiveSkillId, Is.EqualTo($"{hero.ArchetypeId}-passive"), hero.HeroId);
        }

        var itemReward = observation.RewardOptions.Single(option => option.Kind == HeadlessRewardKind.Item);
        Assert.That(itemReward.Mechanics.Item, Is.Not.Null);
        Assert.That(itemReward.Mechanics.Item!.Tags, Is.Not.Empty);
        Assert.That(itemReward.Mechanics.Item.StatModifiers, Is.Not.Empty);
        Assert.That(itemReward.Mechanics.Item.Affixes, Is.Empty, "Reward affixes are rolled only after the choice is applied.");

        var augmentReward = observation.RewardOptions.Single(option => option.Kind == HeadlessRewardKind.TemporaryAugment);
        Assert.That(augmentReward.Mechanics.TemporaryAugment, Is.Not.Null);
        Assert.That(augmentReward.Mechanics.TemporaryAugment!.Tags, Is.Not.Empty);
        Assert.That(augmentReward.Mechanics.TemporaryAugment.StatModifiers, Is.Not.Empty);
        Assert.That(augmentReward.Mechanics.TemporaryAugment.TriggeredEffects, Is.Not.Empty);

        Assert.That(observation.Wallet.Gold, Is.EqualTo(17));
        Assert.That(observation.Wallet.Echo, Is.EqualTo(9));
        Assert.That(
            observation.SynergyCounts.ToDictionary(value => value.CountedTagId, value => value.CurrentCount, StringComparer.Ordinal),
            Is.EquivalentTo(new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["human"] = 4,
                ["vanguard"] = 1,
                ["duelist"] = 1,
                ["ranger"] = 1,
                ["mystic"] = 1,
            }));
        Assert.That(observation.TemporaryAugments.Select(augment => augment.AugmentId), Is.EqualTo(new[] { "augment-ward" }));
    }

    [Test]
    public void EnrichedObservation_SameSeedSerializesIdentically()
    {
        var first = HeadlessMetricJson.Serialize(CreateObservation(1701));
        var second = HeadlessMetricJson.Serialize(CreateObservation(1701));

        Assert.That(second, Is.EqualTo(first));
    }

    private static string PlacementSignature(HeadlessDeploymentDecision decision)
        => string.Join("|", decision.Placements
            .OrderBy(value => value.Anchor)
            .Select(value => $"{value.Anchor}:{value.HeroId}"));

    private static HeadlessPolicyObservation CreateObservation(int decisionSeed = 1701)
    {
        var roster = new[]
        {
            Hero("hero-1", "warden", "human", "vanguard", "anchor", DeploymentAnchorId.FrontCenter, itemCount: 1, isDeployed: true),
            Hero("hero-2", "guardian", "undead", "vanguard", "anchor", DeploymentAnchorId.FrontTop, itemCount: 1),
            Hero("hero-3", "slayer", "human", "duelist", "bruiser", DeploymentAnchorId.FrontBottom, itemCount: 1, isDeployed: true),
            Hero("hero-4", "raider", "beastkin", "duelist", "bruiser", DeploymentAnchorId.FrontTop, itemCount: 1),
            Hero("hero-5", "hunter", "human", "ranger", "carry", DeploymentAnchorId.BackTop, isDeployed: true),
            Hero("hero-6", "scout", "beastkin", "ranger", "carry", DeploymentAnchorId.BackBottom),
            Hero("hero-7", "priest", "human", "mystic", "support", DeploymentAnchorId.BackCenter, isDeployed: true),
            Hero("hero-8", "hexer", "undead", "mystic", "controller", DeploymentAnchorId.BackCenter),
        };
        return new HeadlessPolicyObservation(
            decisionSeed,
            4,
            "chapter-1",
            "site-1",
            roster,
            new[]
            {
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontBottom,
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackCenter,
                DeploymentAnchorId.BackBottom,
            },
            new HeadlessEnemyPreview(
                true,
                "encounter-1",
                "enemy-faction",
                "normal",
                2,
                new[]
                {
                    new HeadlessEnemyUnitPreview("enemy-ranger", "undead", "ranger", "carry", DeploymentAnchorId.BackTop),
                    new HeadlessEnemyUnitPreview("enemy-vanguard", "undead", "vanguard", "anchor", DeploymentAnchorId.FrontCenter),
                },
                string.Empty,
                string.Empty,
                Array.Empty<string>()),
            new[]
            {
                new HeadlessRewardOption(
                    0,
                    HeadlessRewardKind.Item,
                    "item-iron-blade",
                    0,
                    0,
                    0,
                    new HeadlessRewardMechanicsObservation(Item("item-iron-blade"), null)),
                new HeadlessRewardOption(
                    1,
                    HeadlessRewardKind.TemporaryAugment,
                    "augment-ward",
                    0,
                    0,
                    0,
                    new HeadlessRewardMechanicsObservation(null, Augment("augment-ward"))),
                new HeadlessRewardOption(2, HeadlessRewardKind.Echo, string.Empty, 0, 12, 0),
            },
            new HeadlessWalletObservation(17, 9),
            new[] { Augment("augment-ward") },
            new[]
            {
                new HeadlessSynergyCountObservation("duelist", 1),
                new HeadlessSynergyCountObservation("human", 4),
                new HeadlessSynergyCountObservation("mystic", 1),
                new HeadlessSynergyCountObservation("ranger", 1),
                new HeadlessSynergyCountObservation("vanguard", 1),
            },
            new[]
            {
                new HeadlessSynergyObservation(
                    "synergy-human",
                    "human",
                    new[]
                    {
                        new HeadlessSynergyTierObservation(
                            2,
                            new[] { new HeadlessStatModifierObservation("MaxHp", "Add", 10f, string.Empty) },
                            "team-rule-human"),
                    }),
            });
    }

    private static HeadlessHeroObservation Hero(
        string heroId,
        string archetypeId,
        string raceId,
        string classId,
        string roleTag,
        DeploymentAnchorId preferredAnchor,
        int itemCount = 0,
        bool isDeployed = false)
        => new(
            heroId,
            archetypeId,
            raceId,
            classId,
            roleTag,
            1,
            100,
            100,
            itemCount,
            isDeployed,
            preferredAnchor,
            new[]
            {
                Skill($"{archetypeId}-active", SkillKind.Strike, CompiledSkillSlots.CoreActive),
                Skill($"{archetypeId}-passive", SkillKind.Buff, CompiledSkillSlots.Support),
            },
            $"{archetypeId}-active",
            $"{archetypeId}-passive",
            itemCount > 0 ? new[] { Item($"item-{archetypeId}", $"affix-{archetypeId}") } : Array.Empty<HeadlessItemMechanicsObservation>(),
            new[] { $"passive-{archetypeId}" });

    private static HeadlessSkillObservation Skill(string id, SkillKind kind, string slotKind)
        => new(
            id,
            kind,
            slotKind,
            12f,
            2.5f,
            DamageType.Physical,
            0f,
            1f,
            0f,
            0f,
            0f,
            5f,
            3f,
            0.2f,
            true,
            SkillDelivery.Melee,
            SkillTargetRule.NearestEnemy,
            new[] { new HeadlessStatusApplicationObservation($"{id}-status", "marked", 2f, 1f, 1) });

    private static HeadlessItemMechanicsObservation Item(string id, string affixId = "")
        => new(
            id,
            $"{id}-instance",
            new[] { "weapon", "physical" },
            "weapon-sword",
            new[] { new HeadlessStatModifierObservation("PhysicalPower", "Add", 2f, string.Empty) },
            string.IsNullOrWhiteSpace(affixId)
                ? Array.Empty<HeadlessAffixMechanicsObservation>()
                : new[]
                {
                    new HeadlessAffixMechanicsObservation(
                        affixId,
                        new[] { "offense" },
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        new[] { new HeadlessStatModifierObservation("AttackSpeed", "Add", 0.1f, string.Empty) },
                        Array.Empty<HeadlessRuleModifierObservation>()),
                },
            Array.Empty<HeadlessSkillObservation>());

    private static HeadlessAugmentMechanicsObservation Augment(string id)
        => new(
            id,
            "run_utility",
            "ward_line",
            1,
            new[] { "guard", "sustain" },
            new[] { "frontline" },
            new[] { new HeadlessStatModifierObservation("MaxHp", "Add", 8f, string.Empty) },
            Array.Empty<HeadlessRuleModifierObservation>(),
            new[]
            {
                new HeadlessTriggeredEffectObservation(
                    "BattleStart",
                    "ApplyStatus",
                    "Team",
                    1f,
                    0f,
                    "guarded",
                    4f,
                    1),
            });
}
