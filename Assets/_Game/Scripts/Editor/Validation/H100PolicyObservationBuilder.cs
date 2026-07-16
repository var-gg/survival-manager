using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessPolicies;
using SM.Meta.Model;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>
/// Unity/session boundary를 player-visible policy DTO로 투영하는 유일한 adapter.
/// 현재 node preview만 읽고 future node traversal이나 resolved encounter stat을 수행하지 않는다.
/// </summary>
internal static class H100PolicyObservationBuilder
{
    public static HeadlessPolicyObservation Build(
        GameSessionState session,
        RuntimeCombatContentLookup lookup,
        int decisionSeed)
    {
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var error))
        {
            throw new InvalidOperationException($"Cannot build H100 policy observation: {error}");
        }

        var deployed = session.BattleDeployHeroIds.ToHashSet(StringComparer.Ordinal);
        var progressionByHero = session.Profile.HeroProgressions
            .Where(record => !string.IsNullOrWhiteSpace(record.HeroId))
            .GroupBy(record => record.HeroId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var rosterById = session.Profile.Heroes
            .Where(hero => !string.IsNullOrWhiteSpace(hero.HeroId))
            .GroupBy(hero => hero.HeroId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var roster = session.ExpeditionSquadHeroIds
            .Where(rosterById.ContainsKey)
            .Select(heroId =>
            {
                var hero = rosterById[heroId];
                snapshot.Archetypes.TryGetValue(hero.ArchetypeId, out var archetype);
                var equippedItemCount = hero.EquippedItemIds
                    .Concat(session.Profile.Inventory
                        .Where(item => string.Equals(item.EquippedHeroId, hero.HeroId, StringComparison.Ordinal))
                        .Select(item => item.ItemInstanceId))
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                return new HeadlessHeroObservation(
                    hero.HeroId,
                    hero.ArchetypeId,
                    hero.RaceId,
                    hero.ClassId,
                    archetype?.RoleTag ?? string.Empty,
                    progressionByHero.TryGetValue(hero.HeroId, out var progression) ? progression.Level : 1,
                    hero.CurrentHp,
                    hero.MaxHp,
                    equippedItemCount,
                    deployed.Contains(hero.HeroId),
                    archetype?.DefaultAnchor ?? ResolveClassAnchor(hero.ClassId));
            })
            .ToArray();

        var observation = new HeadlessPolicyObservation(
            decisionSeed,
            MetaBalanceDefaults.BattleDeployCap,
            session.SelectedCampaignChapterId,
            session.SelectedCampaignSiteId,
            roster,
            session.DeploymentAnchors.ToArray(),
            BuildCurrentEnemyPreview(session.GetSelectedExpeditionNode(), snapshot),
            session.PendingRewardChoices
                .Select((option, index) => new HeadlessRewardOption(
                    index,
                    MapRewardKind(option.Kind),
                    option.PayloadId,
                    option.GoldAmount,
                    option.EchoAmount,
                    option.PermanentSlotAmount))
                .ToArray());
        HeadlessPolicyGuard.ValidateObservation(observation);
        return observation;
    }

    private static HeadlessEnemyPreview BuildCurrentEnemyPreview(
        ExpeditionNodeViewModel? currentNode,
        CombatContentSnapshot snapshot)
    {
        if (currentNode is not { RequiresBattle: true }
            || snapshot.Encounters is not { } encounters
            || !encounters.TryGetValue(currentNode.Id, out var encounter))
        {
            return HeadlessEnemyPreview.Unavailable;
        }

        var members = Array.Empty<EnemySquadMemberTemplate>();
        IReadOnlyList<string> squadRewardTags = Array.Empty<string>();
        if (snapshot.EnemySquads is { } squads
            && squads.TryGetValue(encounter.EnemySquadTemplateId, out var squad))
        {
            members = (squad.Members ?? Array.Empty<EnemySquadMemberTemplate>()).ToArray();
            squadRewardTags = squad.RewardDropTags ?? Array.Empty<string>();
        }

        var units = members.Select(member =>
        {
            snapshot.Archetypes.TryGetValue(member.ArchetypeId, out var archetype);
            return new HeadlessEnemyUnitPreview(
                member.ArchetypeId,
                archetype?.RaceId ?? string.Empty,
                archetype?.ClassId ?? string.Empty,
                archetype?.RoleTag ?? string.Empty,
                archetype?.DefaultAnchor ?? ResolveClassAnchor(archetype?.ClassId ?? string.Empty));
        }).ToArray();

        var bossAura = string.Empty;
        var bossUtility = string.Empty;
        IReadOnlyList<string> bossRewardTags = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(encounter.BossOverlayId)
            && snapshot.BossOverlays is { } overlays
            && overlays.TryGetValue(encounter.BossOverlayId, out var overlay))
        {
            bossAura = overlay.SignatureAuraTag;
            bossUtility = overlay.SignatureUtilityTag;
            bossRewardTags = overlay.RewardDropTags ?? Array.Empty<string>();
        }

        var rewardTags = (encounter.RewardDropTags ?? Array.Empty<string>())
            .Concat(squadRewardTags)
            .Concat(bossRewardTags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .Take(8)
            .ToArray();
        return new HeadlessEnemyPreview(
            true,
            encounter.Id,
            encounter.FactionId,
            encounter.DifficultyBand,
            Math.Max(1, encounter.ThreatSkulls),
            units,
            bossAura,
            bossUtility,
            rewardTags);
    }

    private static SM.Combat.Model.DeploymentAnchorId ResolveClassAnchor(string classId)
        => classId is "vanguard" or "duelist"
            ? SM.Combat.Model.DeploymentAnchorId.FrontCenter
            : SM.Combat.Model.DeploymentAnchorId.BackCenter;

    private static HeadlessRewardKind MapRewardKind(RewardChoiceKind kind)
        => kind switch
        {
            RewardChoiceKind.Gold => HeadlessRewardKind.Gold,
            RewardChoiceKind.Item => HeadlessRewardKind.Item,
            RewardChoiceKind.TemporaryAugment => HeadlessRewardKind.TemporaryAugment,
            RewardChoiceKind.Echo => HeadlessRewardKind.Echo,
            RewardChoiceKind.PermanentAugmentSlot => HeadlessRewardKind.PermanentAugmentSlot,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown visible reward kind."),
        };
}
