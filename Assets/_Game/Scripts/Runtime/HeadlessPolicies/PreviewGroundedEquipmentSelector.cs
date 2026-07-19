using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.HeadlessPolicies;

internal sealed class PreviewGroundedEquipmentSelection
{
    public PreviewGroundedEquipmentSelection(
        IReadOnlyList<HeadlessEquipmentAssignment> assignments,
        double estimatedValue,
        string detail)
    {
        Assignments = assignments ?? Array.Empty<HeadlessEquipmentAssignment>();
        EstimatedValue = estimatedValue;
        Detail = detail ?? string.Empty;
    }

    public static PreviewGroundedEquipmentSelection None { get; } = new(
        Array.Empty<HeadlessEquipmentAssignment>(),
        0d,
        string.Empty);

    public IReadOnlyList<HeadlessEquipmentAssignment> Assignments { get; }
    public double EstimatedValue { get; }
    public string Detail { get; }
}

/// <summary>보이는 적 장비의 방어 채널과 보유 관통 장비를 연결해 최대 한 건만 재배치한다.</summary>
internal static class PreviewGroundedEquipmentSelector
{
    private const double MinimumImprovement = 0.000001d;

    public static PreviewGroundedEquipmentSelection Select(
        HeadlessPolicyObservation observation,
        IReadOnlyList<HeadlessPlacement> placements)
    {
        if (!observation.EnemyPreview.IsAvailable
            || observation.OwnedItems.Count == 0
            || placements.Count == 0)
        {
            return PreviewGroundedEquipmentSelection.None;
        }

        var profile = EnemyThreatProfileParser.Parse(
            EnemyThreatObservation.FromVisiblePreview(observation.EnemyPreview));
        if (profile.Tags.Contains(EnemyThreatTag.BacklineDive, StringComparer.Ordinal))
        {
            var bait = SelectBacklineDiveBait(observation, placements);
            if (bait != null)
            {
                return bait;
            }
        }

        var enemyItems = observation.EnemyPreview.Units
            .SelectMany(unit => unit.EquippedItems)
            .ToArray();
        var armor = enemyItems.Sum(item => ModifierMagnitude(item, "armor"));
        var resist = enemyItems.Sum(item => ModifierMagnitude(item, "resist"));
        if (armor <= 0d && resist <= 0d)
        {
            return PreviewGroundedEquipmentSelection.None;
        }

        var penetrationStat = armor >= resist ? "phys_pen" : "mag_pen";
        var wallStat = armor >= resist ? "armor" : "resist";
        var deployedIds = placements.Select(value => value.HeroId).ToHashSet(StringComparer.Ordinal);
        var rosterById = observation.Roster.ToDictionary(value => value.HeroId, StringComparer.Ordinal);
        Choice best = null;
        foreach (var owned in observation.OwnedItems.OrderBy(
                     value => value.Mechanics.ItemInstanceId,
                     StringComparer.Ordinal))
        {
            var penetration = ModifierMagnitude(owned.Mechanics, penetrationStat);
            if (penetration <= 0d)
            {
                continue;
            }

            var sourcePotential = deployedIds.Contains(owned.EquippedHeroId)
                                  && rosterById.TryGetValue(owned.EquippedHeroId, out var source)
                ? DamagePotential(source, penetrationStat)
                : 0d;
            foreach (var targetId in deployedIds.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!rosterById.TryGetValue(targetId, out var target)
                    || string.Equals(owned.EquippedHeroId, targetId, StringComparison.Ordinal)
                    || !CanUseWeaponFamily(target, owned.Mechanics.WeaponFamilyTag))
                {
                    continue;
                }

                var targetPotential = DamagePotential(target, penetrationStat);
                var improvement = penetration * (targetPotential - sourcePotential);
                if (improvement <= MinimumImprovement)
                {
                    continue;
                }

                var choice = new Choice(owned, target, penetration, improvement);
                if (best == null || choice.IsBetterThan(best))
                {
                    best = choice;
                }
            }
        }

