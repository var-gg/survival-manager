using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.Combat.Model;
using SM.HeadlessPolicies;

namespace SM.SealedLlmBridge;

/// <summary>
/// Canonical selected_action grammar:
/// deployment = ordinal anchor-name order의 <c>anchorId=heroId</c> pairs joined by <c>;</c>;
/// reward = canonical decimal option index; recruit = canonical decimal offer index or <c>skip</c>;
/// passive = <c>heroId:boardId:nodeId</c> or <c>skip</c>;
/// refit = <c>itemInstanceId:slotIndex</c> or <c>skip</c>.
/// Whitespace, aliases, leading zeroes/signs, reordered deployment pairs, and reserved delimiters in IDs are rejected.
/// </summary>
public static class SealedLlmActionCodec
{
    private const string PrepFormationPrefix = "formation#";
    private const string PrepEquipmentPrefix = "equipment#";

    public static string EncodeDeployment(HeadlessDeploymentDecision decision)
    {
        if (decision?.Placements == null)
        {
            throw new ArgumentNullException(nameof(decision));
        }

        var pairs = decision.Placements
            .Select(placement => placement
                                 ?? throw new ArgumentException("Deployment placements cannot contain null.", nameof(decision)))
            .Select(placement => new
            {
                Anchor = placement.Anchor,
                AnchorId = SealedLlmCanonicalValue.EnumName(placement.Anchor, nameof(placement.Anchor)),
                placement.HeroId,
            })
            .OrderBy(value => value.AnchorId, StringComparer.Ordinal)
            .ToArray();
        if (pairs.Select(value => value.Anchor).Distinct().Count() != pairs.Length
            || pairs.Select(value => value.HeroId).Distinct(StringComparer.Ordinal).Count() != pairs.Length)
        {
            throw new ArgumentException("Deployment decisions cannot contain duplicate anchors or heroes.", nameof(decision));
        }

        return string.Join(";", pairs.Select(value =>
            SealedLlmActionGrammar.DeploymentPair(value.Anchor, value.HeroId)));
    }

