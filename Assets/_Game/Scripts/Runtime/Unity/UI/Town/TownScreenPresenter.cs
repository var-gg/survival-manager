using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
using SM.Unity.UI;
using SM.Unity.UI.Bark;
using UnityEngine;

namespace SM.Unity.UI.Town;

/// <summary>
/// 잿골 hub V3 presenter — pindoc://decision-town-hub-v3-ashglen-face-cluster.
/// 얼굴 중심 cluster + 가변 deploy + 4 NPC ambient + utility entries + Atlas CTA.
/// NPC click 매핑은 controller가 SetNpcOpener로 inject — presenter는 dict lookup만.
/// Welcome captain V1 rotation: deploy squad 첫 hero (narrative cue 부관). Phase 8에 EmotionState 통합.
/// </summary>
public sealed class TownScreenPresenter
{
    private const string HelpPrefsKey = "SM.Help.Town";

    private readonly GameSessionRoot _root;
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly TownScreenView _view;
    private readonly ScreenHelpState _helpState;
    private readonly Dictionary<string, Action> _npcOpeners = new(StringComparer.Ordinal);
    // wave-36-barkbus: Town view 내 face cluster의 ambient bark 발사 채널.
    // Presenter가 단일 인스턴스 보유, view에 bind, NPC click 등 cue에서 publish.
    private readonly BarkBus _barkBus = new();
    private Action<string>? _heroOpener;
    private Action? _settingsOpener;
    private Action? _theaterOpener;
    private int _corePanelReadyCount;
    private int _corePanelTotalCount;
    private string _selectedHeroId = string.Empty;
    private string _pendingStatus = string.Empty;

