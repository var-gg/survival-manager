using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.HeadlessMetrics;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>캡처된 profile을 복원해 한 sunken 편성·배치를 campaign과 같은 site path/seed로 재생한다.</summary>
internal static class H100SunkenSiteRunner
{
    private const int MaxBattleNodesPerSite = 64;

    public static SunkenOracleCandidateRecord Run(
        RuntimeCombatContentLookup lookup,
        string runId,
        H100SunkenCapturedArrival arrival,
        string profileSnapshot,
        H100SunkenOracleCase oracleCase,
        int maxBattleSteps,
        H100SunkenLookbackVariant? lookbackVariant = null)
    {
        var battleSeeds = new List<int>();
        var replayHashes = new List<string>();
        var battleWins = 0;
        var finalTeamHpFraction = 0d;
        var failureEncounterId = string.Empty;
        var failureCode = string.Empty;
        var siteCompleted = false;

        try
        {
            var session = lookbackVariant == null
                ? H100SessionDriver.CreateSession(lookup, H100ProfileSnapshotCodec.Restore(profileSnapshot))
                : RebuildLookbackSession(lookup, lookbackVariant);
            if (!string.Equals(session.SelectedCampaignSiteId, H100SunkenDiagnosisSettings.TargetSiteId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Profile selected site is '{session.SelectedCampaignSiteId}', expected '{H100SunkenDiagnosisSettings.TargetSiteId}'.");
            }

            ApplyDeployment(session, oracleCase);
            session.BeginNewExpedition();
            var localBattleIndex = 0;
            while (session.GetSelectedExpeditionNode()?.RequiresBattle == true)
            {
                if (localBattleIndex >= MaxBattleNodesPerSite)
                {
                    failureCode = "battle-node-safety-exhausted";
                    break;
                }

                if (!session.TryBuildSelectedBattleState(out _, out var encounter, out var allySnapshot, out var buildError))
                {
                    failureCode = $"build:{buildError}";
                    break;
                }

                failureEncounterId = encounter.Context.EncounterId;
                var battleSeed = H100SessionDriver.DeriveSeed(
                    encounter.Context.BattleContextHash,
                    arrival.CampaignSeed + arrival.BattleStartIndex + localBattleIndex);
                var seededEncounter = encounter with { Context = encounter.Context with { BattleSeed = battleSeed } };
                if (!session.TryComposeBattleState(allySnapshot, seededEncounter, out var state, out var composeError))
                {
                    failureCode = $"compose:{composeError}";
                    break;
                }

                battleSeeds.Add(battleSeed);
                var result = BattleResolver.Run(state, maxBattleSteps);
                replayHashes.Add(ReplayHash.Compute(state, result.ActivityTelemetry));
                localBattleIndex++;
                finalTeamHpFraction = ResolveAllyHpFraction(result.FinalUnits);
                var won = result.Winner == TeamSide.Ally;
                session.MarkBattleResolved(won, result.StepCount, result.Events.Count, result.FinalUnits);
                if (!won)
                {
                    break;
                }

                battleWins++;
                session.ResolveSelectedExpeditionNode();
            }

            if (string.IsNullOrWhiteSpace(failureCode)
                && battleWins == battleSeeds.Count
                && session.GetSelectedExpeditionNode()?.RequiresBattle != true)
            {
                siteCompleted = session.ResolveSelectedNodeToRewardSettlement();
                if (!siteCompleted)
                {
                    failureCode = "reward-settlement-unavailable";
                }
            }
        }
        catch (Exception exception)
        {
            failureCode = FailureCode(exception);
        }

        return new SunkenOracleCandidateRecord
        {
            RunId = runId,
            SampleId = arrival.Snapshot.SampleId,
            PolicyId = arrival.Snapshot.PolicyId,
            CampaignSeed = arrival.CampaignSeed,
            Scope = oracleCase.Scope,
            StateVariantId = oracleCase.StateVariantId,
            CandidateId = oracleCase.CandidateId,
            BuildId = oracleCase.BuildId,
            PlacementId = oracleCase.PlacementId,
            CounterFamilyId = oracleCase.CounterFamilyId,
            HeroIds = oracleCase.Members.Select(value => value.HeroId).ToArray(),
            ArchetypeIds = oracleCase.Members.Select(value => value.ArchetypeId).ToArray(),
            AnchorIds = oracleCase.Members.Select(value => (int)value.Anchor).ToArray(),
            BattleSeeds = battleSeeds,
            IsPolicyChoice = oracleCase.IsPolicyChoice,
            AddedRosterArchetypeId = oracleCase.AddedRosterArchetypeId,
            RewardOptionIndex = oracleCase.RewardOptionIndex,
            RewardPayloadId = oracleCase.RewardPayloadId,
            SiteCompleted = siteCompleted,
            BattleCount = battleSeeds.Count,
            BattleWinCount = battleWins,
            BattleWinRate = battleSeeds.Count == 0 ? 0d : (double)battleWins / battleSeeds.Count,
            FinalTeamHpFraction = finalTeamHpFraction,
            FailureEncounterId = siteCompleted ? string.Empty : failureEncounterId,
            FailureCode = failureCode,
            ReplayManifestHash = ReplayHash.ComputeManifest(replayHashes),
        };
    }

    private static void ApplyDeployment(GameSessionState session, H100SunkenOracleCase oracleCase)
    {
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        var requiredHeroIds = oracleCase.Members
            .Select(value => value.HeroId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var heroId in requiredHeroIds.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (session.ExpeditionSquadHeroIds.Contains(heroId, StringComparer.Ordinal))
            {
                continue;
            }

            var removable = session.ExpeditionSquadHeroIds
                .Where(value => !requiredHeroIds.Contains(value))
                .OrderByDescending(value => value, StringComparer.Ordinal)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(removable) && !session.ToggleExpeditionHero(removable))
            {
                throw new InvalidOperationException($"Could not remove expedition hero '{removable}'.");
            }

            if (!session.ToggleExpeditionHero(heroId))
            {
                throw new InvalidOperationException($"Could not add expedition hero '{heroId}'.");
            }
        }

        foreach (var member in oracleCase.Members.OrderBy(value => value.Anchor))
        {
            if (!session.AssignHeroToAnchor(member.Anchor, member.HeroId))
            {
                throw new InvalidOperationException($"Could not assign oracle member '{member.HeroId}' to '{member.Anchor}'.");
            }
        }
    }

