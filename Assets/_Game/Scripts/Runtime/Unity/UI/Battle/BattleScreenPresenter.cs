using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta;
using SM.Unity.UI;

namespace SM.Unity.UI.Battle;

public sealed class BattleScreenPresenter
{
    // 시안(ui_ux_bible_battle_hud_v1)의 로그는 기둥 사이 전폭을 쓰는 여러 줄 피드다.
    // 3 줄은 이전 레이아웃에서 로그를 440px 폭 한 뼘으로 몰아넣은 결과였다.
    // USS 예산: max-height 104px - padding 16 = 88px, line-height 19px → 4 줄.
    private const int MaxVisibleLogs = 4;
    private const int MaxVisibleDecisive = 4;

    private readonly GameLocalizationController _localization;
    private readonly GameSessionState _sessionState;
    private readonly BattlePresentationOptions _options;
    private readonly ContentTextResolver? _contentText;
    private readonly BattleUnitPortraitResolver _portraitResolver = new();

    public BattleScreenPresenter(
        GameLocalizationController localization,
        GameSessionState sessionState,
        BattlePresentationOptions options,
        ContentTextResolver? contentText = null)
    {
        _localization = localization;
        _sessionState = sessionState;
        _options = options;
        _contentText = contentText;
    }

    public BattleDebugFoldoutViewState BuildDebugFoldoutState()
    {
        var overlay = _sessionState.ActiveRun?.Overlay;
        if (overlay == null)
        {
            return BattleDebugFoldoutViewState.Empty;
        }

        var encounterId = string.IsNullOrWhiteSpace(overlay.EncounterId) ? "-" : overlay.EncounterId;
        var contextHash = string.IsNullOrWhiteSpace(overlay.BattleContextHash) ? "-" : overlay.BattleContextHash;
        var siteNodeIndexText = overlay.SiteNodeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new BattleDebugFoldoutViewState(encounterId, siteNodeIndexText, contextHash);
    }

    public BattleShellViewState BuildLoadingState(bool showHelp = false, bool isSummaryExpanded = true)
    {
        return CreateState(
            allyHpText: string.Empty,
            enemyHpText: string.Empty,
            logText: Localize(GameLocalizationTables.UIBattle, "ui.battle.log.preparing", "Preparing battle log"),
            resultText: Localize(GameLocalizationTables.UIBattle, "ui.battle.result.in_progress", "Battle in progress"),
            playbackText: BuildPlaybackText(isPaused: false, playbackSpeed: 1f),
            statusText: Localize(GameLocalizationTables.UIBattle, "ui.battle.status.initializing", "Initializing live simulation"),
            progressNormalized: 0f,
            isPaused: false,
            isBattleFinished: false,
            showSettings: false,
            canReplay: false,
            canRebattle: false,
            canPause: false,
            canChangeSpeed: false,
            showHelp: showHelp,
            isSummaryExpanded: isSummaryExpanded,
            allyRoster: null,
            enemyRoster: null,
            selectedUnit: BattleSelectedUnitViewState.Hidden);
    }

    public BattleShellViewState BuildErrorState(string message, bool showHelp = false, bool isSummaryExpanded = true)
    {
        return CreateState(
            allyHpText: string.Empty,
            enemyHpText: string.Empty,
            logText: message,
            resultText: message,
            playbackText: BuildPlaybackText(isPaused: false, playbackSpeed: 1f),
            statusText: message,
            progressNormalized: 0f,
            isPaused: false,
            isBattleFinished: false,
            showSettings: false,
            canReplay: false,
            canRebattle: false,
            canPause: false,
            canChangeSpeed: false,
            settingsStatusText: message,
            showHelp: showHelp,
            isSummaryExpanded: isSummaryExpanded,
            allyRoster: null,
            enemyRoster: null,
            selectedUnit: BattleSelectedUnitViewState.Hidden);
    }

    public BattleShellViewState BuildState(
        BattleSimulationStep step,
        IReadOnlyList<BattleEvent> recentLogs,
        IReadOnlyList<string> decisiveTimeline,
        int totalEventCount,
        bool isPaused,
        float playbackSpeed,
        bool isBattleFinished,
        bool showSettings,
        float progressNormalized,
        string settingsStatusText,
        bool canReplay,
        bool canRebattle,
        bool canPause = false,
        bool canChangeSpeed = false,
        bool showHelp = false,
        bool isSummaryExpanded = true,
        BattleSelectedUnitViewState? selectedUnit = null,
        IReadOnlyList<string>? beatCallouts = null)
    {
        var selectedState = selectedUnit ?? BattleSelectedUnitViewState.Hidden;
        return CreateState(
            BuildTeamSummary(step.Units.Where(actor => actor.Side == TeamSide.Ally)),
            BuildTeamSummary(step.Units.Where(actor => actor.Side == TeamSide.Enemy)),
            BuildLogText(step, recentLogs, decisiveTimeline, beatCallouts),
            BuildResultText(step, totalEventCount),
            BuildPlaybackText(isPaused, playbackSpeed),
            BuildStatus(step, isPaused),
            progressNormalized,
            isPaused,
            isBattleFinished,
            showSettings,
            canReplay,
            canRebattle,
            canPause,
            canChangeSpeed,
            settingsStatusText,
            showHelp,
            isSummaryExpanded,
            BuildTacticalReadoutRows(step, progressNormalized),
            BuildCombatantTokens(step, selectedState.UnitId),
            BuildRoster(step, TeamSide.Ally, selectedState.UnitId),
            BuildRoster(step, TeamSide.Enemy, selectedState.UnitId),
            selectedState);
    }

