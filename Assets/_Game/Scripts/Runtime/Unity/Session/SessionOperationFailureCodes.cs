namespace SM.Unity;

internal static class SessionOperationFailureCodes
{
    internal const string GenericOperationFailed = "session.operation_failed";
    internal const string ItemNotFound = "session.item_not_found";
    internal const string HeroNotFound = "session.hero_not_found";
    internal const string ArchetypeNotFound = "session.archetype_not_found";

    internal const string RefitBalanceMissing = "session.refit.balance_missing";
    internal const string RefitItemBaseMissing = "session.refit.item_base_missing";
    internal const string RefitMagnitudeStateInvalid = "session.refit.magnitude_state_invalid";
    internal const string RefitItemSelectionRequired = "session.refit.item_selection_required";
    internal const string RefitTownOnly = "session.refit.town_only";
    internal const string RefitUnaffordable = "session.refit.unaffordable";
    internal const string RefitNoEffectiveStep = "session.refit.no_effective_step";
    internal const string RefitQuoteChanged = "session.refit.quote_changed";
    internal const string RefitCommitMismatch = "session.refit.commit_mismatch";

    internal const string InventoryTownOnly = "session.inventory.town_only";
    internal const string InventoryAlreadyEquipped = "session.inventory.already_equipped";
    internal const string EncounterPrepUnavailable = "session.encounter_prep.unavailable";
    internal const string EncounterPrepHeroInvalid = "session.encounter_prep.hero_invalid";

    internal const string PassiveTownOnly = "session.passive.town_only";
    internal const string PassiveBoardMissing = "session.passive.board_missing";
    internal const string PassiveLoadoutMissing = "session.passive.loadout_missing";
    internal const string PassiveNodeCatalogMissing = "session.passive.node_catalog_missing";

    internal const string RecruitTownOnly = "session.recruit.town_only";
    internal const string RecruitOfferInvalid = "session.recruit.offer_invalid";
    internal const string RecruitGoldInsufficient = "session.recruit.gold_insufficient";
    internal const string RecruitRosterFull = "session.recruit.roster_full";
    internal const string RecruitScoutDirectiveRequired = "session.recruit.scout_directive_required";
    internal const string RecruitScoutAlreadyUsed = "session.recruit.scout_already_used";
    internal const string RecruitEchoInsufficient = "session.recruit.echo_insufficient";
    internal const string RecruitLastHeroProtected = "session.recruit.last_hero_protected";
}