        return best == null
            ? PreviewGroundedEquipmentSelection.None
            : new PreviewGroundedEquipmentSelection(
                new[]
                {
                    new HeadlessEquipmentAssignment(
                        best.Item.Mechanics.ItemInstanceId,
                        best.Target.HeroId),
                },
                best.Improvement,
                $"wall={wallStat};counter={penetrationStat};item={best.Item.Mechanics.ItemInstanceId};target={best.Target.HeroId}");
    }

    private static PreviewGroundedEquipmentSelection SelectBacklineDiveBait(
        HeadlessPolicyObservation observation,
        IReadOnlyList<HeadlessPlacement> placements)
    {
        var rosterById = observation.Roster.ToDictionary(value => value.HeroId, StringComparer.Ordinal);
        var bait = placements
            .Where(value => value.Anchor is DeploymentAnchorId.BackTop or DeploymentAnchorId.BackBottom)
            .Where(value => rosterById.TryGetValue(value.HeroId, out var hero)
                            && hero.ClassId is "ranger" or "mystic")
            .Select(value => rosterById[value.HeroId])
            .OrderBy(value => DefensiveMagnitude(value.EquippedItems) > 0d ? 1 : 0)
            .ThenBy(value => string.Equals(value.ClassId, "mystic", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(value => value.MaxHp)
            .ThenBy(value => value.HeroId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (bait == null || DefensiveMagnitude(bait.EquippedItems) > 0d)
        {
            return null;
        }

        var item = observation.OwnedItems
            .Where(value => !string.Equals(value.EquippedHeroId, bait.HeroId, StringComparison.Ordinal))
            .Where(value => CanUseWeaponFamily(bait, value.Mechanics.WeaponFamilyTag))
            .Where(value => CanUseClassTag(bait, value.Mechanics.Tags))
            .Select(value => (Item: value, Defense: DefensiveMagnitude(new[] { value.Mechanics })))
            .Where(value => value.Defense > 0d)
            .OrderByDescending(value => value.Defense)
            .ThenBy(value => value.Item.Mechanics.ItemInstanceId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (item.Item == null)
        {
            return null;
        }

        return new PreviewGroundedEquipmentSelection(
            new[] { new HeadlessEquipmentAssignment(item.Item.Mechanics.ItemInstanceId, bait.HeroId) },
            item.Defense,
            $"threat=backline_dive;counter=durable_bait;item={item.Item.Mechanics.ItemInstanceId};target={bait.HeroId}");
    }

    private static double DefensiveMagnitude(IEnumerable<HeadlessItemMechanicsObservation> items)
        => items.Sum(item =>
            ModifierMagnitude(item, "max_health")
            + (ModifierMagnitude(item, "armor") * 4d)
            + (ModifierMagnitude(item, "resist") * 4d)
            + (ModifierMagnitude(item, "barrier_power") * 2d));

    private static double ModifierMagnitude(HeadlessItemMechanicsObservation item, string statId)
        => item.StatModifiers
               .Concat(item.Affixes.SelectMany(value => value.StatModifiers))
               .Where(value => string.Equals(value.StatId, statId, StringComparison.OrdinalIgnoreCase))
               .Sum(value => Math.Max(0f, value.Value));

    private static double DamagePotential(HeadlessHeroObservation hero, string penetrationStat)
    {
        var magical = string.Equals(penetrationStat, "mag_pen", StringComparison.Ordinal);
        var coefficient = hero.SkillCards.Sum(skill => magical
            ? Math.Max(0f, skill.MagicalCoefficient)
            : Math.Max(0f, skill.PhysicalCoefficient));
        if (coefficient > 0d)
        {
            return coefficient;
        }

        return magical
            ? string.Equals(hero.ClassId, "mystic", StringComparison.Ordinal) ? 1d : 0d
            : hero.ClassId is "duelist" or "ranger" ? 1d : 0d;
    }

    private static bool CanUseWeaponFamily(HeadlessHeroObservation hero, string weaponFamily)
        => string.IsNullOrWhiteSpace(weaponFamily)
           || weaponFamily switch
           {
               "blade" => string.Equals(hero.ClassId, "duelist", StringComparison.Ordinal),
               "bow" => string.Equals(hero.ClassId, "ranger", StringComparison.Ordinal),
               "focus" => string.Equals(hero.ClassId, "mystic", StringComparison.Ordinal),
               "shield" => string.Equals(hero.ClassId, "vanguard", StringComparison.Ordinal),
               _ => hero.EquippedItems.Any(item => string.Equals(
                   item.WeaponFamilyTag,
                   weaponFamily,
                   StringComparison.Ordinal)),
           };

    private static bool CanUseClassTag(
        HeadlessHeroObservation hero,
        IEnumerable<string> itemTags)
    {
        var classTags = (itemTags ?? Array.Empty<string>())
            .Where(IsCombatClassTag)
            .ToArray();
        return classTags.Length == 0
               || classTags.Contains(hero.ClassId, StringComparer.Ordinal);
    }

    private static bool IsCombatClassTag(string tag)
        => tag is "vanguard" or "duelist" or "ranger" or "mystic";

    private sealed class Choice
    {
        public Choice(
            HeadlessOwnedItemObservation item,
            HeadlessHeroObservation target,
            double penetration,
            double improvement)
        {
            Item = item;
            Target = target;
            Penetration = penetration;
            Improvement = improvement;
        }

        public HeadlessOwnedItemObservation Item { get; }
        public HeadlessHeroObservation Target { get; }
        public double Penetration { get; }
        public double Improvement { get; }

        public bool IsBetterThan(Choice other)
            => Improvement > other.Improvement + MinimumImprovement
               || Math.Abs(Improvement - other.Improvement) <= MinimumImprovement
               && (Penetration > other.Penetration + MinimumImprovement
                   || Math.Abs(Penetration - other.Penetration) <= MinimumImprovement
                   && (string.CompareOrdinal(Item.Mechanics.ItemInstanceId, other.Item.Mechanics.ItemInstanceId) < 0
                       || string.Equals(
                              Item.Mechanics.ItemInstanceId,
                              other.Item.Mechanics.ItemInstanceId,
                              StringComparison.Ordinal)
                          && string.CompareOrdinal(Target.HeroId, other.Target.HeroId) < 0));
    }
}
