using System;
using SM.Content;

namespace SM.Content.Definitions
{

    public static class ContentLocalizationTables
    {
        public const string Items = "Content_Items";
        public const string Affixes = "Content_Affixes";
        public const string Skills = "Content_Skills";
        public const string Augments = "Content_Augments";
        public const string Synergies = "Content_Synergies";
        public const string Races = "Content_Races";
        public const string Classes = "Content_Classes";
        public const string Characters = "Content_Characters";
        public const string Traits = "Content_Traits";
        public const string Archetypes = "Content_Archetypes";
        public const string Passives = "Content_Passives";
        public const string TeamTactics = "Content_TeamTactics";
        public const string Roles = "Content_Roles";
        public const string Tags = "Content_Tags";
        public const string Stats = "Content_Stats";
        public const string Rewards = "Content_Rewards";
        public const string Expedition = "Content_Expeditions";
        public const string Campaign = "Content_Campaign";
        public const string Encounters = "Content_Encounters";
        public const string Status = "Content_Status";
        public const string Story = "Content_Story";
        public const string SystemMessages = "System_Messages";

        public static string BuildItemNameKey(string id) => $"content.item.{NormalizeId(id)}.name";
        public static string BuildItemDescriptionKey(string id) => $"content.item.{NormalizeId(id)}.desc";
        public static string BuildAffixNameKey(string id) => $"content.affix.{NormalizeId(id)}.name";
        public static string BuildAffixDescriptionKey(string id) => $"content.affix.{NormalizeId(id)}.desc";
        public static string BuildSkillNameKey(string id) => $"content.skill.{NormalizeId(id)}.name";
        public static string BuildSkillDescriptionKey(string id) => $"content.skill.{NormalizeId(id)}.desc";
        public static string BuildAugmentNameKey(string id) => $"content.augment.{NormalizeId(id)}.name";
        public static string BuildAugmentDescriptionKey(string id) => $"content.augment.{NormalizeId(id)}.desc";
        public static string BuildSynergyNameKey(string id) => $"content.synergy.{NormalizeId(id)}.name";
        public static string BuildSynergyDescriptionKey(string id) => $"content.synergy.{NormalizeId(id)}.desc";
        public static string BuildRaceNameKey(string id) => $"content.race.{NormalizeId(id)}.name";
        public static string BuildRaceDescriptionKey(string id) => $"content.race.{NormalizeId(id)}.desc";
        public static string BuildClassNameKey(string id) => $"content.class.{NormalizeId(id)}.name";
        public static string BuildClassDescriptionKey(string id) => $"content.class.{NormalizeId(id)}.desc";
        public static string BuildCharacterNameKey(string id) => $"content.character.{NormalizeId(id)}.name";
        public static string BuildCharacterDescriptionKey(string id) => $"content.character.{NormalizeId(id)}.desc";
        public static string BuildTraitNameKey(string archetypeId, string id) => $"content.trait.{NormalizeId(archetypeId)}.{NormalizeId(id)}.name";
        public static string BuildTraitDescriptionKey(string archetypeId, string id) => $"content.trait.{NormalizeId(archetypeId)}.{NormalizeId(id)}.desc";
        public static string BuildArchetypeNameKey(string id) => $"content.archetype.{NormalizeId(id)}.name";
        public static string BuildPassiveBoardNameKey(string id) => $"content.passive_board.{NormalizeId(id)}.name";
        public static string BuildPassiveBoardDescriptionKey(string id) => $"content.passive_board.{NormalizeId(id)}.desc";
        public static string BuildPassiveNodeNameKey(string id) => $"content.passive_node.{NormalizeId(id)}.name";
        public static string BuildPassiveNodeDescriptionKey(string id) => $"content.passive_node.{NormalizeId(id)}.desc";
        public static string BuildTeamTacticNameKey(string id) => $"content.team_tactic.{NormalizeId(id)}.name";
        public static string BuildRoleNameKey(string id) => $"content.role.{NormalizeId(id)}.name";
        public static string BuildStatNameKey(string id) => $"content.stat.{NormalizeId(id)}.name";
        public static string BuildStatDescriptionKey(string id) => $"content.stat.{NormalizeId(id)}.desc";
        public static string BuildRewardLabelKey(string id) => $"content.reward.{NormalizeId(id)}.label";
        public static string BuildRewardTableNameKey(string id) => $"content.reward_table.{NormalizeId(id)}.name";
        public static string BuildExpeditionNameKey(string id) => $"content.expedition.{NormalizeId(id)}.name";
        public static string BuildExpeditionDescriptionKey(string id) => $"content.expedition.{NormalizeId(id)}.desc";
        public static string BuildExpeditionNodeLabelKey(string expeditionId, string nodeId) => $"ui.expedition.node.{NormalizeId(expeditionId)}.{NormalizeId(nodeId)}.label";
        public static string BuildExpeditionNodeDescriptionKey(string expeditionId, string nodeId) => $"ui.expedition.node.{NormalizeId(expeditionId)}.{NormalizeId(nodeId)}.desc";
        public static string BuildExpeditionNodeRewardKey(string expeditionId, string nodeId) => $"ui.expedition.node.{NormalizeId(expeditionId)}.{NormalizeId(nodeId)}.reward";
        public static string BuildCampaignChapterNameKey(string id) => $"content.campaign.chapter.{NormalizeId(id)}.name";
        public static string BuildCampaignChapterDescriptionKey(string id) => $"content.campaign.chapter.{NormalizeId(id)}.desc";
        public static string BuildExpeditionSiteNameKey(string id) => $"content.campaign.site.{NormalizeId(id)}.name";
        public static string BuildExpeditionSiteDescriptionKey(string id) => $"content.campaign.site.{NormalizeId(id)}.desc";
        public static string BuildEncounterNameKey(string id) => $"content.encounter.{NormalizeId(id)}.name";
        public static string BuildEncounterDescriptionKey(string id) => $"content.encounter.{NormalizeId(id)}.desc";
        public static string BuildEnemySquadNameKey(string id) => $"content.enemy_squad.{NormalizeId(id)}.name";
        public static string BuildEnemySquadDescriptionKey(string id) => $"content.enemy_squad.{NormalizeId(id)}.desc";
        public static string BuildBossOverlayNameKey(string id) => $"content.boss_overlay.{NormalizeId(id)}.name";
        public static string BuildBossOverlayDescriptionKey(string id) => $"content.boss_overlay.{NormalizeId(id)}.desc";
        public static string BuildStatusNameKey(string id) => $"content.status.{NormalizeId(id)}.name";
        public static string BuildStatusDescriptionKey(string id) => $"content.status.{NormalizeId(id)}.desc";
        public static string BuildCleanseProfileNameKey(string id) => $"content.cleanse_profile.{NormalizeId(id)}.name";
        public static string BuildCleanseProfileDescriptionKey(string id) => $"content.cleanse_profile.{NormalizeId(id)}.desc";
        public static string BuildControlDiminishingNameKey(string id) => $"content.control_diminishing.{NormalizeId(id)}.name";
        public static string BuildControlDiminishingDescriptionKey(string id) => $"content.control_diminishing.{NormalizeId(id)}.desc";
        public static string BuildRewardSourceNameKey(string id) => $"content.reward_source.{NormalizeId(id)}.name";
        public static string BuildRewardSourceDescriptionKey(string id) => $"content.reward_source.{NormalizeId(id)}.desc";
        public static string BuildDropTableNameKey(string id) => $"content.drop_table.{NormalizeId(id)}.name";
        public static string BuildDropTableDescriptionKey(string id) => $"content.drop_table.{NormalizeId(id)}.desc";
        public static string BuildLootBundleNameKey(string id) => $"content.loot_bundle.{NormalizeId(id)}.name";
        public static string BuildLootBundleDescriptionKey(string id) => $"content.loot_bundle.{NormalizeId(id)}.desc";
        public static string BuildTraitTokenNameKey(string id) => $"content.trait_token.{NormalizeId(id)}.name";
        public static string BuildTraitTokenDescriptionKey(string id) => $"content.trait_token.{NormalizeId(id)}.desc";

