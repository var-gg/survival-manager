using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Content;
using SM.Core.Stats;
using SM.Editor.SeedData;
using SM.HeadlessPolicies;
using SM.Unity;

namespace SM.Editor.Validation;

internal sealed record CampaignBossLearningWitnessResult(
    bool DiveAuthored,
    bool SustainWallAuthored,
    IReadOnlyList<string> NaiveAnswerTags,
    IReadOnlyList<string> InformedAnswerTags,
    string NaiveAnswerSurface,
    string InformedAnswerSurface,
    string PrepRationale);

/// <summary>Wolfpine 보스의 실제 authored content와 informed prep 답을 한 경로에서 증명한다.</summary>
internal static class CampaignBossLearningWitness
{
    private const string EncounterId = "site_wolfpine_trail_boss_1";

    public static CampaignBossLearningWitnessResult Capture()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(CampaignBossLearningWitness));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var content, out var contentError))
        {
            throw new InvalidOperationException($"boss learning witness content unavailable: {contentError}");
        }

        var encounterTemplate = content.Encounters![EncounterId];
        var squad = content.EnemySquads![encounterTemplate.EnemySquadTemplateId];
        var overlay = content.BossOverlays![encounterTemplate.BossOverlayId];
        var captain = squad.Members.Single(member => member.Role == EnemySquadMemberRoleValue.Captain);
        var escorts = squad.Members.Where(member => member.Role == EnemySquadMemberRoleValue.Escort).ToArray();
        var diveAuthored = squad.EnemyPosture is TeamPostureType.AllInBackline or TeamPostureType.CollapseWeakSide
                           && escorts.Any(member => content.Archetypes[member.ArchetypeId].ClassId == "duelist")
                           && overlay.SignatureUtilityTag.Contains("backline_dive", StringComparison.Ordinal);
        var overlayStatusIds = (overlay.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>())
            .Select(status => status.StatusId)
            .ToHashSet(StringComparer.Ordinal);
        var sustainWallAuthored = string.Equals(captain.ArchetypeId, "warden", StringComparison.Ordinal)
                                  && content.Archetypes[captain.ArchetypeId].ClassId == "vanguard"
                                  && escorts.Any(member => content.Archetypes[member.ArchetypeId].ClassId == "mystic")
                                  && overlayStatusIds.Contains("guarded")
                                  && overlayStatusIds.Contains("barrier")
                                  && overlay.SignatureAuraTag.Contains("sustain", StringComparison.Ordinal);

        var session = H100SessionDriver.CreateSession(lookup, "campaign-boss-learning-witness");
        CampaignBalanceSweepRunner.AuthorSquad(
            session,
            lookup,
            "boss-learning-mixed",
            new[] { "warden", "raider", "priest", "shaman" },
            CampaignBalanceSweepRunner.LoadItemMetaIndex(),
            redistributeStarterGear: false);
        AdvanceToWolfpineBoss(session);
        ArrangeExposedNaiveFormation(session);

        if (!session.TryBuildSelectedBattleState(
                out _,
                out var resolvedEncounter,
                out var naiveSnapshot,
                out var buildError))
        {
            throw new InvalidOperationException($"boss learning witness could not build naive state: {buildError}");
        }

        if (!string.Equals(resolvedEncounter.Context.EncounterId, EncounterId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"boss learning witness reached '{resolvedEncounter.Context.EncounterId}', expected '{EncounterId}'.");
        }

        var learningSpec = CampaignBalanceSweepConfig.Default.FindBossLearningSpec(EncounterId)
                           ?? throw new InvalidOperationException($"boss learning spec missing: {EncounterId}");
        var naiveTags = CampaignBossAnswerTagEvaluator.Evaluate(naiveSnapshot, learningSpec);
        var seed = H100SessionDriver.DeriveSeed(EncounterId, 1701);
        var observation = H100PolicyObservationBuilder.Build(
            session,
            lookup,
            seed,
            includeTownRoster: true);
        var policy = HeadlessPolicyFactory.Create(HeadlessPolicyFactory.PreviewGroundedConceptId);
        if (policy is not IHeadlessPrepPolicy prepPolicy)
        {
            throw new InvalidOperationException("preview-grounded policy does not expose encounter prep.");
        }

        var prep = H100SessionDriver.ApplyPolicyPrep(session, policy, prepPolicy, seed, observation);
        if (!session.TryBuildSelectedBattleState(
                out _,
                out _,
                out var informedSnapshot,
                out buildError))
        {
            throw new InvalidOperationException($"boss learning witness could not build informed state: {buildError}");
        }

        return new CampaignBossLearningWitnessResult(
            diveAuthored,
            sustainWallAuthored,
            naiveTags,
            CampaignBossAnswerTagEvaluator.Evaluate(informedSnapshot, learningSpec),
            DescribeAnswerSurface(naiveSnapshot),
            DescribeAnswerSurface(informedSnapshot),
            prep.Rationale);
    }

    private static string DescribeAnswerSurface(BattleLoadoutSnapshot snapshot)
        => string.Join(",", (snapshot.Allies ?? Array.Empty<BattleUnitLoadout>())
            .OrderBy(unit => unit.PreferredAnchor)
            .Select(unit =>
            {
                var modifiers = unit.NumericPackages
                    .Concat(unit.TeamPackages ?? Array.Empty<CombatModifierPackage>())
                    .SelectMany(package => package.Modifiers ?? Array.Empty<StatModifier>());
                var stats = new StatBlock(unit.BaseStats, modifiers);
                return $"{unit.PreferredAnchor}:{unit.ArchetypeId}/{unit.ClassId}:hp={stats.Get(StatKey.MaxHealth):0.0}:armor={stats.Get(StatKey.Armor):0.0}";
            }));

    private static void ArrangeExposedNaiveFormation(GameSessionState session)
    {
        var deployed = session.Profile.Heroes
            .Where(hero => session.ExpeditionSquadHeroIds.Contains(hero.HeroId, StringComparer.Ordinal))
            .ToArray();
        var guard = deployed.Single(hero => string.Equals(hero.ClassId, "vanguard", StringComparison.Ordinal));
        var backliners = deployed
            .Where(hero => hero.ClassId is "ranger" or "mystic")
            .OrderBy(hero => hero.HeroId, StringComparer.Ordinal)
            .ToArray();
        var otherFrontliner = deployed.Single(hero =>
            hero.HeroId != guard.HeroId
            && hero.ClassId is not ("ranger" or "mystic"));
        if (backliners.Length != 2)
        {
            throw new InvalidOperationException($"boss learning witness requires two backliners; actual={backliners.Length}");
        }

        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        session.AssignHeroToAnchor(DeploymentAnchorId.FrontCenter, guard.HeroId);
        session.AssignHeroToAnchor(DeploymentAnchorId.FrontTop, otherFrontliner.HeroId);
        session.AssignHeroToAnchor(DeploymentAnchorId.BackTop, backliners[0].HeroId);
        session.AssignHeroToAnchor(DeploymentAnchorId.BackBottom, backliners[1].HeroId);
    }

    private static void AdvanceToWolfpineBoss(GameSessionState session)
    {
        for (var siteIndex = 0; siteIndex < 2; siteIndex++)
        {
            H100SessionDriver.AdvanceToNextUnclearedSite(session);
            session.BeginNewExpedition();
            while (session.GetSelectedExpeditionNode()?.RequiresBattle == true)
            {
                var node = session.GetSelectedExpeditionNode()!;
                if (string.Equals(node.Id, EncounterId, StringComparison.Ordinal))
                {
                    return;
                }

                session.MarkBattleResolved(victory: true, stepCount: 0, eventCount: 0);
                session.ResolveSelectedExpeditionNode();
            }

            session.ResolveSelectedNodeToRewardSettlement();
            if (session.PendingRewardChoices.Count > 0)
            {
                session.ApplyRewardChoice(0);
            }

            session.ReturnToTownAfterReward();
        }

        throw new InvalidOperationException($"boss learning witness could not reach {EncounterId}.");
    }
}
