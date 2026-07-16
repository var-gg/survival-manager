using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.HeadlessPolicies;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class HeadlessPolicyPortfolioFastTests
{
    [Test]
    public void Factory_ExecutesAllSixPoliciesDeterministicallyWithReasons()
    {
        var observation = CreateObservation();
        Assert.That(HeadlessPolicyFactory.AllPolicyIds.Count, Is.EqualTo(6));

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

    private static string PlacementSignature(HeadlessDeploymentDecision decision)
        => string.Join("|", decision.Placements
            .OrderBy(value => value.Anchor)
            .Select(value => $"{value.Anchor}:{value.HeroId}"));

    private static HeadlessPolicyObservation CreateObservation(int decisionSeed = 1701)
    {
        var roster = new[]
        {
            Hero("hero-1", "warden", "human", "vanguard", "anchor", DeploymentAnchorId.FrontCenter, itemCount: 1),
            Hero("hero-2", "guardian", "undead", "vanguard", "anchor", DeploymentAnchorId.FrontTop, itemCount: 1),
            Hero("hero-3", "slayer", "human", "duelist", "bruiser", DeploymentAnchorId.FrontBottom, itemCount: 1),
            Hero("hero-4", "raider", "beastkin", "duelist", "bruiser", DeploymentAnchorId.FrontTop, itemCount: 1),
            Hero("hero-5", "hunter", "human", "ranger", "carry", DeploymentAnchorId.BackTop),
            Hero("hero-6", "scout", "beastkin", "ranger", "carry", DeploymentAnchorId.BackBottom),
            Hero("hero-7", "priest", "human", "mystic", "support", DeploymentAnchorId.BackCenter),
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
                new HeadlessRewardOption(0, HeadlessRewardKind.Gold, string.Empty, 10, 0, 0),
                new HeadlessRewardOption(1, HeadlessRewardKind.TemporaryAugment, "augment_guard_human", 0, 0, 0),
                new HeadlessRewardOption(2, HeadlessRewardKind.Echo, string.Empty, 0, 12, 0),
            });
    }

    private static HeadlessHeroObservation Hero(
        string heroId,
        string archetypeId,
        string raceId,
        string classId,
        string roleTag,
        DeploymentAnchorId preferredAnchor,
        int itemCount = 0)
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
            false,
            preferredAnchor);
}
