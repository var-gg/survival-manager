using System.Linq;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Unity;

internal static class AutomaticLootTextTokenBuilder
{
    internal static SessionTextToken Build(LootBundleResult bundle)
    {
        SessionTextToken listToken;
        if (bundle.Entries.Count == 0)
        {
            listToken = new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.loot.none",
                "No automatic loot");
        }
        else
        {
            var entryTokens = bundle.Entries
                .Select(BuildEntry)
                .ToArray();
            var placeholders = string.Join(
                ", ",
                Enumerable.Range(0, entryTokens.Length).Select(index => $"{{{index}}}"));
            listToken = new SessionTextToken(
                string.Empty,
                string.Empty,
                placeholders,
                entryTokens.Select(SessionTextArg.Token).ToArray());
        }

        return new SessionTextToken(
            GameLocalizationTables.UIReward,
            "ui.reward.summary.auto_loot",
            "Auto Loot: {0}",
            SessionTextArg.Token(listToken));
    }

    private static SessionTextToken BuildEntry(LootEntry entry)
    {
        var nameArg = entry.RewardType switch
        {
            RewardType.Item => SessionTextArg.ItemName(entry.Id),
            RewardType.TemporaryAugment => SessionTextArg.AugmentName(entry.Id),
            RewardType.Gold => LocalizedName("ui.reward.loot.gold", "Gold"),
            RewardType.Echo => LocalizedName("ui.reward.loot.echo", "Echo"),
            RewardType.EmberDust => LocalizedName("ui.reward.loot.ember_dust", "Ember Dust"),
            RewardType.EchoCrystal => LocalizedName("ui.reward.loot.echo_crystal", "Echo Crystal"),
            RewardType.BossSigil => LocalizedName("ui.reward.loot.boss_sigil", "Boss Sigil"),
            RewardType.TraitLockToken => LocalizedName("ui.reward.loot.trait_lock", "Trait Lock Token"),
            RewardType.TraitPurgeToken => LocalizedName("ui.reward.loot.trait_purge", "Trait Purge Token"),
            RewardType.SkillManual => LocalizedName("ui.reward.loot.skill_manual", "Skill Manual"),
            RewardType.SkillShard => LocalizedName("ui.reward.loot.skill_shard", "Skill Shard"),
            _ => LocalizedName("ui.reward.loot.unknown", "Unknown reward"),
        };

        return new SessionTextToken(
            GameLocalizationTables.UIReward,
            "ui.reward.loot.entry",
            "{0} x{1}",
            nameArg,
            SessionTextArg.Number(entry.Amount));
    }

    private static SessionTextArg LocalizedName(string key, string fallback)
        => SessionTextArg.Localized(
            GameLocalizationTables.UIReward,
            key,
            fallback);
}
