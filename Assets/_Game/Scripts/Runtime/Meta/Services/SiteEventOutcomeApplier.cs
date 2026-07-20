using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>
/// 저작된 사건 선택 결과를 입력 순서 그대로 적용한다. 결과에는 런타임 난수, 시각, GUID가 관여하지 않는다.
/// </summary>
public static class SiteEventOutcomeApplier
{
    public static SiteEventOutcomeApplication Apply(
        SiteEventChoiceTemplate choice,
        SiteEventResolutionState state,
        WarWoundSpec woundSpec)
    {
        if (choice == null) throw new ArgumentNullException(nameof(choice));
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (woundSpec == null) throw new ArgumentNullException(nameof(woundSpec));

        var run = state.Run;
        var echo = state.Echo;
        var experience = (state.HeroExperienceById ?? new Dictionary<string, int>())
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var recruitOfferCount = state.RecruitOffersGrantedAtSite;
        var extractBonusEcho = state.ExtractBonusEcho;
        var grantedItems = (state.GrantedItems ?? Array.Empty<SiteEventItemGrant>()).ToList();
        var grantedConsumables = (state.GrantedConsumableIds ?? Array.Empty<string>()).ToList();
        var grantedRecruitOffers = (state.GrantedRecruitOffers ?? Array.Empty<SiteEventRecruitOffer>()).ToList();
        var selectedRouteNodeId = state.SelectedRouteNodeId ?? string.Empty;
        var legalRouteNodeIds = (state.LegalRouteNodeIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var affectedHeroIds = new List<string>();

        foreach (var outcome in choice.Outcomes ?? Array.Empty<SiteEventOutcomeTemplate>())
        {
            if (outcome == null)
            {
                return SiteEventOutcomeApplication.Fail(state, $"Choice '{choice.Id}' contains a null outcome.");
            }

            switch (outcome.Kind)
            {
                case OutcomeKind.GrantItem:
                    if (string.IsNullOrWhiteSpace(outcome.PayloadId))
                    {
                        return SiteEventOutcomeApplication.Fail(state, "GrantItem requires PayloadId.");
                    }

                    for (var index = 0; index < Math.Max(1, outcome.Amount); index++)
                    {
                        grantedItems.Add(new SiteEventItemGrant(outcome.PayloadId, outcome.AuxiliaryId));
                    }
                    break;

                case OutcomeKind.GrantEcho:
                    if (!TryAddNonNegative(echo, outcome.Amount, out echo))
                    {
                        return SiteEventOutcomeApplication.Fail(state, "The event choice would make Echo negative or overflow.");
                    }
                    break;

                case OutcomeKind.GrantExp:
                {
                    var heroId = ResolveTargetHeroId(run, experience.Keys, outcome.PayloadId, outcome.TargetRule);
                    if (string.IsNullOrWhiteSpace(heroId))
                    {
                        return SiteEventOutcomeApplication.Fail(state, "GrantExp could not resolve a deterministic target hero.");
                    }

                    var current = experience.TryGetValue(heroId, out var value) ? value : 0;
                    try
                    {
                        experience[heroId] = checked(current + outcome.Amount);
                    }
                    catch (OverflowException)
                    {
                        return SiteEventOutcomeApplication.Fail(state, "GrantExp overflowed the target hero experience.");
                    }

                    affectedHeroIds.Add(heroId);
                    break;
                }

                case OutcomeKind.CureWound:
                {
                    var resolution = WarWoundResolutionService.CureOrdinal(run);
                    run = resolution.UpdatedRun;
                    affectedHeroIds.AddRange(resolution.AppliedHeroIds);
                    break;
                }

                case OutcomeKind.InflictWound:
                {
                    var resolution = WarWoundResolutionService.InflictOrdinalFrontliner(run, woundSpec);
                    if (resolution.AppliedHeroIds.Count == 0)
                    {
                        return SiteEventOutcomeApplication.Fail(state, "InflictWound could not resolve an unwounded deployed frontliner.");
                    }

                    run = resolution.UpdatedRun;
                    affectedHeroIds.AddRange(resolution.AppliedHeroIds);
                    break;
                }

                case OutcomeKind.RouteToNode:
                    if (string.IsNullOrWhiteSpace(outcome.PayloadId)
                        || !legalRouteNodeIds.Contains(outcome.PayloadId))
                    {
                        return SiteEventOutcomeApplication.Fail(
                            state,
                            $"RouteToNode target '{outcome.PayloadId}' is not a legal authored edge.");
                    }

                    selectedRouteNodeId = outcome.PayloadId;
                    break;

                case OutcomeKind.GrantRecruitOffer:
                    if (string.IsNullOrWhiteSpace(outcome.PayloadId))
                    {
                        return SiteEventOutcomeApplication.Fail(state, "GrantRecruitOffer requires PayloadId.");
                    }

                    if (recruitOfferCount >= Math.Max(0, state.RecruitOffersPerSiteMax))
                    {
                        return SiteEventOutcomeApplication.Fail(state, "RecruitOffersPerSiteMax has already been consumed.");
                    }

                    recruitOfferCount += 1;
                    grantedRecruitOffers.Add(new SiteEventRecruitOffer(outcome.PayloadId, outcome.AuxiliaryId));
                    break;

                case OutcomeKind.GrantConsumable:
                    if (string.IsNullOrWhiteSpace(outcome.PayloadId))
                    {
                        return SiteEventOutcomeApplication.Fail(state, "GrantConsumable requires PayloadId.");
                    }

                    for (var index = 0; index < Math.Max(1, outcome.Amount); index++)
                    {
                        grantedConsumables.Add(outcome.PayloadId);
                    }
                    break;

                case OutcomeKind.ExtractBonus:
                    try
                    {
                        extractBonusEcho = checked(extractBonusEcho + outcome.Amount);
                    }
                    catch (OverflowException)
                    {
                        return SiteEventOutcomeApplication.Fail(state, "ExtractBonus overflowed.");
                    }
                    break;

                default:
                    return SiteEventOutcomeApplication.Fail(state, $"Unsupported outcome kind '{outcome.Kind}'.");
            }
        }

        var updated = state with
        {
            Run = run,
            Echo = echo,
            HeroExperienceById = experience,
            RecruitOffersGrantedAtSite = recruitOfferCount,
            ExtractBonusEcho = extractBonusEcho,
            GrantedItems = grantedItems,
            GrantedConsumableIds = grantedConsumables,
            GrantedRecruitOffers = grantedRecruitOffers,
            SelectedRouteNodeId = selectedRouteNodeId,
        };
        return SiteEventOutcomeApplication.Success(
            updated,
            affectedHeroIds.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static string ResolveTargetHeroId(
        ActiveRunState run,
        IEnumerable<string> knownHeroIds,
        string authoredHeroId,
        OutcomeTargetRule targetRule)
    {
        var known = knownHeroIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(authoredHeroId) && known.Contains(authoredHeroId))
        {
            return authoredHeroId;
        }

        var frontlineOnly = targetRule == OutcomeTargetRule.LowestDeployIndexFrontline;
        var assigned = (run.Blueprint?.DeploymentAssignments
                        ?? new Dictionary<DeploymentAnchorId, string>())
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Where(pair => !frontlineOnly || pair.Key.IsFrontRow())
            .OrderBy(pair => (int)pair.Key)
            .Select(pair => pair.Value)
            .FirstOrDefault(known.Contains);
        if (!string.IsNullOrWhiteSpace(assigned))
        {
            return assigned;
        }

        return (run.BattleDeployHeroIds ?? Array.Empty<string>())
                   .FirstOrDefault(known.Contains)
               ?? known.OrderBy(id => id, StringComparer.Ordinal).FirstOrDefault()
               ?? string.Empty;
    }

    private static bool TryAddNonNegative(int current, int delta, out int result)
    {
        try
        {
            result = checked(current + delta);
            return result >= 0;
        }
        catch (OverflowException)
        {
            result = current;
            return false;
        }
    }
}
