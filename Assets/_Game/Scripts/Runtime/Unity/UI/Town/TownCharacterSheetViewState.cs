namespace SM.Unity.UI.Town;

public sealed record TownCharacterSheetPanelViewState(
    string Title,
    string Body);

public sealed record TownCharacterSheetViewState(
    string HeroId,
    string DisplayName,
    string ArchetypeLabel,
    string RoleLabel,
    string FamilyKey,
    TownCharacterSheetPanelViewState Overview,
    TownCharacterSheetPanelViewState Loadout,
    TownCharacterSheetPanelViewState Passives,
    TownCharacterSheetPanelViewState Synergy,
    TownCharacterSheetPanelViewState Progression);
