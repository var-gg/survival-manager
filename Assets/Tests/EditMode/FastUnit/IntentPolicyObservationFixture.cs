using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.Tests.EditMode;

internal static class IntentPolicyObservationFixture
{
    public static HeadlessPolicyObservation Create(
        int decisionSeed = 1701,
        int threatSkulls = 2,
        IReadOnlyDictionary<string, string> evidence = null)
    {
        var roster = new[]
        {
            Hero("hero-1", "warden", "human", "vanguard", "anchor", DeploymentAnchorId.FrontCenter, true),
            Hero("hero-2", "guardian", "undead", "vanguard", "anchor", DeploymentAnchorId.FrontTop, true),
            Hero("hero-3", "slayer", "human", "duelist", "bruiser", DeploymentAnchorId.FrontBottom, true),
            Hero("hero-4", "raider", "beastkin", "duelist", "bruiser", DeploymentAnchorId.FrontTop, true),
            Hero("hero-5", "hunter", "human", "ranger", "carry", DeploymentAnchorId.BackTop, false),
            Hero("hero-6", "scout", "beastkin", "ranger", "carry", DeploymentAnchorId.BackBottom, false),
            Hero("hero-7", "priest", "human", "mystic", "support", DeploymentAnchorId.BackCenter, false),
            Hero("hero-8", "hexer", "undead", "mystic", "controller", DeploymentAnchorId.BackCenter, false),
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
                "encounter-visible",
                "faction-visible",
                threatSkulls >= 4 ? "lethal" : "normal",
                threatSkulls,
                new[]
                {
                    new HeadlessEnemyUnitPreview(
                        "enemy-ranger",
                        "undead",
                        "ranger",
                        "carry",
                        DeploymentAnchorId.BackTop),
                    new HeadlessEnemyUnitPreview(
                        "enemy-vanguard",
                        "undead",
                        "vanguard",
                        "anchor",
                        DeploymentAnchorId.FrontCenter),
                },
                string.Empty,
                string.Empty,
                Array.Empty<string>()),
            new[]
            {
                new HeadlessRewardOption(
                    0,
                    HeadlessRewardKind.TemporaryAugment,
                    "augment-human-line",
                    0,
                    0,
                    0,
                    new HeadlessRewardMechanicsObservation(
                        null,
                        new HeadlessAugmentMechanicsObservation(
                            "augment-human-line",
                            "run_utility",
                            "human_line",
                            1,
                            new[] { "human", "sustain" },
                            new[] { "human", "frontline" },
                            new[] { new HeadlessStatModifierObservation("MaxHp", "Add", 8f, "human") },
                            Array.Empty<HeadlessRuleModifierObservation>(),
                            Array.Empty<HeadlessTriggeredEffectObservation>()))),
                new HeadlessRewardOption(1, HeadlessRewardKind.Echo, "echo-cache", 0, 10, 0),
            },
            new HeadlessWalletObservation(10, 5),
            Array.Empty<HeadlessAugmentMechanicsObservation>(),
            new[]
            {
                new HeadlessSynergyCountObservation("human", 2),
                new HeadlessSynergyCountObservation("vanguard", 2),
                new HeadlessSynergyCountObservation("duelist", 2),
            },
            new[]
            {
                new HeadlessSynergyObservation(
                    "synergy-human",
                    "human",
                    new[]
                    {
                        new HeadlessSynergyTierObservation(2, Array.Empty<HeadlessStatModifierObservation>(), "rule.phalanx_minor"),
                        new HeadlessSynergyTierObservation(4, Array.Empty<HeadlessStatModifierObservation>(), "rule.phalanx"),
                    }),
                new HeadlessSynergyObservation(
                    "synergy-vanguard",
                    "vanguard",
                    new[]
                    {
                        new HeadlessSynergyTierObservation(3, Array.Empty<HeadlessStatModifierObservation>(), "rule.bulwark"),
                    }),
            },
            evidence ?? DummyEvidence());
    }

    public static HeadlessPolicyObservation CreateWithAuditableFacts(
        out IReadOnlyList<PlayerVisibleFactRecord> facts,
        int decisionSeed = 1701)
    {
        var at = new PlayerVisibleTimelinePoint(0, 0, 0);
        var rows = new[]
        {
            Fact(at, PlayerVisibleUiSource.RunSeedDisplay, "decision_seed"),
            Fact(at, PlayerVisibleUiSource.CampaignMap, "campaign_context"),
            Fact(at, PlayerVisibleUiSource.SquadBuilderFormation, "deployment_surface"),
            Fact(at, PlayerVisibleUiSource.TownRoster, "roster_surface"),
            Fact(at, PlayerVisibleUiSource.EncounterPreview, "enemy_preview"),
            Fact(at, PlayerVisibleUiSource.RewardCard, "reward_surface"),
        };
        facts = rows;
        return Create(
            decisionSeed,
            evidence: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [HeadlessPolicyEvidence.DecisionSeedSignal] = rows[0].FactId,
                [HeadlessPolicyEvidence.CampaignContextSignal] = rows[1].FactId,
                [HeadlessPolicyEvidence.DeploymentSurfaceSignal] = rows[2].FactId,
                [HeadlessPolicyEvidence.RosterSurfaceSignal] = rows[3].FactId,
                [HeadlessPolicyEvidence.EnemyPreviewSignal] = rows[4].FactId,
                [HeadlessPolicyEvidence.RewardSurfaceSignal] = rows[5].FactId,
            });
    }

    public static HeadlessConceptIntent HumanThresholdIntent()
        => new(
            "coverage-human-phalanx",
            "coverage",
            new[] { "build.count_tag(human)>=4", "build.team_rule=rule.phalanx" },
            new[]
            {
                "build.count_tag(human)=3/4",
                "build.count_tag(human)=4/4",
                "build.team_rule=rule.phalanx",
            },
            "beat.synergy_activated",
            new[] { "archetype:hunter", "archetype:priest" },
            new[] { "formation:any_legal" },
            new[] { "enemy_threat:burst -> flex:protected_slot" },
            "aspirational",
            new[]
            {
                "remaining_roster_slots<missing_counted_tag:human",
                "visible_track_has_no_progress_offer:2",
            });

    public static HeadlessConceptIntent MissingPrimaryIntent(bool includeSubstitution)
        => new(
            includeSubstitution ? "coverage-missing-with-substitute" : "coverage-missing-no-substitute",
            "coverage",
            new[] { "owned:archetype:dragon" },
            new[] { "deploy:archetype:dragon" },
            "beat.dragon_payoff",
            includeSubstitution ? new[] { "archetype:hunter" } : Array.Empty<string>(),
            new[] { "formation:any_legal" },
            new[] { "enemy_threat:burst -> flex:dragon_guard" },
            "aspirational",
            new[] { "visible_dragon_source_unavailable" });

    private static HeadlessHeroObservation Hero(
        string heroId,
        string archetypeId,
        string raceId,
        string classId,
        string roleTag,
        DeploymentAnchorId anchor,
        bool deployed)
        => new(
            heroId,
            archetypeId,
            raceId,
            classId,
            roleTag,
            1,
            100,
            100,
            0,
            deployed,
            anchor,
            Array.Empty<HeadlessSkillObservation>(),
            string.Empty,
            string.Empty,
            Array.Empty<HeadlessItemMechanicsObservation>(),
            Array.Empty<string>());

    private static IReadOnlyDictionary<string, string> DummyEvidence()
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HeadlessPolicyEvidence.DecisionSeedSignal] = "fact-decision-seed",
            [HeadlessPolicyEvidence.CampaignContextSignal] = "fact-campaign-context",
            [HeadlessPolicyEvidence.DeploymentSurfaceSignal] = "fact-deployment-surface",
            [HeadlessPolicyEvidence.RosterSurfaceSignal] = "fact-roster-surface",
            [HeadlessPolicyEvidence.EnemyPreviewSignal] = "fact-enemy-preview",
            [HeadlessPolicyEvidence.RewardSurfaceSignal] = "fact-reward-surface",
        };

    private static PlayerVisibleFactRecord Fact(PlayerVisibleTimelinePoint at, string uiSource, string subject)
        => PlayerVisibleFactRecord.Create(
            "intent-test-run",
            "campaign-000000",
            at,
            uiSource,
            subject,
            "shows",
            subject,
            "current decision",
            string.Empty,
            "visible ui",
            $"visible:{subject}");
}
