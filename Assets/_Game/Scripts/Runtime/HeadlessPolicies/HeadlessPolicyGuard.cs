using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>
/// no-cheat observation whitelist와 legal action을 런타임에서 fail-closed로 검사한다.
/// asmdef exact-reference guard와 함께 정책이 session 내부나 authored object에 접근하지 못하게 한다.
/// </summary>
public static class HeadlessPolicyGuard
{
    public static void ValidateObservation(HeadlessPolicyObservation observation)
    {
        if (observation == null)
        {
            throw new ArgumentNullException(nameof(observation));
        }

        RequireText(observation.ChapterId, nameof(observation.ChapterId));
        RequireText(observation.SiteId, nameof(observation.SiteId));
        if (observation.DecisionSeed == 0)
        {
            throw new InvalidOperationException("DecisionSeed must be non-zero and derived before policy execution.");
        }

        if (observation.Roster == null || observation.Anchors == null || observation.EnemyPreview == null || observation.RewardOptions == null)
        {
            throw new InvalidOperationException("Observation collections and current enemy preview must be materialized snapshots.");
        }

        if (observation.DeployCapacity <= 0 || observation.DeployCapacity > observation.Anchors.Count)
        {
            throw new InvalidOperationException($"DeployCapacity must be within visible anchors (capacity={observation.DeployCapacity}, anchors={observation.Anchors.Count}).");
        }

        if (observation.Roster.Count < observation.DeployCapacity)
        {
            throw new InvalidOperationException($"Visible roster cannot fill deploy capacity (roster={observation.Roster.Count}, capacity={observation.DeployCapacity}).");
        }

        RequireDistinct(observation.Anchors, "visible anchor");
        RequireDistinct(observation.Roster.Select(hero => hero.HeroId), "visible hero id");
        foreach (var hero in observation.Roster)
        {
            RequireText(hero.HeroId, nameof(hero.HeroId));
            RequireText(hero.ArchetypeId, nameof(hero.ArchetypeId));
            RequireText(hero.RaceId, nameof(hero.RaceId));
            RequireText(hero.ClassId, nameof(hero.ClassId));
            if (hero.Level <= 0 || hero.CurrentHp < 0 || hero.MaxHp < 0 || hero.EquippedItemCount < 0)
            {
                throw new InvalidOperationException($"Visible hero values are outside legal bounds: {hero.HeroId}.");
            }

            if (hero.MaxHp > 0 && hero.CurrentHp > hero.MaxHp)
            {
                throw new InvalidOperationException($"Visible HP exceeds max HP: {hero.HeroId}.");
            }
        }

        RequireDistinct(observation.CurrentPlacements.Select(value => value.Anchor), "current anchor");
        RequireDistinct(observation.CurrentPlacements.Select(value => value.HeroId), "current deployed hero");
        var visibleHeroIds = observation.Roster.Select(value => value.HeroId).ToHashSet(StringComparer.Ordinal);
        var visibleAnchorIds = observation.Anchors.ToHashSet();
        foreach (var placement in observation.CurrentPlacements)
        {
            if (placement == null
                || !visibleHeroIds.Contains(placement.HeroId)
                || !visibleAnchorIds.Contains(placement.Anchor))
            {
                throw new InvalidOperationException("Current placement is outside the visible roster or anchor set.");
            }
        }

        RequireDistinct(observation.OwnedItems.Select(value => value.Mechanics.ItemInstanceId), "owned item instance");
        foreach (var item in observation.OwnedItems)
        {
            if (item?.Mechanics == null || string.IsNullOrWhiteSpace(item.Mechanics.ItemInstanceId))
            {
                throw new InvalidOperationException("Owned item observations require a stable instance id.");
            }

            if (!string.IsNullOrWhiteSpace(item.EquippedHeroId) && !visibleHeroIds.Contains(item.EquippedHeroId))
            {
                throw new InvalidOperationException("Owned item equip owner must be visible to prep policy.");
            }
        }

        ValidateEnemyPreview(observation.EnemyPreview);
        RequireDistinct(observation.RewardOptions.Select(option => option.Index), "reward option index");
        foreach (var option in observation.RewardOptions)
        {
            if (option.Index < 0 || option.GoldAmount < 0 || option.EchoAmount < 0 || option.PermanentSlotAmount < 0)
            {
                throw new InvalidOperationException($"Reward option contains an illegal visible value: index={option.Index}.");
            }
        }
    }

