namespace SM.Unity.UI.Town;

public sealed record TownCharacterSheetPanelViewState(
    string Title,
    string Body);

public sealed record TownCharacterSheetViewState(
    TownCharacterSheetPanelViewState Overview,
    TownCharacterSheetPanelViewState Loadout,
    TownCharacterSheetPanelViewState Passives,
    TownCharacterSheetPanelViewState Synergy,
    TownCharacterSheetPanelViewState Progression);