    private static GameSessionState RebuildLookbackSession(
        RuntimeCombatContentLookup lookup,
        H100SunkenLookbackVariant variant)
    {
        var session = H100SessionDriver.CreateSession(
            lookup,
            H100ProfileSnapshotCodec.Restore(variant.SourceProfileSnapshot));
        if (!session.ApplyRewardChoice(variant.RewardOptionIndex))
        {
            throw new InvalidOperationException(
                $"Could not replay lookback reward option {variant.RewardOptionIndex} for '{variant.VariantId}'.");
        }

        session.ReturnToTownAfterReward();
        if (variant.RecruitOfferIndex >= 0)
        {
            var recruit = session.Recruit(variant.RecruitOfferIndex);
            if (!recruit.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Could not replay lookback recruit option {variant.RecruitOfferIndex} for '{variant.VariantId}': {recruit.Error}");
            }
        }

        H100SessionDriver.AdvanceToNextUnclearedSite(session);
        return session;
    }

    private static double ResolveAllyHpFraction(IReadOnlyList<BattleUnitReadModel> finalUnits)
    {
        var allies = finalUnits
            .Where(value => value.Side == TeamSide.Ally && value.EntityKind == CombatEntityKind.RosterUnit)
            .ToArray();
        var maxHp = allies.Sum(value => Math.Max(0f, value.MaxHealth));
        return maxHp <= 0f
            ? 0d
            : allies.Sum(value => Math.Max(0f, value.CurrentHealth)) / maxHp;
    }

    private static string FailureCode(Exception exception)
    {
        var message = exception.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (message.Length > 160)
        {
            message = message.Substring(0, 160);
        }

        return string.IsNullOrWhiteSpace(message)
            ? $"exception:{exception.GetType().Name}"
            : $"exception:{exception.GetType().Name}:{message}";
    }
}
