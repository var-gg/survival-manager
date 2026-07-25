using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SM.Editor.Validation;
using SM.Meta.Services;

internal sealed record RefitFarmBattleIdentity(
    int BattleSeed,
    IReadOnlyList<string> OrderedEntityIds);

internal static class RefitFarmPairingVerifier
{
    internal static string HashInitialState(HeadlessCampaignState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var builder = new StringBuilder();
        Append(builder, "campaign-seed", state.CampaignSeed.ToString(CultureInfo.InvariantCulture));
        Append(builder, "campaign-seed-salt", state.CampaignSeedSalt.ToString(CultureInfo.InvariantCulture));
        Append(builder, "heat", state.Heat.ToString(CultureInfo.InvariantCulture));
        Append(builder, "gold", state.Gold.ToString(CultureInfo.InvariantCulture));
        Append(builder, "echo", state.Echo.ToString(CultureInfo.InvariantCulture));
        Append(builder, "chapter", state.SelectedChapterId);
        Append(builder, "site", state.SelectedSiteId);
        Append(builder, "node", state.CurrentNodeIndex.ToString(CultureInfo.InvariantCulture));
        foreach (var hero in state.Heroes.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            Append(builder, "hero", hero.Id);
            Append(builder, "hero-archetype", hero.ArchetypeId);
            Append(builder, "hero-class", hero.ClassId);
            foreach (var itemId in hero.EquippedItemIds.OrderBy(value => value, StringComparer.Ordinal))
            {
                Append(builder, "hero-item", itemId);
            }
        }

        foreach (var item in state.Inventory
                     .OrderBy(value => value.AcquisitionIndex)
                     .ThenBy(value => value.InstanceId, StringComparer.Ordinal))
        {
            Append(builder, "item", item.InstanceId);
            Append(builder, "item-base", item.ItemBaseId);
            Append(builder, "item-grade", ((int)item.RarityTier).ToString(CultureInfo.InvariantCulture));
            Append(builder, "item-acquisition", item.AcquisitionIndex.ToString(CultureInfo.InvariantCulture));
            Append(builder, "item-refit", item.RefitLevel.ToString(CultureInfo.InvariantCulture));
            Append(builder, "item-equipped", item.EquippedHeroId);
            foreach (var affixId in item.AffixIds)
            {
                Append(builder, "item-affix", affixId);
                Append(
                    builder,
                    "item-affix-magnitude-bits",
                    item.AffixMagnitudes.TryGetValue(affixId, out var magnitude)
                        ? BitConverter.SingleToInt32Bits(magnitude)
                            .ToString(CultureInfo.InvariantCulture)
                        : "missing");
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }

    internal static bool NaturalDropsEqual(
        IReadOnlyList<HeadlessCampaignNaturalDrop>? left,
        IReadOnlyList<HeadlessCampaignNaturalDrop>? right)
    {
        var leftDrops = left ?? Array.Empty<HeadlessCampaignNaturalDrop>();
        var rightDrops = right ?? Array.Empty<HeadlessCampaignNaturalDrop>();
        if (leftDrops.Count != rightDrops.Count)
        {
            return false;
        }

        for (var index = 0; index < leftDrops.Count; index++)
        {
            var a = leftDrops[index];
            var b = rightDrops[index];
            if (!string.Equals(a.InstanceId, b.InstanceId, StringComparison.Ordinal)
                || !string.Equals(a.ItemBaseId, b.ItemBaseId, StringComparison.Ordinal)
                || a.AcquisitionIndex != b.AcquisitionIndex
                || a.RarityTier != b.RarityTier
                || !a.AffixIds.SequenceEqual(b.AffixIds, StringComparer.Ordinal)
                || !MagnitudeBytesEqual(a, b))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MagnitudeBytesEqual(
        HeadlessCampaignNaturalDrop left,
        HeadlessCampaignNaturalDrop right)
    {
        if (left.AffixMagnitudes.Count != right.AffixMagnitudes.Count)
        {
            return false;
        }

        foreach (var affixId in left.AffixIds)
        {
            if (!left.AffixMagnitudes.TryGetValue(affixId, out var leftMagnitude)
                || !right.AffixMagnitudes.TryGetValue(affixId, out var rightMagnitude)
                || BitConverter.SingleToInt32Bits(leftMagnitude)
                != BitConverter.SingleToInt32Bits(rightMagnitude))
            {
                return false;
            }
        }

        return true;
    }

    internal static RefitFarmBattleIdentity ComposeBattleIdentity(
        HeadlessCampaignState state,
        CampaignBalanceGridCell cell)
    {
        var setup = state.BuildBattleSetup();
        var encounter = HeadlessCampaignPlaythrough.ProjectEncounter(
            setup.AuthoredEncounter,
            cell.EnemyComposition);
        if (!string.Equals(
                encounter.Context.EncounterId,
                EndlessHeatSweepRunner.TargetEncounterId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refit pairing expected encounter '{EndlessHeatSweepRunner.TargetEncounterId}', "
                + $"found '{encounter.Context.EncounterId}'.");
        }

        if (!SessionBattleStateComposer.TryCompose(
                state.Lookup,
                setup.AllySnapshot,
                encounter,
                out var battleState,
                out var error))
        {
            throw new InvalidOperationException(
                $"Refit pairing battle identity compose failed ({cell.CellId}): {error}");
        }

        return new RefitFarmBattleIdentity(
            encounter.Context.BattleSeed,
            battleState.Allies
                .Select(unit => unit.Id.Value)
                .Concat(battleState.Enemies.Select(unit => unit.Id.Value))
                .ToArray());
    }

    private static void Append(StringBuilder builder, string key, string value)
    {
        builder.Append(key.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(key)
            .Append('=')
            .Append((value ?? string.Empty).Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value ?? string.Empty)
            .Append('\n');
    }
}