    public TownScreenPresenter(
        GameSessionRoot root,
        GameLocalizationController localization,
        ContentTextResolver contentText,
        TownScreenView view)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _helpState = new ScreenHelpState(HelpPrefsKey);
    }

    public void Initialize() { _view.Bind(this); _view.BindBarkBus(_barkBus); Refresh(); }

    public BarkBus BarkBus => _barkBus;

    public void Refresh(string message = "")
    {
        if (!string.IsNullOrEmpty(message)) _pendingStatus = message;
        var session = _root.SessionState;
        session.EnsureBattleDeployReady();
        _view.Render(BuildState(session));
    }

    public void SelectKorean() => _localization.TrySetLocale("ko");
    public void SelectEnglish() => _localization.TrySetLocale("en");
    public void ToggleHelp() { _helpState.Toggle(); Refresh(); }
    public void DismissHelp() { _helpState.Dismiss(); Refresh(); }

    public void SaveProfile()
    {
        var checkpoint = _root.SaveProfile(SessionCheckpointKind.ManualSave);
        Refresh(checkpoint.Status == SessionCheckpointStatus.Success
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.profile_saved", "Profile saved.")
            : checkpoint.Message);
    }

    public void LoadProfile()
    {
        if (!_root.SessionState.CanManualProfileReload(out var reason))
        {
            _root.SessionState.RecordOperationalTelemetry(RuntimeOperationalTelemetry.CreateManualReloadBlocked(
                _root.ActiveProfileId, _root.ActiveProfileId, reason));
            Refresh(reason);
            return;
        }

        var checkpoint = _root.BindProfile(SessionCheckpointKind.ManualLoad);
        Refresh(checkpoint.Status == SessionCheckpointStatus.Success
            ? Localize(GameLocalizationTables.UITown, "ui.town.status.profile_loaded", "Profile reloaded.")
            : checkpoint.Message);
    }

    public void ReturnToStart()
    {
        if (IsReturnToStartBlocked(_root.SessionState))
        {
            Refresh(Localize(GameLocalizationTables.UITown, "ui.town.error.return_start_locked",
                "진행 중인 런이 있어 지금은 시작 화면으로 돌아갈 수 없습니다."));
            return;
        }

        var checkpoint = _root.ReturnToSessionMenu();
        if (!checkpoint.IsSuccessful) Refresh(checkpoint.Message);
    }

    public void OpenExpedition()
    {
        if (_root.SessionState.HasPendingRewardSettlement)
        {
            var checkpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
            if (!checkpoint.IsSuccessful) { Refresh(checkpoint.Message); return; }
            _root.SceneFlow.GoToReward();
            return;
        }

        if (_root.SessionState.CanResumeExpedition)
        {
            var checkpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
            if (!checkpoint.IsSuccessful) { Refresh(checkpoint.Message); return; }
            _root.SceneFlow.GoToAtlas();
            return;
        }

        // 엔딩 후 신규-런 분기는 무한 순환으로 라우팅 — CTA 라벨과 같은 판정(EndlessEntryResolver)을 읽고,
        // 세션 게이트(run/정산 대기)는 BeginEndlessExpedition 내부가 한 번 더 지킨다.
        if (EndlessEntryResolver.IsEndlessEntryActive(_root.SessionState.Profile.CampaignProgress))
        {
            _root.SessionState.BeginEndlessExpedition();
        }
        else
        {
            _root.SessionState.BeginNewExpedition();
        }

        var newRunCheckpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
        if (!newRunCheckpoint.IsSuccessful) { Refresh(newRunCheckpoint.Message); return; }
        _root.SceneFlow.GoToAtlas();
    }

    public void QuickBattle()
    {
        if (!_root.SessionState.CanStartQuickBattleSmoke)
        {
            Refresh(Localize(GameLocalizationTables.UITown, "ui.town.error.quick_battle_locked",
                "빠른 전투는 보상 정산이나 원정 진행 중에는 시작할 수 없습니다."));
            return;
        }

        var checkpoint = _root.SaveProfile(SessionCheckpointKind.TownExit);
        if (!checkpoint.IsSuccessful) { Refresh(checkpoint.Message); return; }

        _root.BeginTransientTownSmoke();
        _root.SessionState.PrepareTownQuickBattleSmoke();
        _root.SceneFlow.GoToBattle();
    }

    public void OpenSettings()
    {
        if (_settingsOpener != null) _settingsOpener.Invoke();
        // 별도 설정 패널(오디오 등)은 아직 없다. 유일한 설정인 언어 전환은 상단 KO/EN 토글로 이미 가능 —
        // dev 메모를 노출하던 자리를 플레이어가 이해할 honest 안내로 바꾼다(silent/jargon dead button 제거).
        // sibling placeholder 메시지(OpenTheater/OnNpcClick)와 같이 하드코딩 status 문자열 패턴을 따른다.
        else Refresh("설정은 준비 중입니다. 언어 전환은 상단의 한국어 / English 버튼을 사용하세요.");
    }

    public void OpenTheater()
    {
        if (_theaterOpener != null) _theaterOpener.Invoke();
        else Refresh("극장 (Theater) — story replay 후속 wire");
    }

    /// <summary>NPC 거점 click 라우팅 — controller가 SetNpcOpener로 inject한 modal opener invoke.</summary>
    public void OnNpcClick(string npcId)
    {
        // wave-36-barkbus: NPC click 시 contextual ambient bark 발사 — minimal viable demo.
        // 진짜 narrative bark catalog는 Phase 8 EmotionReactor + bark line table.
        PublishNpcAmbientBark(npcId);

        if (_npcOpeners.TryGetValue(npcId, out var open)) open.Invoke();
        else Refresh($"NPC 거점 미연결: {npcId}");
    }

    private void PublishNpcAmbientBark(string npcId)
    {
        // hardcoded baseline — 4 잿골 NPC ambient line. localization table 등록은 narrative pass.
        var (emotion, line) = npcId switch
        {
            "dalmok"  => ("amused",    "어이, 한 잔 하고 가시지요."),
            "soemae"  => ("neutral",   "쇠는 정직합니다. 두드리면 답이 옵지요."),
            "galma"   => ("concerned", "다친 곳은 없으십니까?"),
            "solgil"  => ("neutral",   "장부는 여기 다 있습니다."),
            _ => (string.Empty, string.Empty),
        };
        if (string.IsNullOrEmpty(line)) return;
        _barkBus.Publish(new BarkEvent(npcId, emotion, line));
    }

    /// <summary>Hero face card click — Phase 3 CharacterSheet modal 후속.</summary>
    public void OnHeroClick(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return;
        _selectedHeroId = heroId;
        if (_heroOpener != null)
        {
            _heroOpener.Invoke(heroId);
            return;
        }

        Refresh("동료 시트 opener 미연결");
    }

    public void SetNpcOpener(string npcId, Action open) => _npcOpeners[npcId] = open;
    public void SetHeroOpener(Action<string> open) => _heroOpener = open;
    public void SetSettingsOpener(Action open) => _settingsOpener = open;
    public void SetTheaterOpener(Action open) => _theaterOpener = open;
    public void SetCorePanelReadiness(int readyCount, int totalCount)
    {
        _corePanelReadyCount = Math.Max(0, readyCount);
        _corePanelTotalCount = Math.Max(0, totalCount);
    }

    private TownScreenViewState BuildState(GameSessionState session)
    {
        var showDebugActions = Application.isEditor || Debug.isDebugBuild;
        var statusText = _pendingStatus;
        _pendingStatus = string.Empty;

        var deployHeroIds = session.ExpeditionSquadHeroIds.ToList();
        var welcomeHeroId = deployHeroIds.FirstOrDefault()
            ?? session.Profile.Heroes.FirstOrDefault()?.HeroId
            ?? string.Empty;
        var welcomeHero = session.Profile.Heroes
            .FirstOrDefault(h => string.Equals(h.HeroId, welcomeHeroId, StringComparison.Ordinal));
        var welcomeName = ResolveHeroDisplayName(welcomeHero);
        var welcomeGreeting = string.IsNullOrEmpty(welcomeHeroId)
            ? Localize(GameLocalizationTables.UITown, "ui.town.welcome.empty", "잿골은 늘 그대로일세. 한 사람도 함께가 아닌가.")
            : Localize(GameLocalizationTables.UITown, "ui.town.welcome.greeting_default", "잿골은 늘 그대로일세. 잠시 숨을 돌리시지요.");

        // NPC 4 거점 — 정적 (Phase 8에 EmotionState/BarkBus 통합)
        var npcEntries = new List<TownNpcCardViewState>
        {
            new("dalmok",  "달목", "amused",    "tavern",  "모집"),
            new("soemae",  "쇠매", "neutral",   "forge",   "장비"),
            new("galma",   "갈마", "concerned", "clinic",  "수련"),
            new("solgil",  "솔길", "neutral",   "records", "창고"),
        };

        // Deploy row — ExpeditionSquadHeroIds (deploy 4 cap)
        var deployCards = new List<TownHeroCardViewState>();
        foreach (var hid in deployHeroIds)
        {
            var hero = session.Profile.Heroes.FirstOrDefault(h => string.Equals(h.HeroId, hid, StringComparison.Ordinal));
            var displayName = ResolveHeroCardName(hero);
            var badge = string.Equals(hid, welcomeHeroId, StringComparison.Ordinal) ? "captain" : "none";
            deployCards.Add(new TownHeroCardViewState(hid, displayName, "neutral", badge, IsDeploy: true,
                CharacterId: ResolvePortraitKey(hero?.CharacterId, hero?.ArchetypeId)));
        }

        // Roster row — Profile.Heroes 중 deploy 외
        var deployIdSet = new HashSet<string>(deployHeroIds, StringComparer.Ordinal);
        var rosterCards = session.Profile.Heroes
            .Where(h => !deployIdSet.Contains(h.HeroId))
            .Select(h => new TownHeroCardViewState(
                HeroId: h.HeroId,
                DisplayName: ResolveHeroCardName(h),
                EmotionKey: "neutral",
                BadgeKey: "none",
                IsDeploy: false,
                CharacterId: ResolvePortraitKey(h.CharacterId, h.ArchetypeId)))
            .ToList();

        return new TownScreenViewState(
            TitleEyebrow: Localize(GameLocalizationTables.UITown, "ui.town.eyebrow", "ASHGLEN — 잿골"),
            Title: Localize(GameLocalizationTables.UITown, "ui.town.title", "변방 잿골 마을"),
            LocaleStatus: BuildLocaleStatus(),
            LocaleKoLabel: GetLocaleButtonLabel("ko", "한국어"),
            LocaleEnLabel: GetLocaleButtonLabel("en", "English"),
            HelpButtonLabel: Localize(GameLocalizationTables.UICommon, "ui.common.help", "?"),
            Help: BuildHelpState(),
            SaveLabel: Localize(GameLocalizationTables.UICommon, "ui.common.save", "Save"),
            LoadLabel: Localize(GameLocalizationTables.UICommon, "ui.common.load", "Load"),
            SettingsLabel: Localize(GameLocalizationTables.UICommon, "ui.common.settings", "⚙"),
            ShowSettings: _settingsOpener != null,
            ReturnToStartLabel: Localize(GameLocalizationTables.UICommon, "ui.common.return_start", "Return to Start"),
            ReturnToStartTooltip: BuildReturnToStartTooltip(session),
            CanReturnToStart: !IsReturnToStartBlocked(session),
            NpcEntries: npcEntries,
            WelcomeCaptain: new TownWelcomeViewState(
                HeroId: welcomeHeroId,
                DisplayName: welcomeName,
                EmotionKey: "confident",
                Greeting: welcomeGreeting,
                CharacterId: ResolvePortraitKey(welcomeHero?.CharacterId, welcomeHero?.ArchetypeId)),
            DeployHeroes: deployCards,
            RosterHeroes: rosterCards,
            ExpeditionLabel: BuildExpeditionLabel(session),
            ExpeditionTooltip: BuildExpeditionTooltip(session),
            QuickBattleLabel: Localize(GameLocalizationTables.UITown, "ui.town.action.quick_battle_smoke", "빠른 전투"),
            QuickBattleTooltip: Localize(GameLocalizationTables.UITown, "ui.town.tooltip.quick_battle_smoke",
                "현재 마을 편성으로 짧은 전투를 확인하고 보상 정산을 거쳐 돌아옵니다."),
            CanQuickBattle: showDebugActions && session.CanStartQuickBattleSmoke,
            ShowQuickBattle: showDebugActions,
            StatusText: statusText,
            ServiceDecision: BuildServiceDecision(session, string.IsNullOrEmpty(_selectedHeroId) ? welcomeHeroId : _selectedHeroId),
            LedgerTitle: Localize(GameLocalizationTables.UITown, "ui.town.ledger.title", "LEDGER · 마을 비망록"),
            RosterEntryLabel: Localize(GameLocalizationTables.UITown, "ui.town.entry.roster", "◈  동료 명부"),
            CompendiumEntryLabel: Localize(GameLocalizationTables.UITown, "ui.town.entry.compendium", "도감"),
            TacticalSetupEntryLabel: Localize(GameLocalizationTables.UITown, "ui.town.entry.tactical_setup", "❖  전술 설정"),
            TacticalWorkshopEntryLabel: Localize(GameLocalizationTables.UITown, "ui.town.entry.tactical_workshop", "전술 공방"),
            PermanentAugmentEntryLabel: Localize(GameLocalizationTables.UITown, "ui.town.entry.permanent_augment", "✦  영구 강화"),
            TheaterEntryLabel: Localize(GameLocalizationTables.UITown, "ui.town.entry.theater", "▶  극장"),
            ShowTheater: _theaterOpener != null,
            ClusterEyebrow: Localize(GameLocalizationTables.UITown, "ui.town.cluster.eyebrow", "잿골에 머무는 동료"));
    }

    private TownServiceDecisionViewState BuildServiceDecision(GameSessionState session, string selectedHeroId)
    {
        var selectedHero = session.Profile.Heroes.FirstOrDefault(hero =>
            string.Equals(hero.HeroId, selectedHeroId, StringComparison.Ordinal));
        var heroLabel = selectedHero == null
            ? "동료 선택 없음"
            : ResolveHeroDisplayName(selectedHero);
        var deployCount = session.ExpeditionSquadHeroIds.Count;
        var panelTotal = _corePanelTotalCount > 0 ? _corePanelTotalCount : 10;
        var panelReady = _corePanelReadyCount > 0 ? _corePanelReadyCount : _npcOpeners.Count + (_heroOpener != null ? 1 : 0);

        // 플레이어 의미로: 영문 jargon(inventory/roster/deploy) + dev 텔레메트리("핵심 패널 N/M 준비") 제거.
        var allFacilitiesReady = panelReady >= panelTotal;
        return new TownServiceDecisionViewState(
            SelectedHeroLabel: heroLabel,
            WalletLabel: $"{session.Profile.Currencies.Gold:N0} 골드 · {session.Profile.Currencies.Echo:N0} 잔향",
            InventoryLabel: $"소지품 {session.Profile.Inventory.Count}개",
            RosterPressureLabel: $"동료 {session.Profile.Heroes.Count}/12 · 출전 {deployCount}/4",
            ModalAvailabilityLabel: allFacilitiesReady
                ? "모든 시설 이용 가능"
                : $"시설 {panelReady}/{panelTotal} 이용 가능");
    }

    // 흉상 resolve 키 — instance CharacterId 우선, 비면 archetype id로 fallback
    // (ContentIconResolver.CharacterArtAliases가 archetype id도 매핑). 둘 다 비면 빈 문자열 → 글리프 fallback.
    private static string ResolvePortraitKey(string? characterId, string? archetypeId)
    {
        if (!string.IsNullOrWhiteSpace(characterId)) return characterId!;
        return string.IsNullOrWhiteSpace(archetypeId) ? string.Empty : archetypeId!;
    }

    private string ResolveHeroDisplayName(HeroInstanceRecord? hero)
        => HeroDisplayLabelFormatter.ResolvePersonAndJob(
            hero,
            _contentText.GetCharacterName,
            _contentText.GetArchetypeName);

    // 흉상 카드는 person·job을 담지 못해 전부 말줄임으로 끝났다(8장 전부 "..."). 잘린 직군보다
    // 이름만 온전한 쪽이 읽힌다 — 직군과 한자 병기는 자리가 있는 표면(캐릭터 시트·정산·출격 확인)에 남는다.
    private string ResolveHeroCardName(HeroInstanceRecord? hero)
        => HeroDisplayLabelFormatter.ResolvePersonNameCompact(hero, _contentText.GetCharacterName);

    private string BuildLocaleStatus()
    {
        var locale = _localization.CurrentLocale;
        if (locale == null) return "-";
        return $"{Localize(GameLocalizationTables.UICommon, "ui.common.current_language", "Current")}: {_localization.GetLocaleButtonLabel(locale)}";
    }

    private string GetLocaleButtonLabel(string localeCode, string fallback)
    {
        var locale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales?.GetLocale(localeCode);
        return locale != null ? _localization.GetLocaleButtonLabel(locale) : fallback;
    }

    private HelpStripViewState BuildHelpState()
    {
        return new HelpStripViewState(
            _helpState.IsVisible,
            Localize(GameLocalizationTables.UITown, "ui.town.help.body",
                "달목·쇠매·갈마·솔길 — 4 거점 NPC를 통해 모집·장비·수련·창고로. 우측 명부에서 동료를, 원정 버튼으로 출발."),
            Localize(GameLocalizationTables.UICommon, "ui.common.hide", "Hide"));
    }

    private string BuildExpeditionLabel(GameSessionState session)
    {
        if (session.HasPendingRewardSettlement)
        {
            return Localize(GameLocalizationTables.UIReward, "ui.reward.action.open", "Open Reward");
        }

        if (session.CanResumeExpedition)
        {
            return Localize(GameLocalizationTables.UITown, "ui.town.action.resume_expedition", "Resume Expedition");
        }

        // 엔딩 후에는 원정 CTA가 무한 순환 진입으로 전환된다 — 버튼은 하나, 라벨/라우팅만 바뀐다(죽은 affordance 방지).
        if (EndlessEntryResolver.IsEndlessEntryActive(session.Profile.CampaignProgress))
        {
            return Localize(GameLocalizationTables.UITown, "ui.town.action.start_endless_cycle", "무한 순환 시작");
        }

        // 신규 플레이어(첫 원정 루프 전)에게는 막연한 "원정 시작" 대신 "첫 원정 시작"으로 다음 행동을 명확히 가리킨다.
        return FirstRunStatusResolver.IsFirstRunActive(session.Profile.CampaignProgress)
            ? Localize(GameLocalizationTables.UITown, "ui.town.action.start_first_expedition", "첫 원정 시작")
            : Localize(GameLocalizationTables.UITown, "ui.town.action.start_expedition", "원정으로");
    }

    private string BuildExpeditionTooltip(GameSessionState session)
    {
        if (session.HasPendingRewardSettlement)
        {
            return Localize(GameLocalizationTables.UITown, "ui.town.tooltip.expedition_reward",
                "Open Reward to settle the previous node before continuing.");
        }

        if (session.CanResumeExpedition)
        {
            return Localize(GameLocalizationTables.UITown, "ui.town.tooltip.expedition_resume",
                "Resume the authored expedition from the currently selected route.");
        }

        if (EndlessEntryResolver.IsEndlessEntryActive(session.Profile.CampaignProgress))
        {
            // 인자는 Localize 안으로 넣는다. 2026-07-31 이전에는 인자 없이 Localize 를 부른 뒤
            // 결과를 string.Format 했는데, 이 항목은 smart format 이라 로컬라이제이션 단계에서
            // 이미 {0} 을 셀렉터로 평가하려다 매번 터졌다 —
            //   OperationException : Error parsing format string: Could not evaluate the selector "0"
            // 엔딩 이후 마을을 그릴 때마다 났고, 그 화면에 도달하는 경로가 없어서 안 보였다.
            var cycle = session.StoryDirector.Progress.EndlessCycle;
            return Localize(
                GameLocalizationTables.UITown,
                "ui.town.tooltip.expedition_endless",
                "무한 순환 {0}회차 — 순환마다 적이 강해지고(열기 {1}) 잔향 보상이 커집니다.",
                cycle.CycleIndex + 1,
                cycle.Heat + 1);
        }

        return FirstRunStatusResolver.IsFirstRunActive(session.Profile.CampaignProgress)
            ? Localize(GameLocalizationTables.UITown, "ui.town.tooltip.expedition_first_run",
                "동료가 준비됐습니다. 첫 원정을 시작해 전투 → 보상 → 성장 루프를 경험하세요.")
            : Localize(GameLocalizationTables.UITown, "ui.town.tooltip.expedition_start",
                "Begin the authored expedition loop with the current preparation.");
    }

    private string BuildReturnToStartTooltip(GameSessionState session)
    {
        return IsReturnToStartBlocked(session)
            ? Localize(GameLocalizationTables.UITown, "ui.town.tooltip.return_start_locked",
                "Blocked while an authored run or smoke battle is still active.")
            : Localize(GameLocalizationTables.UITown, "ui.town.tooltip.return_start",
                "Leave the local run shell and go back to Boot.");
    }

    private static bool IsReturnToStartBlocked(GameSessionState session)
    {
        return session.HasActiveExpeditionRun || session.IsQuickBattleSmokeActive;
    }

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _localization.LocalizeOrFallback(table, key, fallback, args);
    }
}