    public static HeadlessDeploymentDecision DecodeDeployment(
        HeadlessPolicyObservation observation,
        string selectedAction)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        var action = RequireAction(selectedAction);
        var visibleHeroes = observation.Roster.Select(hero => hero.HeroId).ToHashSet(StringComparer.Ordinal);
        var visibleAnchors = observation.Anchors.ToHashSet();
        var placements = new List<HeadlessPlacement>();
        var usedHeroes = new HashSet<string>(StringComparer.Ordinal);
        var usedAnchors = new HashSet<DeploymentAnchorId>();
        string previousAnchorId = null;
        foreach (var pair in action.Split(new[] { ';' }, StringSplitOptions.None))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0 || separator != pair.LastIndexOf('=') || separator == pair.Length - 1)
            {
                throw DecodeError(
                    SealedLlmActionDecodeReason.MalformedGrammar,
                    selectedAction,
                    "Deployment action must contain exact anchorId=heroId pairs.");
            }

            var anchorId = pair.Substring(0, separator);
            var heroId = pair.Substring(separator + 1);
            RequireGrammarToken(anchorId, selectedAction, '=', ';');
            RequireGrammarToken(heroId, selectedAction, '=', ';');
            if (!Enum.TryParse(anchorId, false, out DeploymentAnchorId anchor)
                || !string.Equals(Enum.GetName(typeof(DeploymentAnchorId), anchor), anchorId, StringComparison.Ordinal))
            {
                throw DecodeError(
                    SealedLlmActionDecodeReason.OffMenu,
                    selectedAction,
                    $"Deployment anchor '{anchorId}' is not a canonical visible anchor.");
            }

            if (previousAnchorId != null
                && StringComparer.Ordinal.Compare(previousAnchorId, anchorId) >= 0)
            {
                var reason = string.Equals(previousAnchorId, anchorId, StringComparison.Ordinal)
                    ? SealedLlmActionDecodeReason.DuplicateAssignment
                    : SealedLlmActionDecodeReason.NonCanonicalOrder;
                throw DecodeError(reason, selectedAction, "Deployment pairs must be strictly ordinal-sorted by anchorId.");
            }

            if (!usedAnchors.Add(anchor) || !usedHeroes.Add(heroId))
            {
                throw DecodeError(
                    SealedLlmActionDecodeReason.DuplicateAssignment,
                    selectedAction,
                    "Deployment action assigns an anchor or hero more than once.");
            }

            if (!visibleAnchors.Contains(anchor) || !visibleHeroes.Contains(heroId))
            {
                throw DecodeError(
                    SealedLlmActionDecodeReason.OffMenu,
                    selectedAction,
                    $"Deployment pair '{pair}' is outside the visible action space.");
            }

            placements.Add(new HeadlessPlacement(anchor, heroId));
            previousAnchorId = anchorId;
        }

        var decision = new HeadlessDeploymentDecision(
            placements,
            SealedLlmDecisionEnvelope.Rationale,
            0d,
            SealedLlmDecisionEnvelope.EvidenceFactIds);
        ValidateDecoded(
            selectedAction,
            () => HeadlessPolicyGuard.ValidateDeploymentDecision(observation, decision));
        return decision;
    }

    public static string EncodePrep(HeadlessPrepDecision decision)
    {
        if (decision == null) throw new ArgumentNullException(nameof(decision));
        var deployment = EncodeDeployment(new HeadlessDeploymentDecision(
            decision.Placements,
            decision.Rationale,
            decision.EstimatedValue,
            decision.EvidenceFactIds));
        var equipment = decision.EquipmentAssignments.Count == 0
            ? SealedLlmActionGrammar.Skip
            : string.Join(",", decision.EquipmentAssignments
                .OrderBy(value => value.ItemInstanceId, StringComparer.Ordinal)
                .Select(value => SealedLlmActionGrammar.PrepEquipmentPair(value.ItemInstanceId, value.HeroId)));
        return $"{PrepFormationPrefix}{deployment}|{PrepEquipmentPrefix}{equipment}";
    }

    public static HeadlessPrepDecision DecodePrep(
        HeadlessPolicyObservation observation,
        string selectedAction)
    {
        var action = RequireAction(selectedAction);
        var separator = action.IndexOf('|');
        if (!action.StartsWith(PrepFormationPrefix, StringComparison.Ordinal)
            || separator <= PrepFormationPrefix.Length
            || !action.Substring(separator + 1).StartsWith(PrepEquipmentPrefix, StringComparison.Ordinal)
            || separator != action.LastIndexOf('|'))
        {
            throw DecodeError(
                SealedLlmActionDecodeReason.MalformedGrammar,
                selectedAction,
                "Prep action must be formation#<deployment>|equipment#<skip-or-item=hero-list>.");
        }

        var deploymentAction = action.Substring(PrepFormationPrefix.Length, separator - PrepFormationPrefix.Length);
        var deployment = DecodeDeployment(observation, deploymentAction);
        var equipmentAction = action.Substring(separator + 1 + PrepEquipmentPrefix.Length);
        var assignments = new List<HeadlessEquipmentAssignment>();
        if (!string.Equals(equipmentAction, SealedLlmActionGrammar.Skip, StringComparison.Ordinal))
        {
            string previousItemId = null;
            foreach (var pair in equipmentAction.Split(new[] { ',' }, StringSplitOptions.None))
            {
                var equals = pair.IndexOf('=');
                if (equals <= 0 || equals != pair.LastIndexOf('=') || equals == pair.Length - 1)
                {
                    throw DecodeError(SealedLlmActionDecodeReason.MalformedGrammar, selectedAction, "Prep equipment entries must be itemInstanceId=heroId.");
                }

                var itemId = pair.Substring(0, equals);
                var heroId = pair.Substring(equals + 1);
                RequireGrammarToken(itemId, selectedAction, '=', ',', '#', '|');
                RequireGrammarToken(heroId, selectedAction, '=', ',', '#', '|');
                if (previousItemId != null && StringComparer.Ordinal.Compare(previousItemId, itemId) >= 0)
                {
                    throw DecodeError(SealedLlmActionDecodeReason.NonCanonicalOrder, selectedAction, "Prep equipment entries must be strictly item-id sorted.");
                }

                assignments.Add(new HeadlessEquipmentAssignment(itemId, heroId));
                previousItemId = itemId;
            }
        }

        var decision = new HeadlessPrepDecision(
            deployment.Placements,
            assignments,
            SealedLlmDecisionEnvelope.Rationale,
            0d,
            SealedLlmDecisionEnvelope.EvidenceFactIds);
        ValidateDecoded(selectedAction, () => HeadlessPrepPolicyGuard.ValidateDecision(observation, decision));
        return decision;
    }

    public static string EncodeReward(HeadlessRewardDecision decision)
        => SealedLlmCanonicalValue.Invariant(
            (decision ?? throw new ArgumentNullException(nameof(decision))).OptionIndex);

    public static HeadlessRewardDecision DecodeReward(
        HeadlessPolicyObservation observation,
        string selectedAction)
    {
        var action = RequireAction(selectedAction);
        var optionIndex = ParseCanonicalInteger(action, selectedAction);
        if (!SealedLlmLegalActionSet.RewardKeys(observation).Contains(action, StringComparer.Ordinal))
        {
            throw DecodeError(
                SealedLlmActionDecodeReason.OffMenu,
                selectedAction,
                $"Reward option '{action}' is outside the current legal menu.");
        }

        var decision = new HeadlessRewardDecision(
            optionIndex,
            SealedLlmDecisionEnvelope.Rationale,
            0d,
            SealedLlmDecisionEnvelope.EvidenceFactIds);
        ValidateDecoded(selectedAction, () => HeadlessPolicyGuard.ValidateRewardDecision(observation, decision));
        return decision;
    }

    public static string EncodeRecruit(HeadlessRecruitDecision decision)
    {
        if (decision == null) throw new ArgumentNullException(nameof(decision));
        return decision.IsNoOp
            ? SealedLlmActionGrammar.Skip
            : SealedLlmCanonicalValue.Invariant(decision.OfferIndex);
    }

    public static HeadlessRecruitDecision DecodeRecruit(
        HeadlessRosterPolicyObservation observation,
        string selectedAction)
    {
        var action = RequireAction(selectedAction);
        var offerIndex = -1;
        if (!string.Equals(action, SealedLlmActionGrammar.Skip, StringComparison.Ordinal))
        {
            offerIndex = ParseCanonicalNonNegativeInteger(action, selectedAction);
        }

        if (!SealedLlmLegalActionSet.RecruitKeys(observation).Contains(action, StringComparer.Ordinal))
        {
            throw DecodeError(
                SealedLlmActionDecodeReason.OffMenu,
                selectedAction,
                $"Recruit action '{action}' is outside the current legal menu.");
        }

        var decision = new HeadlessRecruitDecision(
            offerIndex,
            SealedLlmDecisionEnvelope.Rationale,
            0d,
            SealedLlmDecisionEnvelope.EvidenceFactIds);
        ValidateDecoded(selectedAction, () => HeadlessRosterPolicyGuard.ValidateRecruitDecision(observation, decision));
        return decision;
    }

    public static string EncodePassive(HeadlessPassiveDecision decision)
    {
        if (decision == null) throw new ArgumentNullException(nameof(decision));
        return decision.IsNoOp
            ? SealedLlmActionGrammar.Skip
            : SealedLlmActionGrammar.PassiveKey(decision.HeroId, decision.BoardId, decision.NodeId);
    }

    public static HeadlessPassiveDecision DecodePassive(
        HeadlessRosterPolicyObservation observation,
        string selectedAction)
    {
        var action = RequireAction(selectedAction);
        string heroId;
        string boardId;
        string nodeId;
        if (string.Equals(action, SealedLlmActionGrammar.Skip, StringComparison.Ordinal))
        {
            heroId = string.Empty;
            boardId = string.Empty;
            nodeId = string.Empty;
        }
        else
        {
            var parts = action.Split(new[] { ':' }, StringSplitOptions.None);
            if (parts.Length != 3)
            {
                throw DecodeError(
                    SealedLlmActionDecodeReason.MalformedGrammar,
                    selectedAction,
                    "Passive action must be heroId:boardId:nodeId or skip.");
            }

            foreach (var part in parts) RequireGrammarToken(part, selectedAction, ':');
            heroId = parts[0];
            boardId = parts[1];
            nodeId = parts[2];
        }

        if (!SealedLlmLegalActionSet.PassiveKeys(observation).Contains(action, StringComparer.Ordinal))
        {
            throw DecodeError(
                SealedLlmActionDecodeReason.OffMenu,
                selectedAction,
                $"Passive action '{action}' is outside the current legal menu.");
        }

        var decision = new HeadlessPassiveDecision(
            heroId,
            boardId,
            nodeId,
            SealedLlmDecisionEnvelope.Rationale,
            0d,
            SealedLlmDecisionEnvelope.EvidenceFactIds);
        ValidateDecoded(selectedAction, () => HeadlessRosterPolicyGuard.ValidatePassiveDecision(observation, decision));
        return decision;
    }

    public static string EncodeRefit(HeadlessRefitDecision decision)
    {
        if (decision == null) throw new ArgumentNullException(nameof(decision));
        return decision.IsNoOp
            ? SealedLlmActionGrammar.Skip
            : SealedLlmActionGrammar.RefitKey(decision.ItemInstanceId, decision.AffixSlotIndex);
    }

    public static HeadlessRefitDecision DecodeRefit(
        HeadlessRosterPolicyObservation observation,
        string selectedAction)
    {
        var action = RequireAction(selectedAction);
        string itemInstanceId;
        var slotIndex = -1;
        if (string.Equals(action, SealedLlmActionGrammar.Skip, StringComparison.Ordinal))
        {
            itemInstanceId = string.Empty;
        }
        else
        {
            var parts = action.Split(new[] { ':' }, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                throw DecodeError(
                    SealedLlmActionDecodeReason.MalformedGrammar,
                    selectedAction,
                    "Refit action must be itemInstanceId:slotIndex or skip.");
            }

            RequireGrammarToken(parts[0], selectedAction, ':');
            itemInstanceId = parts[0];
            slotIndex = ParseCanonicalNonNegativeInteger(parts[1], selectedAction);
        }

        if (!SealedLlmLegalActionSet.RefitKeys(observation).Contains(action, StringComparer.Ordinal))
        {
            throw DecodeError(
                SealedLlmActionDecodeReason.OffMenu,
                selectedAction,
                $"Refit action '{action}' is outside the current legal menu.");
        }

        var decision = new HeadlessRefitDecision(
            itemInstanceId,
            slotIndex,
            SealedLlmDecisionEnvelope.Rationale,
            0d,
            SealedLlmDecisionEnvelope.EvidenceFactIds);
        ValidateDecoded(selectedAction, () => HeadlessRosterPolicyGuard.ValidateRefitDecision(observation, decision));
        return decision;
    }

    private static string RequireAction(string selectedAction)
    {
        if (selectedAction == null)
        {
            throw DecodeError(SealedLlmActionDecodeReason.NullAction, null, "selected_action cannot be null.");
        }

        if (selectedAction.Length == 0 || selectedAction.Any(char.IsWhiteSpace))
        {
            throw DecodeError(
                SealedLlmActionDecodeReason.MalformedGrammar,
                selectedAction,
                "selected_action cannot be empty or contain whitespace.");
        }

        return selectedAction;
    }

    private static int ParseCanonicalInteger(string value, string selectedAction)
    {
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result)
            || !string.Equals(SealedLlmCanonicalValue.Invariant(result), value, StringComparison.Ordinal))
        {
            throw DecodeError(
                SealedLlmActionDecodeReason.MalformedGrammar,
                selectedAction,
                $"'{value}' is not a canonical invariant integer.");
        }

        return result;
    }

    private static int ParseCanonicalNonNegativeInteger(string value, string selectedAction)
    {
        var result = ParseCanonicalInteger(value, selectedAction);
        if (result < 0)
        {
            throw DecodeError(
                SealedLlmActionDecodeReason.MalformedGrammar,
                selectedAction,
                $"'{value}' must be non-negative.");
        }

        return result;
    }

    private static void RequireGrammarToken(string value, string selectedAction, params char[] reserved)
    {
        try
        {
            SealedLlmActionGrammar.RequireToken(value, nameof(selectedAction), reserved);
        }
        catch (InvalidOperationException exception)
        {
            throw DecodeError(
                SealedLlmActionDecodeReason.MalformedGrammar,
                selectedAction,
                exception.Message,
                exception);
        }
    }

    private static void ValidateDecoded(string selectedAction, Action validate)
    {
        try
        {
            validate();
        }
        catch (InvalidOperationException exception)
        {
            throw DecodeError(
                SealedLlmActionDecodeReason.OffMenu,
                selectedAction,
                "Decoded action failed the production policy guard.",
                exception);
        }
    }

    private static SealedLlmActionDecodeException DecodeError(
        SealedLlmActionDecodeReason reason,
        string selectedAction,
        string message,
        Exception innerException = null)
        => new(reason, selectedAction, message, innerException);
}