        public static string NormalizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "unknown";
            }

            return id.Trim().Replace(' ', '_').Replace('-', '_').ToLowerInvariant();
        }

        public static string GetTableName(Type definitionType)
        {
            if (definitionType == typeof(ItemBaseDefinition))
            {
                return Items;
            }

            if (definitionType == typeof(SkillDefinitionAsset))
            {
                return Skills;
            }

            if (definitionType == typeof(AffixDefinition))
            {
                return Affixes;
            }

            if (definitionType == typeof(AugmentDefinition))
            {
                return Augments;
            }

            if (definitionType == typeof(RaceDefinition))
            {
                return Races;
            }

            if (definitionType == typeof(ClassDefinition))
            {
                return Classes;
            }

            if (definitionType == typeof(CharacterDefinition))
            {
                return Characters;
            }

            if (definitionType == typeof(TraitEntry))
            {
                return Traits;
            }

            if (definitionType == typeof(UnitArchetypeDefinition))
            {
                return Archetypes;
            }

            if (definitionType == typeof(SynergyDefinition) || definitionType == typeof(SynergyTierDefinition))
            {
                return Synergies;
            }

            if (definitionType == typeof(PassiveBoardDefinition) || definitionType == typeof(PassiveNodeDefinition))
            {
                return Passives;
            }

            if (definitionType == typeof(TeamTacticDefinition))
            {
                return TeamTactics;
            }

            if (definitionType == typeof(RoleInstructionDefinition))
            {
                return Roles;
            }

            if (definitionType == typeof(StableTagDefinition))
            {
                return Tags;
            }

            if (definitionType == typeof(StatDefinition))
            {
                return Stats;
            }

            if (definitionType == typeof(RewardTableDefinition) || definitionType == typeof(RewardEntry))
            {
                return Rewards;
            }

            if (definitionType == typeof(RewardSourceDefinition)
                || definitionType == typeof(DropTableDefinition)
                || definitionType == typeof(LootBundleDefinition)
                || definitionType == typeof(TraitTokenDefinition))
            {
                return Rewards;
            }

            if (definitionType == typeof(ExpeditionDefinition) || definitionType == typeof(ExpeditionNodeDefinition))
            {
                return Expedition;
            }

            if (definitionType == typeof(CampaignChapterDefinition) || definitionType == typeof(ExpeditionSiteDefinition))
            {
                return Campaign;
            }

            if (definitionType == typeof(EncounterDefinition)
                || definitionType == typeof(EnemySquadTemplateDefinition)
                || definitionType == typeof(BossOverlayDefinition)
                || definitionType == typeof(EnemySquadMemberDefinition))
            {
                return Encounters;
            }

            if (definitionType == typeof(StatusFamilyDefinition)
                || definitionType == typeof(CleanseProfileDefinition)
                || definitionType == typeof(ControlDiminishingRuleDefinition))
            {
                return Status;
            }

            if (definitionType == typeof(StoryEventDefinition)
                || definitionType == typeof(DialogueSequenceDefinition)
                || definitionType == typeof(ChapterBeatDefinition)
                || definitionType == typeof(HeroLoreDefinition))
            {
                return Story;
            }

            return SystemMessages;
        }
    }
}