    public static void ValidateDeploymentDecision(
        HeadlessPolicyObservation observation,
        HeadlessDeploymentDecision decision)
    {
        if (decision == null || decision.Placements == null)
        {
            throw new InvalidOperationException("Policy must return a materialized deployment decision.");
        }

        RequireText(decision.Rationale, nameof(decision.Rationale));
        if (double.IsNaN(decision.EstimatedValue) || double.IsInfinity(decision.EstimatedValue))
        {
            throw new InvalidOperationException("Policy estimated value must be finite.");
        }

        ValidateEvidenceFactIds(decision.EvidenceFactIds);

        var expectedCount = Math.Min(observation.DeployCapacity, observation.Roster.Count);
        if (decision.Placements.Count != expectedCount)
        {
            throw new InvalidOperationException($"Policy must fill the legal deploy cap (expected={expectedCount}, actual={decision.Placements.Count}).");
        }

        RequireDistinct(decision.Placements.Select(value => value.Anchor), "chosen anchor");
        RequireDistinct(decision.Placements.Select(value => value.HeroId), "chosen hero");
        var visibleHeroes = observation.Roster.Select(hero => hero.HeroId).ToHashSet(StringComparer.Ordinal);
        var visibleAnchors = observation.Anchors.ToHashSet();
        foreach (var placement in decision.Placements)
        {
            if (!visibleHeroes.Contains(placement.HeroId) || !visibleAnchors.Contains(placement.Anchor))
            {
                throw new InvalidOperationException($"Policy returned an action outside the visible legal set: {placement.HeroId}@{placement.Anchor}.");
            }
        }
    }

    public static void ValidateRewardDecision(
        HeadlessPolicyObservation observation,
        HeadlessRewardDecision decision)
    {
        if (decision == null)
        {
            throw new InvalidOperationException("Policy must return a reward decision.");
        }

        RequireText(decision.Rationale, nameof(decision.Rationale));
        if (double.IsNaN(decision.EstimatedValue) || double.IsInfinity(decision.EstimatedValue))
        {
            throw new InvalidOperationException("Policy estimated value must be finite.");
        }

        ValidateEvidenceFactIds(decision.EvidenceFactIds);

        if (observation.RewardOptions.Count == 0)
        {
            if (decision.OptionIndex != -1)
            {
                throw new InvalidOperationException("A no-reward observation must return option index -1.");
            }

            return;
        }

        if (observation.RewardOptions.All(option => option.Index != decision.OptionIndex))
        {
            throw new InvalidOperationException($"Policy chose a reward outside the visible legal set: {decision.OptionIndex}.");
        }
    }

    private static void ValidateEnemyPreview(HeadlessEnemyPreview preview)
    {
        if (!preview.IsAvailable)
        {
            if (preview.Units.Count != 0 || preview.ThreatSkulls != 0)
            {
                throw new InvalidOperationException("Unavailable enemy preview cannot carry partial hidden unit or threat data.");
            }

            return;
        }

        RequireText(preview.EncounterId, nameof(preview.EncounterId));
        if (preview.ThreatSkulls <= 0)
        {
            throw new InvalidOperationException("Available enemy preview must expose its player-facing threat band.");
        }

        foreach (var unit in preview.Units)
        {
            RequireText(unit.ArchetypeId, nameof(unit.ArchetypeId));
            RequireText(unit.RaceId, nameof(unit.RaceId));
            RequireText(unit.ClassId, nameof(unit.ClassId));
        }
    }

    private static void ValidateEvidenceFactIds(IReadOnlyList<string> evidenceFactIds)
    {
        if (evidenceFactIds == null || evidenceFactIds.Count == 0)
        {
            throw new HeadlessPolicyEvidenceException("Every policy decision must cite at least one player-visible fact id.");
        }

        if (evidenceFactIds.Any(string.IsNullOrWhiteSpace)
            || evidenceFactIds.Distinct(StringComparer.Ordinal).Count() != evidenceFactIds.Count)
        {
            throw new HeadlessPolicyEvidenceException("Policy evidence fact ids must be non-empty and distinct.");
        }
    }

    private static void RequireDistinct<T>(IEnumerable<T> values, string label)
    {
        var materialized = values.ToArray();
        if (materialized.Distinct().Count() != materialized.Length)
        {
            throw new InvalidOperationException($"Observation/decision contains duplicate {label} values.");
        }
    }

    private static void RequireText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label} must be visible, stable text.");
        }
    }
}