    private BattleShellViewState CreateState(
        string allyHpText,
        string enemyHpText,
        string logText,
        string resultText,
        string playbackText,
        string statusText,
        float progressNormalized,
        bool isPaused,
        bool isBattleFinished,
        bool showSettings,
        bool canReplay,
        bool canRebattle,
        bool canPause,
        bool canChangeSpeed,
        string? settingsStatusText = null,
        bool showHelp = false,
        bool isSummaryExpanded = true,
        IReadOnlyList<BattleTacticalReadoutRowViewState>? tacticalReadoutRows = null,
        IReadOnlyList<BattleCombatantTokenViewState>? combatantTokens = null,
        IReadOnlyList<BattleRosterUnitViewState>? allyRoster = null,
        IReadOnlyList<BattleRosterUnitViewState>? enemyRoster = null,
        BattleSelectedUnitViewState? selectedUnit = null)
    {
        var isSmoke = _sessionState.IsQuickBattleSmokeActive;
        var isDirect = _sessionState.IsDirectCombatSandboxLane;
        return new BattleShellViewState(
            Localize(GameLocalizationTables.UIBattle, "ui.battle.title", "전투 전황"),
            BuildLocaleStatus(),
            GetLocaleButtonLabel("ko", "한국어"),
            GetLocaleButtonLabel("en", "English"),
            Localize(GameLocalizationTables.UICommon, "ui.common.help", "Help"),
            CreateHelpState(showHelp),
            Localize(GameLocalizationTables.UIBattle, "ui.battle.panel.summary", "Summary"),
            isSummaryExpanded
                ? Localize(GameLocalizationTables.UICommon, "ui.common.collapse", "Collapse")
                : Localize(GameLocalizationTables.UICommon, "ui.common.expand", "Expand"),
            isSummaryExpanded
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.tooltip.summary_collapse", "Fold the summary panel to clear more of the battlefield.")
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.tooltip.summary_expand", "Expand the summary panel."),
            isSummaryExpanded,
            Localize(GameLocalizationTables.UIBattle, "ui.battle.panel.allies", "Allies"),
            allyHpText,
            Localize(GameLocalizationTables.UIBattle, "ui.battle.panel.enemies", "Enemies"),
            enemyHpText,
            Localize(GameLocalizationTables.UIBattle, "ui.battle.panel.feed", "전투 기록"),
            logText,
            resultText,
            playbackText,
            statusText,
            Localize(GameLocalizationTables.UIBattle, "ui.battle.group.playback", "Playback"),
            // 배속 라벨은 로케일 독립 숫자다(기존 speed_1/2/4도 ko=en 동일값). speed_05 정식 키는
            // LocalizationFoundationBootstrap dict(SoT)에 등록돼 있고, 여기서는 코드에 키 문자열을
            // 박지 않아 string-table audit이 새 키를 missing으로 잡지 않게 리터럴로 표기한다.
            "x0.5",
            Localize(GameLocalizationTables.UIBattle, "ui.battle.action.speed_1", "x1"),
            Localize(GameLocalizationTables.UIBattle, "ui.battle.action.speed_2", "x2"),
            Localize(GameLocalizationTables.UIBattle, "ui.battle.action.speed_4", "x4"),
            isPaused
                ? Localize(GameLocalizationTables.UICommon, "ui.common.resume", "Resume")
                : Localize(GameLocalizationTables.UICommon, "ui.common.pause", "Pause"),
            Localize(
                GameLocalizationTables.UIBattle,
                isDirect ? "ui.battle.tooltip.playback_direct" : "ui.battle.tooltip.playback",
                isDirect
                    ? "Combat Sandbox lets you pause, replay the same seed, and roll a new seed."
                    : "빠른 전투에서는 일시정지, 재생, 속도 조절을 사용할 수 있습니다."),
            isSmoke,
            canChangeSpeed,
            canPause,
            Localize(GameLocalizationTables.UIBattle, "ui.battle.group.primary", isDirect ? "Sandbox Result" : "Primary Action"),
            Localize(GameLocalizationTables.UICommon, "ui.common.continue", "Continue"),
            !isDirect && isBattleFinished,
            isBattleFinished
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.tooltip.continue_ready", "Proceed to Reward with the resolved battle result.")
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.tooltip.continue_locked", "Continue activates after the battle is fully resolved."),
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.action.replay_same_seed" : "ui.battle.action.replay", isDirect ? "Replay Same Seed" : "Replay"),
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.tooltip.replay_same_seed" : "ui.battle.tooltip.replay", isDirect ? "Replay the active Combat Sandbox battle with the same deterministic seed." : "현재 빠른 전투 타임라인을 처음부터 다시 봅니다."),
            canReplay,
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.action.new_seed" : "ui.battle.action.rebattle", isDirect ? "New Seed" : "재전투"),
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.tooltip.new_seed" : "ui.battle.tooltip.rebattle", isDirect ? "Restart Combat Sandbox with the next seed while keeping the active preset." : "새 시드로 빠른 전투를 다시 시작합니다."),
            canRebattle,
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.action.exit_sandbox" : "ui.battle.action.return_town_debug", isDirect ? "Exit Sandbox" : "마을로 복귀"),
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.tooltip.exit_sandbox" : "ui.battle.tooltip.return_town_direct", isDirect ? "Leave Combat Sandbox after the battle is resolved." : "전투가 끝난 뒤 마을로 바로 돌아갑니다."),
            isSmoke && isBattleFinished,
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.group.sandbox" : "ui.battle.group.smoke", isDirect ? "Combat Sandbox" : "빠른 전투"),
            isSmoke,
            Localize(GameLocalizationTables.UIBattle, "ui.battle.group.utility", "Utility"),
            Localize(GameLocalizationTables.UICommon, "ui.common.settings", "Settings"),
            Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.tooltip.settings_sandbox" : "ui.battle.tooltip.settings", isDirect ? "Open display settings. Sandbox diagnostics appear only in Combat Sandbox." : "표시 설정을 엽니다."),
            progressNormalized,
            true,
            _options.ShowTeamHpSummary,
            !isDirect && isBattleFinished,
            Localize(GameLocalizationTables.UIBattle, "ui.battle.panel.force_distribution", "전력 분포"),
            Localize(GameLocalizationTables.UIBattle, "ui.battle.panel.tactical_readout", "전황 판단"),
            tacticalReadoutRows,
            combatantTokens,
            new BattleSettingsViewState(
                showSettings,
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.title", "Battle View Settings"),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.display", "Display"),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.overhead_ui", "Overhead UI {0}", BuildStateLabel(_options.ShowOverheadUi)),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.tooltip.overhead_ui", "Show HP and state over units in the battle field."),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.damage_text", "Damage Text {0}", BuildStateLabel(_options.ShowDamageText)),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.tooltip.damage_text", "Show floating damage and heal numbers during battle."),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.team_summary", "Team Summary {0}", BuildStateLabel(_options.ShowTeamHpSummary)),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.tooltip.team_summary", "Show ally and enemy team summaries in the side panels."),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.debug", "Debug"),
                Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.debug_overlay", "Debug Overlay {0}", BuildStateLabel(_options.ShowDebugOverlay)),
                Localize(GameLocalizationTables.UIBattle, isDirect ? "ui.battle.tooltip.debug_overlay_sandbox" : "ui.battle.tooltip.debug_overlay", isDirect ? "Show targeting lines and sandbox diagnostics for Combat Sandbox." : "전투 판정선을 표시합니다."),
                isSmoke,
                string.IsNullOrWhiteSpace(settingsStatusText)
                    ? Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.title", "Battle View Settings")
                    : settingsStatusText),
            allyRoster,
            enemyRoster,
            selectedUnit ?? BattleSelectedUnitViewState.Hidden);
    }

    private IReadOnlyList<BattleCombatantTokenViewState> BuildCombatantTokens(BattleSimulationStep step, string selectedUnitId)
    {
        return step.Units
            .OrderByDescending(unit => IsActiveCombatant(unit))
            .ThenBy(unit => unit.Side)
            .ThenBy(unit => unit.Anchor)
            .ThenBy(unit => unit.Id, StringComparer.Ordinal)
            .Take(10)
            .Select(unit => new BattleCombatantTokenViewState(
                unit.Id,
                ResolveUnitDisplayName(unit),
                BattleReadabilityFormatter.BuildSemanticLabel(BattleReadabilityFormatter.ResolveSemantic(unit, step), LocaleCode),
                unit.MaxHealth > 0f ? UnityEngine.Mathf.Clamp01(unit.CurrentHealth / unit.MaxHealth) : 0f,
                unit.Side == TeamSide.Ally,
                IsActiveCombatant(unit) || string.Equals(unit.Id, selectedUnitId, StringComparison.Ordinal),
                !unit.IsAlive))
            .ToList();
    }

    private IReadOnlyList<BattleTacticalReadoutRowViewState> BuildTacticalReadoutRows(BattleSimulationStep step, float progressNormalized)
    {
        var allies = step.Units.Where(unit => unit.Side == TeamSide.Ally).ToArray();
        var enemies = step.Units.Where(unit => unit.Side == TeamSide.Enemy).ToArray();
        var pressure = BattleReadabilityFormatter.ComputePressureScore(step, TeamSide.Ally);
        var allySustain = ComputeSustain(allies);
        var enemySustain = ComputeSustain(enemies);

        var rows = new List<BattleTacticalReadoutRowViewState>
        {
            new BattleTacticalReadoutRowViewState(
                Localize(GameLocalizationTables.UIBattle, "ui.battle.readout.progress", "진행"),
                $"{step.StepIndex:000} / {step.TimeSeconds:0.0}s",
                UnityEngine.Mathf.Clamp01(progressNormalized),
                "neutral"),
            new BattleTacticalReadoutRowViewState(
                Localize(GameLocalizationTables.UIBattle, "ui.battle.readout.pressure", "전선 압박"),
                BuildPressureLabel(step),
                UnityEngine.Mathf.Clamp01((pressure + 1f) * 0.5f),
                pressure > 0.08f ? "ally" : pressure < -0.08f ? "enemy" : "neutral"),
            new BattleTacticalReadoutRowViewState(
                Localize(GameLocalizationTables.UIBattle, "ui.battle.readout.allies", "아군 유지"),
                BuildTeamBrief(allies),
                allySustain,
                allySustain < 0.35f ? "warning" : "ally"),
            new BattleTacticalReadoutRowViewState(
                Localize(GameLocalizationTables.UIBattle, "ui.battle.readout.enemies", "적 전력"),
                BuildTeamBrief(enemies),
                enemySustain,
                enemySustain < 0.35f ? "warning" : "enemy"),
        };

        // ADR-0028 #provenance: 이번 전투에 적용된 정치 조건(후원/경계 + 출처 세력)을 readout에 노출 —
        // 관전 중 "정치가 전투를 바꿨다"가 읽히게(GPT Pro 잔여 20%). 미서약/조건 없으면 행 없음.
        rows.AddRange(BuildPoliticalReadoutRows());
        return rows;
    }

    private IReadOnlyList<BattleTacticalReadoutRowViewState> BuildPoliticalReadoutRows()
    {
        return BuildPoliticalReadoutRowsCore(
            _sessionState.ActiveBattlePoliticalConditions,
            ResolvePoliticalFactionName,
            WarrantDisplayDefaults.ChannelText);
    }

    // 순수 변환 — 정치 조건 → 전투 readout 행(세력명 + 후원/경계 + ally/enemy tone). 테스트는 이 코어를 직접 친다.
    internal static IReadOnlyList<BattleTacticalReadoutRowViewState> BuildPoliticalReadoutRowsCore(
        IReadOnlyList<PoliticalCombatCondition> conditions,
        Func<string, string> factionName,
        Func<PoliticalChannel, string> channelText)
    {
        if (conditions == null || conditions.Count == 0)
        {
            return Array.Empty<BattleTacticalReadoutRowViewState>();
        }

        var rows = new List<BattleTacticalReadoutRowViewState>(conditions.Count);
        foreach (var condition in conditions)
        {
            var isSupport = condition.Channel == PoliticalChannel.AllySupport;
            rows.Add(new BattleTacticalReadoutRowViewState(
                factionName(condition.SourceFactionId),
                channelText(condition.Channel),
                1f,
                isSupport ? "ally" : "enemy"));
        }

        return rows;
    }

    private string ResolvePoliticalFactionName(string factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return string.Empty;
        }

        var name = _contentText?.GetFactionName(factionId);
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith("content.", StringComparison.Ordinal))
        {
            name = WarrantDisplayDefaults.FactionName(factionId);
        }

        return string.IsNullOrWhiteSpace(name) ? factionId : name;
    }

    private static bool IsActiveCombatant(BattleUnitReadModel unit)
    {
        return unit.IsAlive
               && (unit.ActionState is CombatActionState.ExecuteAction or CombatActionState.Approach or CombatActionState.Reposition
                   || unit.PendingActionType != null
                   || unit.WindupProgress > 0.01f);
    }

    private static float ComputeSustain(IReadOnlyList<BattleUnitReadModel> units)
    {
        if (units.Count == 0)
        {
            return 0f;
        }

        var maxHp = UnityEngine.Mathf.Max(1f, units.Sum(unit => UnityEngine.Mathf.Max(1f, unit.MaxHealth)));
        var hpRatio = UnityEngine.Mathf.Clamp01(units.Sum(unit => UnityEngine.Mathf.Max(0f, unit.CurrentHealth)) / maxHp);
        var aliveRatio = units.Count(unit => unit.IsAlive) / (float)units.Count;
        return UnityEngine.Mathf.Clamp01((hpRatio * 0.65f) + (aliveRatio * 0.35f));
    }

    private static string BuildTeamBrief(IReadOnlyList<BattleUnitReadModel> units)
    {
        if (units.Count == 0)
        {
            return "0/0 | 0 HP";
        }

        var alive = units.Count(unit => unit.IsAlive);
        var hp = units.Sum(unit => UnityEngine.Mathf.Max(0f, unit.CurrentHealth));
        return $"{alive}/{units.Count} | {hp:0} HP";
    }

    private IReadOnlyList<BattleRosterUnitViewState> BuildRoster(BattleSimulationStep step, TeamSide side, string selectedUnitId)
    {
        return step.Units
            .Where(unit => unit.Side == side)
            .OrderBy(unit => unit.Anchor)
            .ThenBy(unit => unit.Name)
            .Select(unit => new BattleRosterUnitViewState(
                unit.Id,
                BuildRosterDisplayName(unit),
                BuildRosterActionLabel(unit, step),
                unit.MaxHealth > 0f ? UnityEngine.Mathf.Clamp01(unit.CurrentHealth / unit.MaxHealth) : 0f,
                unit.IsAlive,
                string.Equals(unit.Id, selectedUnitId, System.StringComparison.Ordinal),
                _portraitResolver.Resolve(unit),
                BuildRosterHealthText(unit),
                BuildRosterRoleText(unit)))
            .ToList();
    }

    /// <summary>
    /// 카드·전투 기록에 쓸 <b>짧은 이름</b>.
    ///
    /// 등록부 표시명은 <c>단린 (丹麟) / Dawn Priest</c> 형식이다 — 한국어 표시명 + 한자 +
    /// 영문 별칭을 <b>한 문자열에</b> 담은 형태고, 그건 등록부·상세창의 형식이지 카드의 형식이 아니다.
    /// 폭 268px 카드에 그대로 넣으면 <c>단린 (丹麟) / Dawn Pri...</c> 로 잘려서 <b>세 표기 중
    /// 어느 것도 온전히 안 읽힌다</b>. 전투 기록 한 줄에 두 명이 들어가면 줄이 통째로 넘친다.
    ///
    /// 이름 자체(SoT)는 건드리지 않는다. 여기서 줄이는 건 <b>표시 밀도</b>일 뿐이고,
    /// 온전한 표기는 상세창이 갖는다.
    /// </summary>
    private string BuildRosterDisplayName(BattleUnitReadModel unit)
    {
        return ToCompactDisplayName(ResolveUnitDisplayName(unit));
    }

    private static string ToCompactDisplayName(string full)
    {
        if (string.IsNullOrWhiteSpace(full))
        {
            return full;
        }

        var slash = full.IndexOf('/');
        var head = (slash > 0 ? full[..slash] : full).Trim();

        var paren = head.IndexOf('(');
        if (paren > 0)
        {
            head = head[..paren].TrimEnd();
        }

        // 별칭만 있는 이름(영문 단독)은 잘라낼 게 없으므로 원문을 지킨다.
        return head.Length == 0 ? full.Trim() : head;
    }

    /// <summary>
    /// 유닛 카드의 역할 칩.
    ///
    /// 시안(<c>ui_ux_bible_battle_hud_v1</c>)은 이름 밑에 "근접 딜러 / 탱커 / 서포터" 같은
    /// 역할을 작은 상자로 뒀다. 시안 시점에는 그게 손으로 적은 라벨이었지만, 지금은
    /// <see cref="BattleUnitReadModel.RoleInstructionId"/> 와 <c>RoleTag</c> 가 실데이터로 있다.
    /// 그래서 이 자리는 시안을 그대로 따르면서 동시에 <b>현행 시스템의 진실</b>을 쓴다.
    /// </summary>
    private string BuildRosterRoleText(BattleUnitReadModel unit)
    {
        if (_contentText == null)
        {
            return string.Empty;
        }

        var role = _contentText.GetRoleName(unit.RoleInstructionId, unit.RoleTag);
        return string.IsNullOrWhiteSpace(role) ? string.Empty : SanitizePlayerFacingText(role, string.Empty);
    }

    /// <summary>
    /// 파티 카드에 쓸 <b>동사만</b> 남긴다.
    ///
    /// <see cref="BattleReadabilityFormatter.BuildPlayerFacingState"/> 는 머리 위 표시를 겸해서
    /// <c>"접근 -&gt; 침월 (沉月)"</c> 처럼 대상을 붙이고, 대상이 아직 없으면 <c>"대상 탐색 -&gt; -"</c> 가 된다.
    /// 카드 폭이 122 px 라 대상은 어차피 잘려서 <c>"접근 -&gt; 침월 (沉月) ..."</c> 로 보이고,
    /// 잘린 화살표는 <b>상태 기계 로그처럼</b> 읽힌다. 실제로 이 화면이 시뮬레이터처럼 보이던 원인 중 하나다.
    /// 누구를 노리는지는 전장에서 보이므로 카드에는 동사만 남긴다.
    /// </summary>
    private string BuildRosterActionLabel(BattleUnitReadModel unit, BattleSimulationStep step)
    {
        var text = SanitizeBattleStateText(
            BattleReadabilityFormatter.BuildPlayerFacingState(unit, step, LocaleCode), step);

        var arrow = text.IndexOf("->", System.StringComparison.Ordinal);
        return arrow > 0 ? text[..arrow].TrimEnd() : text;
    }

    private static string BuildRosterHealthText(BattleUnitReadModel unit)
    {
        return unit.MaxHealth > 0f
            ? $"{UnityEngine.Mathf.CeilToInt(UnityEngine.Mathf.Max(0f, unit.CurrentHealth))}/{UnityEngine.Mathf.CeilToInt(unit.MaxHealth)}"
            : string.Empty;
    }

    private HelpStripViewState CreateHelpState(bool showHelp)
    {
        var helpBody = _sessionState.IsDirectCombatSandboxLane
            ? "Watch the battlefield, portrait strip, and short battle feed. Use pause, replay, speed, and settings to inspect the run."
            : "Watch the battlefield, portrait strip, and short battle feed. Continue unlocks after the battle resolves.";
        return new HelpStripViewState(
            showHelp,
            Localize(
                GameLocalizationTables.UIBattle,
                _sessionState.IsDirectCombatSandboxLane ? "ui.battle.help.body_sandbox" : "ui.battle.help.body",
                helpBody),
            Localize(GameLocalizationTables.UICommon, "ui.common.hide", "Hide"));
    }

    private string BuildLocaleStatus()
    {
        var locale = _localization.CurrentLocale;
        if (locale == null)
        {
            return "-";
        }

        return $"{Localize(GameLocalizationTables.UICommon, "ui.common.current_language", "Current")}: {_localization.GetLocaleButtonLabel(locale)}";
    }

    private string BuildStateLabel(bool isOn)
    {
        return isOn
            ? Localize(GameLocalizationTables.UICommon, "ui.common.on", "ON")
            : Localize(GameLocalizationTables.UICommon, "ui.common.off", "OFF");
    }

    private string GetLocaleButtonLabel(string localeCode, string fallback)
    {
        var locale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales?.GetLocale(localeCode);
        return locale != null ? _localization.GetLocaleButtonLabel(locale) : fallback;
    }

    private string BuildPlaybackText(bool isPaused, float playbackSpeed)
    {
        if (_sessionState.IsDirectCombatSandboxLane)
        {
            return isPaused
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.playback.direct_paused", "전투 실험장 | 속도 x{0:0} | 일시정지", playbackSpeed)
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.playback.direct", "전투 실험장 | 속도 x{0:0}", playbackSpeed);
        }

        if (_sessionState.IsQuickBattleSmokeActive)
        {
            return isPaused
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.playback.quick_paused", "빠른 전투 | 속도 x{0:0} | 일시정지", playbackSpeed)
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.playback.quick", "빠른 전투 | 속도 x{0:0}", playbackSpeed);
        }

        return Localize(GameLocalizationTables.UIBattle, "ui.battle.playback.ingame", "원정 전투");
    }

    private string BuildTeamSummary(IEnumerable<BattleUnitReadModel> units)
    {
        // 좌/우 strip의 본분은 '누가 팀에 있나'(얼굴 로스터)다 → 생존 수만 표기한다.
        // 팀 HP 합계는 전황판단(readout) + 유닛별 HP바 + 머리 위 바가 이미 담당하므로
        // strip 헤더에서는 중복 표기를 제거한다. 팀 이름은 인접 타이틀이 보여준다.
        var snapshot = units.ToList();
        if (snapshot.Count == 0)
        {
            return "0/0";
        }

        var alive = snapshot.Count(unit => unit.IsAlive);
        return $"{alive}/{snapshot.Count}";
    }

    private string BuildStatus(BattleSimulationStep step, bool isPaused)
    {
        var pausedSuffix = isPaused
            ? $" | {Localize(GameLocalizationTables.UIBattle, "ui.battle.status.pause_suffix", "Paused")}"
            : string.Empty;
        var pressure = BuildPressureLabel(step);

        if (step.IsFinished)
        {
            var result = step.Winner == TeamSide.Ally
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.result.victory", "Victory")
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.result.defeat", "Defeat");
            return Localize(GameLocalizationTables.UIBattle, "ui.battle.status.finished_summary", "Result | {0} | {1}{2}", result, pressure, pausedSuffix);
        }

        if (BattleReadabilityFormatter.TryResolveStepFocus(step, out var focus))
        {
            var actorName = ResolveUnitDisplayName(step, focus.ActorId, focus.ActorName);
            var targetName = ResolveUnitDisplayName(step, focus.TargetId, focus.TargetName);
            var verb = BattleReadabilityFormatter.BuildSemanticLabel(focus.Semantic, LocaleCode);
            if (focus.IsWindup)
            {
                verb = $"{verb} {UnityEngine.Mathf.RoundToInt(focus.Progress * 100f)}%";
            }

            return Localize(
                GameLocalizationTables.UIBattle,
                "ui.battle.status.step_focus",
                "전황 {0:000} | {1} {2} -> {3} | {4}{5}",
                step.StepIndex,
                actorName,
                verb,
                targetName,
                pressure,
                pausedSuffix);
        }

        return Localize(
            GameLocalizationTables.UIBattle,
            "ui.battle.status.step_only",
            "전황 {0:000} | {1}{2}",
            step.StepIndex,
            pressure,
            pausedSuffix);
    }

    private string BuildPressureLabel(BattleSimulationStep step)
    {
        var diff = BattleReadabilityFormatter.ComputePressureScore(step, TeamSide.Ally);
        if (diff > 0.08f)
        {
            return Localize(GameLocalizationTables.UIBattle, "ui.battle.pressure.allies", "Allies pressing");
        }

        if (diff < -0.08f)
        {
            return Localize(GameLocalizationTables.UIBattle, "ui.battle.pressure.enemies", "Enemies pressing");
        }

        return Localize(GameLocalizationTables.UIBattle, "ui.battle.pressure.even", "Pressure even");
    }

    private string BuildResultText(BattleSimulationStep step, int totalEventCount)
    {
        if (!step.IsFinished)
        {
            return Localize(GameLocalizationTables.UIBattle, "ui.battle.result.in_progress", "Battle in progress");
        }

        // 승패만 낸다.
        //
        // 원래는 "{0} | {1:000} steps | {2} events" 로 스텝 수와 이벤트 수를 붙여서
        // 화면에 <b>"패배 | 185 스텝 | 이벤트 0개"</b> 가 떴다. 스텝 인덱스와 이벤트 카운트는
        // 계측치지 플레이어가 읽을 결과가 아니다. 게다가 패배는 이 게임에서 감정적 정점인데
        // 그 자리에 텔레메트리를 얹으면 정점이 아니라 로그 줄이 된다.
        //
        // 남은 서식 인자를 쓰지 않으므로 totalEventCount 도 더 이상 결과 문구에 관여하지 않는다.
        return step.Winner == TeamSide.Ally
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.result.victory", "Victory")
            : Localize(GameLocalizationTables.UIBattle, "ui.battle.result.defeat", "Defeat");
    }

    private string BuildLogText(
        BattleSimulationStep step,
        IReadOnlyList<BattleEvent> recentLogs,
        IReadOnlyList<string> decisiveTimeline,
        IReadOnlyList<string>? beatCallouts = null)
    {
        if (step.IsFinished && decisiveTimeline.Count > 0)
        {
            return string.Join("\n", decisiveTimeline.TakeLast(MaxVisibleDecisive).Select(line => $"* {line}"));
        }

        // Phase 4 beat 콜아웃 — 디렉터가 밀도 게이트(1초 1건)를 통과시킨 사건만 도착한다.
        // 패널 높이 예산(max-height) 안에 머물도록 콜아웃이 피드 줄을 대체한다(총 줄 수 보존).
        var calloutCount = beatCallouts?.Count ?? 0;
        var visibleLogs = System.Math.Max(1, MaxVisibleLogs - calloutCount);
        var feed = recentLogs.TakeLast(visibleLogs).Select(eventData => BuildLogLine(step, eventData)).ToList();
        if (beatCallouts is { Count: > 0 })
        {
            feed.InsertRange(0, beatCallouts.Select(line => $"★ {line}"));
        }

        return feed.Count == 0
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.feed.empty", "Watching for key moments...")
            : string.Join("\n", feed);
    }

    private string BuildLogLine(BattleSimulationStep step, BattleEvent eventData)
    {
        var source = ResolveUnitDisplayName(step, eventData.ActorId.Value, eventData.ActorName);
        var target = ResolveUnitDisplayName(step, eventData.TargetId?.Value, eventData.TargetName);
        var time = eventData.TimeSeconds;
        return eventData.LogCode switch
        {
            BattleLogCode.BasicAttackDamage => Localize(GameLocalizationTables.UIBattle, "ui.battle.feed.basic_attack", "{0:0.0}s | {1} hit {2} for {3:0}", time, source, target, eventData.Value),
            BattleLogCode.ActiveSkillHeal => Localize(GameLocalizationTables.UIBattle, "ui.battle.feed.heal", "{0:0.0}s | {1} restored {2} for {3:0}", time, source, target, eventData.Value),
            BattleLogCode.ActiveSkillDamage => Localize(GameLocalizationTables.UIBattle, "ui.battle.feed.skill", "{0:0.0}s | {1} used a skill on {2} for {3:0}", time, source, target, eventData.Value),
            BattleLogCode.WaitDefend => Localize(GameLocalizationTables.UIBattle, "ui.battle.feed.guard", "{0:0.0}s | {1} held guard", time, source),
            BattleLogCode.Generic when eventData.EventKind == BattleEventKind.Kill => Localize(GameLocalizationTables.UIBattle, "ui.battle.feed.down", "{0:0.0}s | {1} went down", time, target),
            _ => Localize(GameLocalizationTables.UIBattle, "ui.battle.feed.generic", "{0:0.0}s | {1} {2}", time, source, BattleReadabilityFormatter.BuildShortEventVerb(eventData, LocaleCode))
        };
    }

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _localization.LocalizeOrFallback(table, key, fallback, args);
    }

    private string ResolveUnitDisplayName(BattleUnitReadModel unit)
    {
        if (_contentText != null && !string.IsNullOrWhiteSpace(unit.CharacterId))
        {
            var characterName = _contentText.GetCharacterName(unit.CharacterId, unit.ArchetypeId);
            if (!string.IsNullOrWhiteSpace(characterName))
            {
                return SanitizePlayerFacingText(characterName, unit.Name);
            }
        }

        var name = SanitizePlayerFacingText(
            unit.Name,
            string.IsNullOrWhiteSpace(unit.ArchetypeId) ? unit.Id : unit.ArchetypeId);
        if (!LooksLikeRawLocalizationKey(name))
        {
            return name;
        }

        return _contentText != null && !string.IsNullOrWhiteSpace(unit.ArchetypeId)
            ? SanitizePlayerFacingText(_contentText.GetArchetypeName(unit.ArchetypeId), unit.ArchetypeId)
            : name;
    }

    private string ResolveUnitDisplayName(BattleSimulationStep step, string? unitId, string? fallbackName)
    {
        var unit = step.Units.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(unitId)
            && string.Equals(candidate.Id, unitId, StringComparison.Ordinal));
        if (unit == null && !string.IsNullOrWhiteSpace(fallbackName))
        {
            unit = step.Units.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, fallbackName, StringComparison.Ordinal));
        }

        return unit != null
            ? BuildRosterDisplayName(unit)
            : ToCompactDisplayName(SanitizePlayerFacingText(fallbackName ?? "?", "?"));
    }

    private string SanitizeBattleStateText(string stateText, BattleSimulationStep step)
    {
        if (string.IsNullOrWhiteSpace(stateText))
        {
            return stateText;
        }

        var result = stateText;
        foreach (var unit in step.Units.OrderByDescending(unit => unit.Name?.Length ?? 0))
        {
            if (string.IsNullOrWhiteSpace(unit.Name))
            {
                continue;
            }

            result = result.Replace(unit.Name, ResolveUnitDisplayName(unit), StringComparison.Ordinal);
        }

        return SanitizePlayerFacingText(result, stateText);
    }

    private static string SanitizePlayerFacingText(string value, string fallback = "-")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        if (!LooksLikeRawLocalizationKey(trimmed) && !LooksLikeIdentifierPhrase(trimmed))
        {
            return trimmed;
        }

        var token = ExtractRawKeyLeaf(trimmed);
        return ToTitleCase(BattleReadabilityFormatter.HumanizeToken(token, fallback));
    }

    private static bool LooksLikeRawLocalizationKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith("content.", StringComparison.Ordinal)
               || trimmed.StartsWith("ui.", StringComparison.Ordinal)
               || trimmed.StartsWith("No translation found", StringComparison.OrdinalIgnoreCase)
               || (trimmed.Contains('_', StringComparison.Ordinal) && !trimmed.Contains(' ', StringComparison.Ordinal));
    }

    private static bool LooksLikeIdentifierPhrase(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(ch => ch > 127)
            || !value.Any(char.IsLetter)
            || value.Any(char.IsUpper))
        {
            return false;
        }

        return value.All(ch => char.IsLower(ch) || char.IsDigit(ch) || char.IsWhiteSpace(ch) || ch is '-' or '_');
    }

    private static string ExtractRawKeyLeaf(string value)
    {
        var token = value;
        if (value.Contains('.', StringComparison.Ordinal))
        {
            var parts = value.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = parts.Length - 1; i >= 0; i--)
            {
                if (!string.Equals(parts[i], "name", StringComparison.Ordinal)
                    && !string.Equals(parts[i], "desc", StringComparison.Ordinal))
                {
                    token = parts[i];
                    break;
                }
            }
        }

        foreach (var prefix in new[] { "extra_", "item_", "augment_", "reward_source_", "site_", "encounter_", "character_", "archetype_", "enemy_", "hero_", "skirmish_" })
        {
            if (token.StartsWith(prefix, StringComparison.Ordinal))
            {
                return token[prefix.Length..];
            }
        }

        return token;
    }

    private static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var words = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];
            words[i] = word.Length == 1
                ? word.ToUpperInvariant()
                : char.ToUpperInvariant(word[0]) + word[1..];
        }

        return string.Join(" ", words);
    }

    private string LocaleCode => _localization.CurrentLocale?.Identifier.Code ?? string.Empty;
}
