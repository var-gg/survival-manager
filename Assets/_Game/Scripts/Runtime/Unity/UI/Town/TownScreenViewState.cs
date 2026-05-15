using SM.Unity.UI;

namespace SM.Unity.UI.Town;

/// <summary>잿골 hub Welcome hero standee — center stage, narrative receptionist.</summary>
public sealed record TownWelcomeHeroViewState(
    string HeroId,
    string EyebrowText,
    string HeroName,
    string Greeting,
    string HintText);

/// <summary>잿골 hub NPC menu entry — 4 거점 (달목 / 쇠매 / 갈마 / 솔길).</summary>
public sealed record TownNpcEntryViewState(
    string HintText);

public sealed record TownScreenViewState(
    string TitleEyebrow,
    string Title,
    string LocaleStatus,
    string LocaleKoLabel,
    string LocaleEnLabel,
    string HelpButtonLabel,
    HelpStripViewState Help,
    string SaveLabel,
    string LoadLabel,
    string ReturnToStartLabel,
    string ReturnToStartTooltip,
    bool CanReturnToStart,
    TownNpcEntryViewState DalmokEntry,
    TownNpcEntryViewState SoemaeEntry,
    TownNpcEntryViewState GalmaEntry,
    TownNpcEntryViewState SolgilEntry,
    TownWelcomeHeroViewState WelcomeHero,
    string RosterCountText,
    string ExpeditionLabel,
    string ExpeditionTooltip,
    string QuickBattleLabel,
    string QuickBattleTooltip,
    bool CanQuickBattle,
    bool ShowQuickBattle,
    string StatusText);
