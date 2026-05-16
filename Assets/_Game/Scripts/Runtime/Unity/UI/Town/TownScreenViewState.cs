using System.Collections.Generic;
using SM.Unity.UI;

namespace SM.Unity.UI.Town;

/// <summary>잿골 hub V3 NPC 거점 card — 4 NPC (달목/쇠매/갈마/솔길).</summary>
public sealed record TownNpcCardViewState(
    string NpcId,
    string DisplayName,
    string EmotionKey,
    string BadgeKey);

/// <summary>잿골 hub V3 hero face card — deploy highlight 또는 roster thumbnail.</summary>
public sealed record TownHeroCardViewState(
    string HeroId,
    string DisplayName,
    string EmotionKey,
    string BadgeKey,    // captain (deploy 첫 hero) 또는 none
    bool IsDeploy);

/// <summary>잿골 hub V3 Welcome captain — center stage standee, narrative receptionist.</summary>
public sealed record TownWelcomeViewState(
    string HeroId,
    string DisplayName,
    string EmotionKey,
    string Greeting);

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
    string SettingsLabel,
    string ReturnToStartLabel,
    string ReturnToStartTooltip,
    bool CanReturnToStart,
    IReadOnlyList<TownNpcCardViewState> NpcEntries,
    TownWelcomeViewState WelcomeCaptain,
    IReadOnlyList<TownHeroCardViewState> DeployHeroes,
    IReadOnlyList<TownHeroCardViewState> RosterHeroes,
    string ExpeditionLabel,
    string ExpeditionTooltip,
    string QuickBattleLabel,
    string QuickBattleTooltip,
    bool CanQuickBattle,
    bool ShowQuickBattle,
    string StatusText);
