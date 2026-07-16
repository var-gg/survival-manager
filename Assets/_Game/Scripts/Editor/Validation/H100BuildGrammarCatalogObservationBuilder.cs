using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.HeadlessPolicies;
using SM.Meta.Model;

namespace SM.Editor.Validation;

/// <summary>실콘텐츠 catalog를 E01 projector가 소비하는 결정적 관측 입력으로 구성한다.</summary>
internal static class H100BuildGrammarCatalogObservationBuilder
{
    public static HeadlessPolicyObservation Build(CombatContentSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var itemMechanics = ItemIds(snapshot)
            .Select((id, index) => H100PolicyObservationBuilder.BuildItemMechanics(
                id,
                $"surface-item-{index:D3}",
                Array.Empty<string>(),
                snapshot))
            .ToArray();
        var passiveIds = snapshot.PassiveNodes.Keys
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var archetypes = snapshot.Archetypes.Values
            .Where(archetype => archetype != null && !string.IsNullOrWhiteSpace(archetype.Id))
            .OrderBy(archetype => archetype.Id, StringComparer.Ordinal)
            .ToArray();
        var heroes = archetypes.Select((archetype, index) => new HeadlessHeroObservation(
                $"surface-hero-{index:D3}",
                archetype.Id,
                archetype.RaceId,
                archetype.ClassId,
                archetype.RoleTag,
                1,
                1,
                1,
                index == 0 ? itemMechanics.Length : 0,
                false,
                archetype.DefaultAnchor,
                H100PolicyObservationBuilder.BuildSkillCards(CollectArchetypeSkills(archetype).ToArray()),
                archetype.FlexActive?.Id ?? string.Empty,
                archetype.FlexPassive?.Id ?? string.Empty,
                index == 0 ? itemMechanics : Array.Empty<HeadlessItemMechanicsObservation>(),
                index == 0 ? passiveIds : Array.Empty<string>()))
            .ToArray();
        if (heroes.Length == 0)
        {
            throw new InvalidOperationException("Surface audit requires at least one content archetype.");
        }

        var augments = AugmentIds(snapshot)
            .Select(id => H100PolicyObservationBuilder.BuildAugmentMechanics(id, snapshot))
            .ToArray();
        var rewards = new List<HeadlessRewardOption>();
        foreach (var item in itemMechanics.OrderBy(item => item.ItemId, StringComparer.Ordinal))
        {
            rewards.Add(new HeadlessRewardOption(
                rewards.Count,
                HeadlessRewardKind.Item,
                item.ItemId,
                0,
                0,
                0,
                new HeadlessRewardMechanicsObservation(item, null)));
        }

        foreach (var augment in augments.OrderBy(augment => augment.AugmentId, StringComparer.Ordinal))
        {
            rewards.Add(new HeadlessRewardOption(
                rewards.Count,
                HeadlessRewardKind.TemporaryAugment,
                augment.AugmentId,
                0,
                0,
                0,
                new HeadlessRewardMechanicsObservation(null, augment)));
        }

        return new HeadlessPolicyObservation(
            0,
            4,
            "surface-catalog",
            "surface-catalog",
            heroes,
            Enum.GetValues(typeof(DeploymentAnchorId)).Cast<DeploymentAnchorId>().OrderBy(anchor => anchor).ToArray(),
            HeadlessEnemyPreview.Unavailable,
            rewards,
            HeadlessWalletObservation.Empty,
            augments,
            Array.Empty<HeadlessSynergyCountObservation>(),
            H100PolicyObservationBuilder.BuildSynergyCatalog(snapshot));
    }

    private static IEnumerable<string> ItemIds(CombatContentSnapshot snapshot)
        => snapshot.ItemPackages.Keys
            .Concat(snapshot.ItemCatalog?.Keys ?? Array.Empty<string>())
            .Concat(snapshot.ItemGrantedSkills?.Keys ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal);

    private static IEnumerable<string> AugmentIds(CombatContentSnapshot snapshot)
        => snapshot.AugmentPackages.Keys
            .Concat(snapshot.AugmentCatalog.Keys)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal);

    private static IEnumerable<BattleSkillSpec> CollectArchetypeSkills(CombatArchetypeTemplate archetype)
    {
        var skills = (archetype.Skills ?? Array.Empty<BattleSkillSpec>())
            .Concat(archetype.RecruitFlexActivePool ?? Array.Empty<BattleSkillSpec>())
            .Concat(archetype.RecruitFlexPassivePool ?? Array.Empty<BattleSkillSpec>());
        if (archetype.SignatureActive != null)
        {
            skills = skills.Append(archetype.SignatureActive);
        }

        if (archetype.FlexActive != null)
        {
            skills = skills.Append(archetype.FlexActive);
        }

        return skills.Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.Id))
            .GroupBy(skill => skill.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(skill => skill.Id, StringComparer.Ordinal);
    }
}
