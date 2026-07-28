namespace SM.Meta.Model;

public static class MetaOperationFailureCodes
{
    public const string RefitItemStateIncomplete = "refit.item_state_incomplete";
    public const string RefitInvalidGrade = "refit.invalid_grade";
    public const string RefitAffixCatalogUnavailable = "refit.affix_catalog_unavailable";
    public const string RefitItemBaseUnknown = "refit.item_base_unknown";
    public const string RefitAffixSetInvalid = "refit.affix_set_invalid";
    public const string RefitAffixIllegalForSlot = "refit.affix_illegal_for_slot";
    public const string RefitAffixExclusiveConflict = "refit.affix_exclusive_conflict";
    public const string RefitTierSequenceInvalid = "refit.tier_sequence_invalid";
    public const string RefitOperationNotAllowed = "refit.operation_not_allowed";
    public const string RefitLevelInvalid = "refit.level_invalid";
    public const string RefitChapterEconomyUnavailable = "refit.chapter_economy_unavailable";
    public const string RefitCostResolutionFailed = "refit.cost_resolution_failed";
    public const string RefitQualityResolutionFailed = "refit.quality_resolution_failed";
    public const string RefitQualityMaxed = "refit.quality_maxed";
    public const string RefitGenerationFailed = "refit.generation_failed";
    public const string RefitPostconditionFailed = "refit.postcondition_failed";
    public const string RefitSealAttemptInvalid = "refit.seal_attempt_invalid";
    public const string RefitSealSelectionRequired = "refit.seal_selection_required";
    public const string RefitSealSelectionInvalid = "refit.seal_selection_invalid";
    public const string RefitSealAllAffixesLocked = "refit.seal_all_affixes_locked";
    public const string RefitSealMagnitudeMissing = "refit.seal_magnitude_missing";
    public const string RefitSealMagnitudeChanged = "refit.seal_magnitude_changed";

    public const string PassiveNodeMissing = "passive.node_missing";
    public const string PassiveNodeWrongBoard = "passive.node_wrong_board";
    public const string PassivePrerequisiteRequired = "passive.prerequisite_required";
    public const string PassiveActiveNodeLimitReached = "passive.active_node_limit_reached";
    public const string PassiveKeystoneLimitReached = "passive.keystone_limit_reached";
    public const string PassiveMutualExclusion = "passive.mutual_exclusion";

    public const string LootRewardSourceMissing = "loot.reward_source_missing";
    public const string LootDropTableMissing = "loot.drop_table_missing";
    public const string LootWeightedItemMissing = "loot.weighted_item_missing";

    public const string EncounterCatalogUnavailable = "encounter.catalog_unavailable";
    public const string EncounterDefinitionMissing = "encounter.definition_missing";
    public const string EncounterEnemySquadMissing = "encounter.enemy_squad_missing";
    public const string EncounterEnemyEquipmentMissing = "encounter.enemy_equipment_missing";
    public const string EncounterBattleSetupFailed = "encounter.battle_setup_failed";

    public const string BattleSetupArchetypeMissing = "battle_setup.archetype_missing";
    public const string BattleSetupTraitMissing = "battle_setup.trait_missing";
    public const string BattleSetupItemMissing = "battle_setup.item_missing";
    public const string BattleSetupAffixMissing = "battle_setup.affix_missing";
    public const string BattleSetupAugmentMissing = "battle_setup.augment_missing";
}
