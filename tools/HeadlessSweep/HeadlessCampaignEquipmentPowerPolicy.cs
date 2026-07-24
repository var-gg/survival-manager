using SM.Core.Content;
using SM.Meta.Model;
using SM.Meta.Services;

internal static class HeadlessCampaignEquipmentPowerPolicy
{
    internal const int ExpectedHeroCount = 4;
    internal const int ExpectedSlotsPerHero = 3;

    internal static HeadlessEquippedLoadoutObservation Apply(HeadlessCampaignState state)
    {
        var itemCatalog = state.Snapshot.ItemCatalog
                          ?? throw new InvalidDataException("Headless equipment policy requires an item catalog.");
        var affixCatalog = state.Snapshot.AffixCatalog
                           ?? throw new InvalidDataException("Headless equipment policy requires an affix catalog.");
        var heroes = state.ExpeditionSquadHeroIds
            .Select(heroId => state.Heroes.Single(hero =>
                string.Equals(hero.Id, heroId, StringComparison.Ordinal)))
            .ToArray();
        if (heroes.Length != ExpectedHeroCount)
        {
            throw new InvalidDataException(
                $"Endless Heat measurement requires {ExpectedHeroCount} deployed heroes, found {heroes.Length}.");
        }

        var slotTypes = itemCatalog.Values
            .Select(item => item.SlotType)
            .Where(slot => !string.IsNullOrWhiteSpace(slot))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(slot => slot, StringComparer.Ordinal)
            .ToArray();
        if (slotTypes.Length != ExpectedSlotsPerHero)
        {
            throw new InvalidDataException(
                $"Endless Heat measurement requires {ExpectedSlotsPerHero} item slots, found [{string.Join(",", slotTypes)}].");
        }

        foreach (var hero in state.Heroes)
        {
            hero.EquippedItemIds.Clear();
        }

        foreach (var item in state.Inventory)
        {
            item.EquippedHeroId = string.Empty;
        }

        var equipped = new List<HeadlessEquippedSlotObservation>(
            ExpectedHeroCount * ExpectedSlotsPerHero);
        foreach (var slotType in slotTypes)
        {
            var assignment = ResolveSlotAssignment(
                state.Inventory,
                heroes,
                slotType,
                itemCatalog,
                affixCatalog);
            for (var heroIndex = 0; heroIndex < heroes.Length; heroIndex++)
            {
                var item = assignment[heroIndex];
                var hero = heroes[heroIndex];
                item.EquippedHeroId = hero.Id;
                hero.EquippedItemIds.Add(item.InstanceId);
                equipped.Add(new HeadlessEquippedSlotObservation(
                    hero.Id,
                    slotType,
                    item.InstanceId,
                    item.ItemBaseId,
                    (int)item.RarityTier,
                    ResolvePowerScoreQ(item, affixCatalog)));
            }
        }

        var ordered = equipped
            .OrderBy(value => value.HeroId, StringComparer.Ordinal)
            .ThenBy(value => value.SlotType, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length != ExpectedHeroCount * ExpectedSlotsPerHero)
        {
            throw new InvalidDataException(
                $"Endless Heat equipment policy filled {ordered.Length} slots instead of 12.");
        }

        var histogram = Enumerable.Range(0, 5)
            .Select(grade => ordered.Count(slot => slot.Grade == grade))
            .ToArray();
        return new HeadlessEquippedLoadoutObservation(
            ordered,
            ordered.Average(slot => slot.Grade),
            ordered.Count(slot => slot.Grade >= (int)ItemRarityTierValue.Epic) / (double)ordered.Length,
            ordered.Count(slot => slot.Grade == (int)ItemRarityTierValue.Legendary) / (double)ordered.Length,
            histogram);
    }

    private static IReadOnlyList<HeadlessCampaignItem> ResolveSlotAssignment(
        IReadOnlyList<HeadlessCampaignItem> inventory,
        IReadOnlyList<HeadlessCampaignHero> heroes,
        string slotType,
        IReadOnlyDictionary<string, ItemTemplate> itemCatalog,
        IReadOnlyDictionary<string, AffixTemplate> affixCatalog)
    {
        var candidates = inventory
            .Where(item => itemCatalog.TryGetValue(item.ItemBaseId, out var template)
                           && string.Equals(template.SlotType, slotType, StringComparison.Ordinal))
            .OrderBy(item => item.InstanceId, StringComparer.Ordinal)
            .ToArray();
        var stateCount = 1 << heroes.Count;
        var states = new SlotAssignmentState?[stateCount];
        states[0] = new SlotAssignmentState(
            TotalPowerScoreQ: 0,
            TotalGrade: 0,
            ItemsByHero: new HeadlessCampaignItem?[heroes.Count]);

        foreach (var candidate in candidates)
        {
            var template = itemCatalog[candidate.ItemBaseId];
            var powerScoreQ = ResolvePowerScoreQ(candidate, affixCatalog);
            var next = (SlotAssignmentState?[])states.Clone();
            for (var mask = 0; mask < states.Length; mask++)
            {
                var current = states[mask];
                if (current == null)
                {
                    continue;
                }

                for (var heroIndex = 0; heroIndex < heroes.Count; heroIndex++)
                {
                    var bit = 1 << heroIndex;
                    if ((mask & bit) != 0 || !CanWear(heroes[heroIndex], template))
                    {
                        continue;
                    }

                    var items = (HeadlessCampaignItem?[])current.ItemsByHero.Clone();
                    items[heroIndex] = candidate;
                    var proposal = new SlotAssignmentState(
                        current.TotalPowerScoreQ + powerScoreQ,
                        current.TotalGrade + (int)candidate.RarityTier,
                        items);
                    var nextMask = mask | bit;
                    if (IsBetter(proposal, next[nextMask]))
                    {
                        next[nextMask] = proposal;
                    }
                }
            }

            states = next;
        }

        var complete = states[^1]
                       ?? throw new InvalidDataException(
                           $"No complete best-power assignment exists for slot '{slotType}'.");
        return complete.ItemsByHero
            .Select(item => item
                            ?? throw new InvalidDataException(
                                $"Best-power assignment left an empty '{slotType}' slot."))
            .ToArray();
    }

    private static bool CanWear(HeadlessCampaignHero hero, ItemTemplate item)
        => item.AllowedClassIds is not { Count: > 0 }
           || item.AllowedClassIds.Contains(hero.ClassId, StringComparer.Ordinal);

    private static int ResolvePowerScoreQ(
        HeadlessCampaignItem item,
        IReadOnlyDictionary<string, AffixTemplate> affixCatalog)
    {
        var score = 0;
        foreach (var affixId in item.AffixIds)
        {
            if (!affixCatalog.TryGetValue(affixId, out var affix))
            {
                throw new InvalidDataException(
                    $"Generated item '{item.InstanceId}' references missing affix '{affixId}'.");
            }

            score = checked(score + AffixQualityProfileCompiler.ToBudgetScoreQ(affix.BudgetScore));
        }

        return score;
    }

    private static bool IsBetter(SlotAssignmentState proposal, SlotAssignmentState? current)
    {
        if (current == null)
        {
            return true;
        }

        if (proposal.TotalPowerScoreQ != current.TotalPowerScoreQ)
        {
            return proposal.TotalPowerScoreQ > current.TotalPowerScoreQ;
        }

        return proposal.TotalGrade > current.TotalGrade;
    }

    private sealed record SlotAssignmentState(
        int TotalPowerScoreQ,
        int TotalGrade,
        HeadlessCampaignItem?[] ItemsByHero);
}
