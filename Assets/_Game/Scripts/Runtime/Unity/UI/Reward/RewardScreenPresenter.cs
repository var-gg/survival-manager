using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Unity.UI;

namespace SM.Unity.UI.Reward;

public sealed class RewardScreenPresenter
{
    private const string HelpPrefsKey = "SM.Help.Reward";

    private readonly GameSessionRoot _root;
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly RewardScreenView _view;
    private readonly ScreenHelpState _helpState;

    public RewardScreenPresenter(
        GameSessionRoot root,
        GameLocalizationController localization,
        ContentTextResolver contentText,
        RewardScreenView view)
    {
        _root = root;
        _localization = localization;
        _contentText = contentText;
        _view = view;
        _helpState = new ScreenHelpState(HelpPrefsKey);
    }

    public void Initialize()
    {
        _view.Bind(this);
        Refresh();
    }

    public void SelectKorean() => _localization.TrySetLocale("ko");
    public void SelectEnglish() => _localization.TrySetLocale("en");
    public void Choose0() => Choose(0);
    public void Choose1() => Choose(1);
    public void Choose2() => Choose(2);
    public void ToggleHelp()
    {
        _helpState.Toggle();
        Refresh();
    }

    public void DismissHelp()
    {
        _helpState.Dismiss();
        Refresh();
    }

    public event Action<int>? RewardChoiceCommitted;

    public void ReturnToTown()
    {
        if (_root.IsTransientTownSmokeActive)
        {
            var restored = _root.RestoreCanonicalProfileAfterTransientSmoke();
            if (!restored.IsSuccessful)
            {
                Refresh(restored.Message);
                return;
            }
        }
        else
        {
            _root.SessionState.ReturnToTownAfterReward();
            var checkpoint = _root.SaveProfile(SessionCheckpointKind.RewardSettled);
            if (!checkpoint.IsSuccessful)
            {
                Refresh(checkpoint.Message);
                return;
            }
        }

        _root.SceneFlow.ReturnToTown();
    }

    public void Refresh(string message = "")
    {
        _view.Render(BuildState(_root.SessionState, message));
    }

    private void Choose(int index)
    {
        var choice = index >= 0 && index < _root.SessionState.PendingRewardChoices.Count
            ? _root.SessionState.PendingRewardChoices[index]
            : null;
        if (_root.SessionState.ApplyRewardChoice(index))
        {
            var checkpoint = _root.SaveProfile(SessionCheckpointKind.RewardApplied);
            if (checkpoint.Status == SessionCheckpointStatus.Failed)
            {
                Refresh(checkpoint.Message);
                return;
            }

            RewardChoiceCommitted?.Invoke(index);

            Refresh(choice == null
                ? Localize(GameLocalizationTables.UIReward, "ui.reward.status.choice_applied", "Reward applied.")
                : Localize(GameLocalizationTables.UIReward, "ui.reward.status.choice_applied_named", "{0} applied.", ResolveChoiceTitle(choice)));
            return;
        }

        Refresh(Localize(GameLocalizationTables.UIReward, "ui.reward.error.choice_failed", "Failed to apply the selected reward."));
    }

    private RewardScreenViewState BuildState(GameSessionState session, string message)
    {
        var defaultStatus = BuildDefaultStatus(session);
        var canReturnToTown = session.PendingRewardChoices.Count == 0;
        var profile = _root.ProfileQueries.GetProfileView(_root.ActiveProfileId);
        return new RewardScreenViewState(
            Localize(GameLocalizationTables.UIReward, "ui.reward.title", "보상 정산"),
            BuildLocaleStatus(),
            GetLocaleButtonLabel("ko", "한국어"),
            GetLocaleButtonLabel("en", "English"),
            Localize(GameLocalizationTables.UICommon, "ui.common.help", "Help"),
            BuildHelpState(),
            BuildResultHeadline(session),
            BuildCurrencyChips(session),
            BuildPayoffRows(session),
            BuildChoiceCards(session),
            string.IsNullOrWhiteSpace(message)
                ? defaultStatus
                : message,
            BuildReturnTownLabel(session),
            BuildReturnTownTooltip(session),
            canReturnToTown,
            canReturnToTown,
            // wave-28-survivor GPT Pro patch: squad 4명 survivor row — BattleDeployHeroIds 기반.
            BuildSurvivorRows(session, profile));
    }

    /// <summary>
    /// 시안(<c>ui_ux_bible_reward_v1</c>)의 <c>승리 — 늪지 척후소</c> 줄.
    ///
    /// 이전에는 같은 정보가 세 곳에 흩어져 있었다 — 정산 패널의 거점/단계/조우 네 행,
    /// 카드 위의 영문 <c>RESULT</c> 배너, 진행 패널의 <c>승리 / 100 스텝 / 이벤트 0개</c>.
    /// 마지막 것은 결과가 아니라 시뮬레이터 계측이다. 한 줄로 모은다.
    /// </summary>
    private string BuildResultHeadline(GameSessionState session)
    {
        var outcome = session.LastBattleVictory
            ? Localize(GameLocalizationTables.UIReward, "ui.reward.result.victory", "승리")
            : Localize(GameLocalizationTables.UIReward, "ui.reward.result.defeat", "패배");

        var settlement = BuildSettlementSummaryState(session);
        var place = settlement.SiteValueText;
        return string.IsNullOrWhiteSpace(place) || string.Equals(place, "-", StringComparison.Ordinal)
            ? outcome
            : $"{outcome} — {place}";
    }

    /// <summary>
    /// 결과 줄 옆 화폐 칩. 시안의 <c>XP +84 / 골드 +25 / 잔향 +8</c> 자리다.
    ///
    /// 실제 전리품 번들에서만 만든다. 시안의 XP 칩은 <b>넣지 않았다</b> — 현행 시스템은
    /// 전투 단위 경험치 증분을 노출하지 않아서, 그 칩을 채우려면 없는 값을 지어내야 한다.
    /// 아이템은 종류별로 나열하면 칩 줄이 넘치므로 개수 하나로 묶는다.
    /// </summary>
    private IReadOnlyList<RewardCurrencyChipViewState> BuildCurrencyChips(GameSessionState session)
    {
        var bundle = session.LastAutomaticLootBundle;
        if (bundle == null || bundle.Entries.Count == 0)
        {
            return Array.Empty<RewardCurrencyChipViewState>();
        }

        var gold = bundle.Entries.Where(entry => entry.RewardType == RewardType.Gold).Sum(entry => entry.Amount);
        var echo = bundle.Entries.Where(entry => entry.RewardType == RewardType.Echo).Sum(entry => entry.Amount);
        var items = bundle.Entries
            .Where(entry => entry.RewardType != RewardType.Gold && entry.RewardType != RewardType.Echo)
            .Sum(entry => Math.Max(1, entry.Amount));

        var chips = new List<RewardCurrencyChipViewState>();
        if (gold > 0)
        {
            chips.Add(new RewardCurrencyChipViewState(
                Localize(GameLocalizationTables.UIReward, "ui.reward.chip.gold", "골드 +{0}", gold), "gold"));
        }

        if (echo > 0)
        {
            chips.Add(new RewardCurrencyChipViewState(
                Localize(GameLocalizationTables.UIReward, "ui.reward.chip.echo", "잔향 +{0}", echo), "echo"));
        }

        if (items > 0)
        {
            chips.Add(new RewardCurrencyChipViewState(
                Localize(GameLocalizationTables.UIReward, "ui.reward.chip.loot", "전리품 {0}점", items), "loot"));
        }

        return chips;
    }

    /// <summary>
    /// 전과 원장 — 이번 전투가 실제로 만든 것만.
    ///
    /// 이전 "진행" 패널의 여섯 행 중 다섯(전투 스텝 수·자동 전리품·보상 선택 상태·지갑 총액·
    /// 인벤토리 개수·복귀 상태)은 계측이거나 다른 표면의 중복이라 결과 줄·화폐 칩·하단 힌트로
    /// 옮겼다. 여기 남은 셋은 <b>일부러 만든 페이오프 표면</b>이라 지우지 않는다.
    /// </summary>
    private IReadOnlyList<RewardProgressionRowViewState> BuildPayoffRows(GameSessionState session)
    {
        var rows = new List<RewardProgressionRowViewState>();

        // 게임의 중심 카타르시스 — "내 진형이 만든 그림"(MVP·진형 전과·발현)을 전투 직후 빌드를 고르는
        // 이 dwell 화면으로 운반한다. 전투 피드의 transient 3줄과 달리 여기선 다음 빌드 결정과 나란히 읽힌다.
        rows.AddRange(BuildFormationPayoffRows(session));

        // 첫 임시 증강 픽으로 곧 영구 후보가 해금될 예정임을 픽 직후 정산 화면에서 확인시킨다.
        rows.AddRange(BuildPermanentUnlockRows(session));

        // ADR-0028 #1 가독성: 정치 정산 행 — 전투→정치 인과를 같은 화면에서 읽게.
        rows.AddRange(BuildPoliticalRows(session));
        return rows;
    }

    public RewardSettlementSummaryViewState BuildSettlementSummaryState(GameSessionState session)
    {
        var state = BuildSettlementSummaryStateCore(
            session,
            (key, fallback) => Localize(GameLocalizationTables.UIReward, key, fallback),
            (key, fallback, percent) => Localize(GameLocalizationTables.UIReward, key, fallback, percent),
            siteId => SanitizePlayerFacingSummary(_contentText.GetExpeditionSiteName(siteId)),
            chapterId => SanitizePlayerFacingSummary(_contentText.GetCampaignChapterName(chapterId)),
            encounterId => SanitizePlayerFacingSummary(_contentText.GetEncounterName(encounterId)));

        if (state == RewardSettlementSummaryViewState.Empty || string.Equals(state.CommitIdValueText, "-", StringComparison.Ordinal))
        {
            return state;
        }

        return state with
        {
            CommitIdValueText = Localize(GameLocalizationTables.UIReward, "ui.reward.settlement.commit_value", "Recorded")
        };
    }

    internal static RewardSettlementSummaryViewState BuildSettlementSummaryStateForTest(GameSessionState session)
    {
        return BuildSettlementSummaryStateCore(
            session,
            (_, fallback) => fallback,
            (_, fallback, percent) => string.Format(System.Globalization.CultureInfo.InvariantCulture, fallback, percent));
    }

    // wave-33-progression: ProfileHeroView가 Level/Experience/HP를 실데이터로 노출하므로
    // placeholder("체력 회복" / "경험치 +") 대신 ProfileHeroView lookup으로 진짜 progression을 표시.
    // HP가 0/0이면 "데이터 없음" → "체력 만전" fallback (combat resolution이 아직 HP를 기록하지 않은 hero).
    // 진짜 HP delta(battle 직후 surviving HP) wire는 별도 sprint — schema는 본 turn에 준비 완료.
    private IReadOnlyList<RewardSurvivorRowViewState> BuildSurvivorRows(GameSessionState session, ProfileView profile)
    {
        var deployIds = session.BattleDeployHeroIds ?? Array.Empty<string>();
        if (deployIds.Count == 0)
        {
            return Array.Empty<RewardSurvivorRowViewState>();
        }

        var profileHeroById = profile.Heroes.ToDictionary(h => h.HeroId, h => h, StringComparer.Ordinal);
        var victory = session.LastBattleVictory;
        var rows = new List<RewardSurvivorRowViewState>();
        foreach (var heroId in deployIds.Take(4))
        {
            if (!profileHeroById.TryGetValue(heroId, out var hero))
            {
                continue;
            }

            var glyph = ResolveSurvivorGlyph(hero.ClassId);
            var identity = session.Profile.Heroes.FirstOrDefault(record =>
                string.Equals(record.HeroId, hero.HeroId, StringComparison.Ordinal));
            var displayName = HeroDisplayLabelFormatter.ResolvePersonAndJob(
                identity,
                _contentText.GetCharacterName,
                _contentText.GetArchetypeName);

            // HP: 데이터 있으면 "62 / 80" + 실제 ratio, 없으면 victory/retreat fallback 텍스트.
            string hpText;
            float hpPercent;
            if (hero.MaxHp > 0)
            {
                hpText = $"{hero.CurrentHp} / {hero.MaxHp}";
                hpPercent = Math.Clamp((float)hero.CurrentHp / hero.MaxHp, 0f, 1f);
            }
            else
            {
                hpText = victory ? "체력 만전" : "체력 손상";
                hpPercent = victory ? 1f : 0.5f;
            }

            // Lv {L} · {exp}/{threshold}. ExperienceToNextLevel은 ProfileHeroView가 curve에서 계산해 제공.
            var expText = hero.MaxHp > 0
                ? $"Lv {hero.Level} · {hero.Experience} / {hero.ExperienceToNextLevel}"
                : $"Lv {hero.Level}";

            var statusKind = victory ? "victory" : "retreat";
            var statusText = victory ? "생존" : "복귀";

            rows.Add(new RewardSurvivorRowViewState(
                HeroId: hero.HeroId,
                DisplayName: displayName,
                PortraitGlyph: glyph,
                HpText: hpText,
                HpPercent: hpPercent,
                ExpText: expText,
                StatusChipText: statusText,
                StatusChipKind: statusKind));
        }
        return rows;
    }

    private static string ResolveSurvivorGlyph(string? classId)
    {
        if (string.IsNullOrEmpty(classId)) return "◆";
        var key = classId.ToLowerInvariant();
        if (key.Contains("vanguard")) return "◈";
        if (key.Contains("duelist")) return "⚔";
        if (key.Contains("ranger")) return "❖";
        if (key.Contains("mystic")) return "✦";
        return "◆";
    }

    // 픽 직후 pending 영구 해금을 정산 ledger에 surfacing. PendingPermanentUnlockId 는 ApplyRewardChoice 시 세팅돼
    // 이 화면(귀환 전)에 존재하며, 귀환 시 ConsumePendingPermanentUnlock 으로 실제 해금된다.
    private IReadOnlyList<RewardProgressionRowViewState> BuildPermanentUnlockRows(GameSessionState session)
    {
        var pendingUnlockId = session.ActiveRun?.Overlay.PendingPermanentUnlockId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pendingUnlockId))
        {
            return Array.Empty<RewardProgressionRowViewState>();
        }

        var label = Localize(GameLocalizationTables.UIReward, "ui.reward.progression.permanent_unlock", "Permanent Unlock");
        var value = Localize(
            GameLocalizationTables.UIReward,
            "ui.reward.progression.permanent_unlock_pending",
            "{0} · permanent candidate locks on return",
            _contentText.GetAugmentName(pendingUnlockId));
        return BuildPermanentUnlockRowsCore(pendingUnlockId, label, value);
    }

    // 순수 변환 — pending unlock id + 표시 라벨/값을 받아 정산 row로. 테스트는 이 코어를 직접 친다(세션 불요).
    internal static IReadOnlyList<RewardProgressionRowViewState> BuildPermanentUnlockRowsCore(
        string pendingUnlockId,
        string labelText,
        string valueText)
    {
        return string.IsNullOrWhiteSpace(pendingUnlockId)
            ? Array.Empty<RewardProgressionRowViewState>()
            : new[] { new RewardProgressionRowViewState(labelText, valueText, "permanent-unlock") };
    }

    // 직전 전투의 진형 페이오프 carrier(session.LastBattleFormationPayoff)를 보상 ledger 행으로.
    private IReadOnlyList<RewardProgressionRowViewState> BuildFormationPayoffRows(GameSessionState session)
    {
        return BuildFormationPayoffRowsCore(session.LastBattleFormationPayoff);
    }

    // 순수 변환 — payoff carrier 만 받아 보상 row 로. 값이 빈 섹션은 행을 만들지 않는다(평시 무잡음). 테스트는 이 코어를 직접 친다.
    internal static IReadOnlyList<RewardProgressionRowViewState> BuildFormationPayoffRowsCore(BattleFormationPayoffSummary payoff)
    {
        if (payoff == null || !payoff.HasData)
        {
            return Array.Empty<RewardProgressionRowViewState>();
        }

        var rows = new List<RewardProgressionRowViewState>(3);

        var mvp = BattleFormationPayoffFormatter.BuildMvpValue(payoff);
        if (!string.IsNullOrEmpty(mvp))
        {
            rows.Add(new RewardProgressionRowViewState("MVP", mvp, "formation-mvp"));
        }

        var highlight = BattleFormationPayoffFormatter.BuildHighlightValue(payoff);
        if (!string.IsNullOrEmpty(highlight))
        {
            rows.Add(new RewardProgressionRowViewState("진형 전과", highlight, "formation-highlight"));
        }

        var manifest = BattleFormationPayoffFormatter.BuildManifestValue(payoff);
        if (!string.IsNullOrEmpty(manifest))
        {
            rows.Add(new RewardProgressionRowViewState("발현", manifest, "formation-manifest"));
        }

        return rows;
    }

    private IReadOnlyList<RewardProgressionRowViewState> BuildPoliticalRows(GameSessionState session)
    {
        return BuildPoliticalRowsCore(
            session.LastPoliticalSettlement,
            ResolvePoliticalFactionName,
            WarrantDisplayDefaults.SettlementReasonText,
            factionId => ResolveCurrentStanding(session, factionId));
    }

    // 순수 변환 — report + 표시명/standing 해석기를 받아 정치 정산 row로. 테스트는 이 코어를 직접 친다(세션 불요).
    internal static IReadOnlyList<RewardProgressionRowViewState> BuildPoliticalRowsCore(
        PoliticalSettlementReport report,
        Func<string, string> factionName,
        Func<PoliticalSettlementReason, string> reasonText,
        Func<string, int> standingLookup)
    {
        if (report == null || !report.HasPolitics)
        {
            return Array.Empty<RewardProgressionRowViewState>();
        }

        var rows = new List<RewardProgressionRowViewState>(report.Lines.Count);
        foreach (var line in report.Lines)
        {
            var sign = line.Delta.ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture);
            var resulting = standingLookup(line.FactionId);
            var value = $"{reasonText(line.Reason)} · 신뢰 {sign} (현재 {resulting})";
            rows.Add(new RewardProgressionRowViewState(
                factionName(line.FactionId),
                value,
                line.Delta >= 0 ? "politics-gain" : "politics-loss"));
        }

        return rows;
    }

    private static int ResolveCurrentStanding(GameSessionState session, string factionId)
    {
        return session.Profile.FactionStanding
            .FirstOrDefault(standing => string.Equals(standing.FactionId, factionId, StringComparison.Ordinal))?.Trust ?? 0;
    }

    private string ResolvePoliticalFactionName(string factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return string.Empty;
        }

        var name = _contentText.GetFactionName(factionId);
        return string.IsNullOrWhiteSpace(name) || name.StartsWith("content.", StringComparison.Ordinal)
            ? HumanizeIdentifier(factionId)
            : name;
    }

    // Summary 패널 1줄 headline — 가장 많이 읽히는 위치에 "어느 세력에 한 약속을 지켰나/어겼나"를 박는다.
    private string BuildPoliticalHeadline(GameSessionState session)
    {
        var report = session.LastPoliticalSettlement;
        if (report == null || !report.HasPolitics || report.IssuerLine is not { } issuer)
        {
            return string.Empty;
        }

        var sign = issuer.Delta.ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture);
        return Localize(
            GameLocalizationTables.UIReward,
            "ui.reward.summary.political",
            "정치: {0} {1} (신뢰 {2})",
            ResolvePoliticalFactionName(issuer.FactionId),
            WarrantDisplayDefaults.SettlementReasonText(issuer.Reason),
            sign);
    }

    private static string LocalizeProgressionValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value
            .Replace("Victory", "승리", StringComparison.Ordinal)
            .Replace("Defeat", "패배", StringComparison.Ordinal)
            .Replace("None", "없음", StringComparison.Ordinal)
            .Replace("Applied", "적용됨", StringComparison.Ordinal)
            .Replace("Awaiting choice", "선택 대기", StringComparison.Ordinal)
            .Replace("Gold", "골드", StringComparison.Ordinal)
            .Replace("Echo", "잔향", StringComparison.Ordinal)
            .Replace("items", "개", StringComparison.Ordinal)
            .Replace("Run stays active and can resume from Town.", "원정은 유지되고 마을에서 재개할 수 있습니다.", StringComparison.Ordinal)
            .Replace("Run closes after this return to Town.", "마을로 돌아가면 원정이 종료됩니다.", StringComparison.Ordinal);
    }

    private static string LocalizeTimelineDetail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.EndsWith(" pending choices", StringComparison.Ordinal))
        {
            return value.Replace(" pending choices", "개 선택 대기", StringComparison.Ordinal);
        }

        if (value.StartsWith("town return ready", StringComparison.Ordinal))
        {
            var count = value.Split(' ').FirstOrDefault(part => int.TryParse(part, out _));
            return string.IsNullOrWhiteSpace(count)
                ? "마을 복귀 준비"
                : $"마을 복귀 준비 · 인벤토리 {count}개";
        }

        return value switch
        {
            "settlement recorded" => "정산 기록됨",
            "fallback settlement" => "대체 정산",
            "no automatic loot" => "자동 전리품 없음",
            "automatic loot bundled" => "자동 전리품 묶음",
            "choice applied" => "선택 적용됨",
            "locked until reward choice" => "보상 선택 전 잠김",
            _ => value,
        };
    }

    private static RewardSettlementSummaryViewState BuildSettlementSummaryStateCore(
        GameSessionState session,
        System.Func<string, string, string> textResolver,
        System.Func<string, string, int, string> percentResolver,
        System.Func<string, string>? siteNameResolver = null,
        System.Func<string, string>? chapterNameResolver = null,
        System.Func<string, string>? encounterNameResolver = null)
    {
        var overlay = session?.ActiveRun?.Overlay;
        if (overlay == null)
        {
            return RewardSettlementSummaryViewState.Empty;
        }

        var titleText = textResolver("ui.reward.settlement.title", "Settlement");
        var siteKey = textResolver("ui.reward.settlement.site_key", "Site");
        var stageKey = textResolver("ui.reward.settlement.stage_key", "Stage");
        var encounterKey = textResolver("ui.reward.settlement.encounter_key", "Encounter");
        var commitIdKey = textResolver("ui.reward.settlement.commit_key", "Commit");

        var siteValue = ResolveTraceName(overlay.SiteId, siteNameResolver);
        var stageValue = BuildStageValueText(overlay, textResolver, chapterNameResolver);
        var encounterValue = ResolveTraceName(overlay.EncounterId, encounterNameResolver);
        var commitIdValue = BuildCommitIdValueText(overlay.RewardCommitId);

        var modifierPayload = session!.AtlasExpeditionModifierPayload;
        var rewardBiasChip = BuildModifierChipText(
            "ui.reward.settlement.chip.reward_bias",
            "Reward Bias +{0}%",
            modifierPayload?.RewardBiasPercent ?? 0,
            percentResolver);
        var threatPressureChip = BuildModifierChipText(
            "ui.reward.settlement.chip.threat_pressure",
            "Threat Pressure +{0}%",
            modifierPayload?.ThreatPressurePercent ?? 0,
            percentResolver);
        var affinityBoostChip = BuildModifierChipText(
            "ui.reward.settlement.chip.affinity_boost",
            "Affinity Boost +{0}%",
            modifierPayload?.AffinityBoostPercent ?? 0,
            percentResolver);
        var hasAnyModifier = modifierPayload?.HasAnyModifier ?? false;

        // task-atlas-modifier-application-v1 acceptance #5: ThreatPressurePercent를 ComputeThreatBand로
        // band label로 매핑해 chip 값과 band 요약을 같은 surface에 일관 표시.
        // 본 baseline은 한국어 hardcoded — 영문 localization key 등록은 후속 turn에서 SharedData asset
        // sync와 함께 처리 (UiLocalizationAuditTests의 SharedStringTables_Cover_RuntimeUiKeys 정합 유지).
        var threatBand = AtlasModifierApplicationService.ComputeThreatBand(modifierPayload?.ThreatPressurePercent ?? 0);
        var threatBandLabel = threatBand switch
        {
            AtlasThreatBand.Elevated => "위협 고조",
            AtlasThreatBand.High => "위협 과중",
            AtlasThreatBand.Severe => "위협 극단",
            _ => string.Empty,
        };

        return new RewardSettlementSummaryViewState(
            TitleText: titleText,
            SiteKeyText: siteKey,
            SiteValueText: siteValue,
            StageKeyText: stageKey,
            StageValueText: stageValue,
            EncounterKeyText: encounterKey,
            EncounterValueText: encounterValue,
            CommitIdKeyText: commitIdKey,
            CommitIdValueText: commitIdValue,
            RewardBiasChipText: rewardBiasChip,
            ThreatPressureChipText: threatPressureChip,
            AffinityBoostChipText: affinityBoostChip,
            HasAnyModifier: hasAnyModifier,
            ThreatBandLabelText: threatBandLabel);
    }

    private static string BuildStageValueText(
        RunOverlayState overlay,
        System.Func<string, string, string> textResolver,
        System.Func<string, string>? chapterNameResolver = null)
    {
        var chapter = ResolveTraceName(overlay.ChapterId, chapterNameResolver);
        var siteNodeIndex = overlay.SiteNodeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var format = textResolver("ui.reward.settlement.stage_value", "{0} / Node {1}");
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, format, chapter, siteNodeIndex);
    }

    private static string ResolveTraceName(string id, System.Func<string, string>? resolver)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "-";
        }

        var resolved = resolver?.Invoke(id) ?? id;
        return string.IsNullOrWhiteSpace(resolved) ? "-" : resolved;
    }

    private static string BuildCommitIdValueText(string rewardCommitId)
    {
        if (string.IsNullOrWhiteSpace(rewardCommitId))
        {
            return "-";
        }

        var trimmed = rewardCommitId.Trim();
        return trimmed.Length <= 12 ? trimmed : trimmed.Substring(0, 12);
    }

    private static string BuildModifierChipText(
        string key,
        string fallbackFormat,
        int percent,
        System.Func<string, string, int, string> percentResolver)
    {
        if (percent <= 0)
        {
            return string.Empty;
        }

        return percentResolver(key, fallbackFormat, percent);
    }

    /// <summary>
    /// 실제 선택지만 카드로 만든다.
    ///
    /// 이전에는 항상 세 장을 만들고 남는 자리를 <c>빈 카드 / 선택지가 없습니다 / -</c> 로 채웠다.
    /// 패배 회수 레인처럼 선택지가 한 장뿐일 때 <b>금테 두른 빈 액자 두 개</b>가 나란히 섰고,
    /// 그건 "선택지가 없다"가 아니라 <b>"로딩에 실패했다"</b>로 읽힌다. 없는 카드는 안 그린다
    /// (<see cref="RewardScreenView"/> 가 남는 슬롯을 감춘다).
    /// </summary>
    private IReadOnlyList<RewardChoiceCardViewState> BuildChoiceCards(GameSessionState session)
    {
        var cards = new List<RewardChoiceCardViewState>(3);
        foreach (var choice in session.PendingRewardChoices.Take(3))
        {
            cards.Add(new RewardChoiceCardViewState(
                ResolveChoiceTitle(choice),
                ResolveChoiceDescription(choice),
                BuildKindText(choice),
                Localize(GameLocalizationTables.UIReward, "ui.reward.choice.build_impact", "Build Impact: {0}", BuildChoiceContextText(choice, session)),
                Localize(GameLocalizationTables.UIReward, "ui.reward.action.choose", "Choose"),
                BuildChoiceTooltip(choice, session),
                true));
        }

        return cards;
    }

    private string ResolveLootEntryName(LootEntry entry)
    {
        return entry.RewardType switch
        {
            RewardType.Item => _contentText.GetItemName(entry.Id),
            RewardType.TemporaryAugment => _contentText.GetAugmentName(entry.Id),
            RewardType.Gold => Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.loot.gold",
                "Gold"),
            RewardType.Echo => Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.loot.echo",
                "Echo"),
            RewardType.EmberDust => Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.loot.ember_dust",
                "Ember Dust"),
            RewardType.EchoCrystal => Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.loot.echo_crystal",
                "Echo Crystal"),
            RewardType.BossSigil => Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.loot.boss_sigil",
                "Boss Sigil"),
            RewardType.TraitLockToken => Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.loot.trait_lock",
                "Trait Lock Token"),
            RewardType.TraitPurgeToken => Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.loot.trait_purge",
                "Trait Purge Token"),
            RewardType.SkillManual => Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.loot.skill_manual",
                "Skill Manual"),
            RewardType.SkillShard => Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.loot.skill_shard",
                "Skill Shard"),
            _ => Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.loot.unknown",
                "Unknown reward"),
        };
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

    private bool IsKoreanLocale => string.Equals(_localization.CurrentLocale?.Identifier.Code, "ko", StringComparison.OrdinalIgnoreCase);

    private string GetLocaleButtonLabel(string localeCode, string fallback)
    {
        var locale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales?.GetLocale(localeCode);
        if (locale != null)
        {
            return _localization.GetLocaleButtonLabel(locale);
        }

        return fallback;
    }

    private HelpStripViewState BuildHelpState()
    {
        return new HelpStripViewState(
            _helpState.IsVisible,
            Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.help.body",
                "Pick one reward to apply it immediately, then return to Town to resume or close the run."),
            Localize(GameLocalizationTables.UICommon, "ui.common.hide", "Hide"));
    }

    private string BuildReturnTownTooltip(GameSessionState session)
    {
        return session.PendingRewardChoices.Count > 0
            ? Localize(GameLocalizationTables.UIReward, "ui.reward.tooltip.return_locked", "Choose one reward first. The summary will keep the applied delta on screen.")
            : Localize(GameLocalizationTables.UIReward, "ui.reward.tooltip.return_ready", "Return to Town with the applied reward result and continuation state.");
    }

    private string BuildKindText(RewardChoiceViewModel choice)
    {
        return choice.Kind switch
        {
            RewardChoiceKind.Gold => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.gold", "Gold +{0}", choice.GoldAmount),
            RewardChoiceKind.Item => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.item", "Item / {0}", _contentText.GetItemName(choice.PayloadId)),
            RewardChoiceKind.TemporaryAugment => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.temp_augment", "Temp / {0}", _contentText.GetAugmentName(choice.PayloadId)),
            RewardChoiceKind.Echo => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.echo", "Echo +{0}", choice.EchoAmount),
            RewardChoiceKind.PermanentAugmentSlot => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.permanent_slot", "Legacy Slot Reward"),
            _ => choice.Kind.ToString()
        };
    }

    private string BuildChoiceContextText(RewardChoiceViewModel choice, GameSessionState session)
    {
        return choice.Kind switch
        {
            RewardChoiceKind.Gold => Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.gold", "Economy rail: recruit and refresh."),
            RewardChoiceKind.Echo => Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.echo", "Economy rail: scout, retrain, refit, and recovery."),
            RewardChoiceKind.Item => BuildItemChoiceContext(choice.PayloadId),
            RewardChoiceKind.TemporaryAugment => BuildTemporaryAugmentChoiceContext(choice.PayloadId, session),
            RewardChoiceKind.PermanentAugmentSlot => Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.permanent_slot", "Normal lane does not generate permanent slot rewards."),
            _ => string.Empty,
        };
    }

    private string BuildChoiceTooltip(RewardChoiceViewModel choice, GameSessionState session)
    {
        return Localize(
            GameLocalizationTables.UIReward,
            "ui.reward.tooltip.choice",
            "{0}. {1}",
            BuildKindText(choice),
            BuildChoiceContextText(choice, session));
    }

    private string BuildDefaultStatus(GameSessionState session)
    {
        if (session.LastRewardApplicationSummary.HasValue && session.PendingRewardChoices.Count == 0)
        {
            return Localize(GameLocalizationTables.UIReward, "ui.reward.status.default.return_town", "Reward locked in. Return to Town to continue.");
        }

        if (session.IsQuickBattleSmokeActive)
        {
            return Localize(GameLocalizationTables.UIReward, "ui.reward.status.default.quick", "빠른 전투 정산입니다. 보상 카드 한 장을 고르고 마을로 돌아가세요.");
        }

        if (!session.LastBattleVictory)
        {
            return Localize(GameLocalizationTables.UIReward, "ui.reward.status.default.defeat", "Run failed. Pick a fallback reward and return to Town.");
        }

        return IsFinalExtractSettlement(session)
            ? Localize(GameLocalizationTables.UIReward, "ui.reward.status.default.complete", "Run complete. Pick one reward and return to Town.")
            : Localize(GameLocalizationTables.UIReward, "ui.reward.status.default.resume", "Pick one reward and return to Town. You can resume the expedition later.");
    }

    private string BuildReturnTownLabel(GameSessionState session)
    {
        if (session.IsQuickBattleSmokeActive)
        {
            return Localize(GameLocalizationTables.UIReward, "ui.reward.action.return_town_smoke", "마을로 복귀 / 빠른 전투 완료");
        }

        if (!session.LastBattleVictory)
        {
            return Localize(GameLocalizationTables.UIReward, "ui.reward.action.return_town_failed", "Return to Town / Run Failed");
        }

        return IsFinalExtractSettlement(session)
            ? Localize(GameLocalizationTables.UIReward, "ui.reward.action.return_town_complete", "Return to Town / Run Complete")
            : Localize(GameLocalizationTables.UIReward, "ui.reward.action.return_town_resume", "Return to Town / Resume Later");
    }

    private string BuildSettlementHeadline(GameSessionState session)
    {
        if (session.IsQuickBattleSmokeActive)
        {
            return Localize(GameLocalizationTables.UIReward, "ui.reward.result.quick_smoke", "빠른 전투");
        }

        if (!session.LastBattleVictory)
        {
            return Localize(GameLocalizationTables.UIReward, "ui.reward.result.defeat", "Defeat");
        }

        return IsFinalExtractSettlement(session)
            ? Localize(GameLocalizationTables.UIReward, "ui.reward.result.run_complete", "Final Extract")
            : Localize(GameLocalizationTables.UIReward, "ui.reward.result.victory", "Victory");
    }

    private string BuildFallbackSummary(GameSessionState session)
    {
        var currentNode = session.GetCurrentExpeditionNode();
        if (currentNode == null)
        {
            return Localize(GameLocalizationTables.UIReward, "ui.reward.summary.none", "Settlement summary is unavailable.");
        }

        return Localize(
            GameLocalizationTables.UIReward,
            "ui.reward.summary.route_only",
            "Route: {0} / {1}",
            ResolveExpeditionNodeName(currentNode),
            ResolveNodeRewardName(currentNode));
    }

    private string ResolveExpeditionNodeName(ExpeditionNodeViewModel node)
    {
        if (node.RequiresBattle)
        {
            return _contentText.GetEncounterName(node.Id);
        }

        if (node.LabelKey.StartsWith("content.site_event.", StringComparison.Ordinal))
        {
            return Localize(
                ContentLocalizationTables.Campaign,
                node.LabelKey,
                Localize(
                    GameLocalizationTables.UICommon,
                    "ui.common.unknown_event",
                    "Unknown event"));
        }

        if (node.LabelKey.StartsWith("ui.", StringComparison.Ordinal))
        {
            return Localize(
                GameLocalizationTables.UIExpedition,
                node.LabelKey,
                Localize(
                    GameLocalizationTables.UICommon,
                    "ui.common.unknown_route",
                    "Unknown route"));
        }

        return Localize(
            GameLocalizationTables.UICommon,
            "ui.common.unknown_route",
            "Unknown route");
    }

    private string ResolveNodeRewardName(ExpeditionNodeViewModel node)
    {
        if (!string.IsNullOrWhiteSpace(node.RewardSourceId))
        {
            return _contentText.GetRewardSourceName(node.RewardSourceId);
        }

        if (node.PlannedRewardKey.StartsWith("ui.", StringComparison.Ordinal))
        {
            return Localize(
                GameLocalizationTables.UIExpedition,
                node.PlannedRewardKey,
                Localize(
                    GameLocalizationTables.UICommon,
                    "ui.common.unknown_reward_source",
                    "Unknown reward source"));
        }

        return Localize(
            GameLocalizationTables.UICommon,
            "ui.common.unknown_reward_source",
            "Unknown reward source");
    }

    private static bool IsFinalExtractSettlement(GameSessionState session)
    {
        var currentNode = session.GetCurrentExpeditionNode();
        return currentNode != null
            && !currentNode.RequiresBattle
            && string.Equals(currentNode.Id, $"{session.SelectedCampaignSiteId}:extract", System.StringComparison.Ordinal);
    }

    private string BuildItemChoiceContext(string itemId)
    {
        if (!_root.CombatContentLookup.TryGetItemDefinition(itemId, out var item))
        {
            return Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.item.default", "Hero hook: inventory-ready permanent item.");
        }

        return item.SlotType switch
        {
            SM.Content.Definitions.ItemSlotType.Weapon => Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.item.weapon", "Hero hook: offensive or rule-changing weapon line."),
            SM.Content.Definitions.ItemSlotType.Armor => Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.item.armor", "Hero hook: frontline durability or protection line."),
            SM.Content.Definitions.ItemSlotType.Accessory => Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.item.accessory", "Hero hook: utility or sustain accessory line."),
            _ => Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.item.default", "Hero hook: inventory-ready permanent item."),
        };
    }

    private string BuildTemporaryAugmentChoiceContext(string augmentId, GameSessionState session)
    {
        var builder = new StringBuilder();
        builder.Append(BuildAugmentSupportText(augmentId));
        var previewUnlockId = session.PreviewPermanentUnlockFromTemporaryAugment(augmentId);
        if (!string.IsNullOrWhiteSpace(previewUnlockId))
        {
            builder.Append(Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.build_impact.temp_unlock",
                " / First temp pick unlocks {0}",
                _contentText.GetAugmentName(previewUnlockId)));
        }
        else if (!string.IsNullOrWhiteSpace(session.ActiveRun?.Overlay.FirstSelectedTemporaryAugmentId))
        {
            builder.Append(Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.temp_fixed", " / Permanent unlock already fixed for this run"));
        }

        return builder.ToString();
    }

    private string BuildAugmentSupportText(string augmentId)
    {
        if (!_root.CombatContentLookup.TryGetAugmentDefinition(augmentId, out var augment))
        {
            return Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.augment.default", "Run hook: temporary tactical spike.");
        }

        return augment.FamilyId switch
        {
            "hunt_line" => Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.augment.hunt", "Run hook: supports front-line pressure and finishing."),
            "ward_line" => Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.augment.ward", "Run hook: supports sustain and protection pivots."),
            "tempo_drive" => Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.augment.tempo", "Run hook: supports tempo and snowball lines."),
            "hex_line" => Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.augment.hex", "Run hook: supports control and attrition lines."),
            _ => augment.IsPermanent
                ? Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.augment.permanent", "Build hook: permanent thesis choice.")
                : Localize(GameLocalizationTables.UIReward, "ui.reward.build_impact.augment.default", "Run hook: temporary tactical spike."),
        };
    }

    private string BuildThesisLine(GameSessionState session, string equippedPermanentId)
    {
        var thesisParts = new List<string>
        {
            TeamPostureText.Resolve(_localization, session.SelectedTeamPosture)
        };
        if (!string.IsNullOrWhiteSpace(equippedPermanentId)
            && _root.CombatContentLookup.TryGetAugmentDefinition(equippedPermanentId, out var augment))
        {
            thesisParts.Add(augment.FamilyId switch
            {
                "hunt_line" => Localize(GameLocalizationTables.UIReward, "ui.reward.build.thesis.hunt", "Frontline pressure"),
                "ward_line" => Localize(GameLocalizationTables.UIReward, "ui.reward.build.thesis.ward", "Sustain pivot"),
                "tempo_drive" => Localize(GameLocalizationTables.UIReward, "ui.reward.build.thesis.tempo", "Tempo snowball"),
                "hex_line" => Localize(GameLocalizationTables.UIReward, "ui.reward.build.thesis.hex", "Control attrition"),
                _ => _contentText.GetAugmentName(equippedPermanentId),
            });
        }
        else
        {
            thesisParts.Add(Localize(GameLocalizationTables.UIReward, "ui.reward.build.no_permanent", "No permanent thesis"));
        }

        var tempCount = session.Expedition.TemporaryAugmentIds.Count;
        thesisParts.Add(tempCount == 0
            ? Localize(GameLocalizationTables.UIReward, "ui.reward.build.no_temp", "No temp overlay yet")
            : Localize(GameLocalizationTables.UIReward, "ui.reward.build.temp_count", "{0} temp overlay", tempCount));
        return string.Join(" / ", thesisParts);
    }

    private string GetEquippedPermanentAugmentId(GameSessionState session)
    {
        return session.Profile.PermanentAugmentLoadouts
            .FirstOrDefault(record => string.Equals(record.BlueprintId, session.Profile.ActiveBlueprintId, StringComparison.Ordinal))
            ?.EquippedAugmentIds.FirstOrDefault() ?? string.Empty;
    }

    private string FormatAugmentName(string augmentId)
        => string.IsNullOrWhiteSpace(augmentId)
            ? Localize(GameLocalizationTables.UICommon, "ui.common.none", "None")
            : _contentText.GetAugmentName(augmentId);

    private string FormatAugmentList(IEnumerable<string> augmentIds)
    {
        var names = augmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(FormatAugmentName)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return names.Count == 0
            ? Localize(GameLocalizationTables.UICommon, "ui.common.none", "None")
            : string.Join(", ", names);
    }

    private string ResolveChoiceTitle(RewardChoiceViewModel choice) => Localize(GameLocalizationTables.UIReward, choice.TitleKey, BuildChoiceFallbackTitle(choice));
    private string ResolveChoiceDescription(RewardChoiceViewModel choice) => Localize(GameLocalizationTables.UIReward, choice.DescriptionKey, BuildChoiceFallbackDescription(choice));
    private string Localize(string table, string key, string fallback, params object[] args) => _localization.LocalizeOrFallback(table, key, fallback, args);

    private string BuildChoiceFallbackTitle(RewardChoiceViewModel choice)
    {
        return choice.Kind switch
        {
            RewardChoiceKind.Gold => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.gold", "Gold +{0}", choice.GoldAmount),
            RewardChoiceKind.Echo => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.echo", "Echo +{0}", choice.EchoAmount),
            RewardChoiceKind.Item => _contentText.GetItemName(choice.PayloadId),
            RewardChoiceKind.TemporaryAugment => _contentText.GetAugmentName(choice.PayloadId),
            RewardChoiceKind.PermanentAugmentSlot => Localize(GameLocalizationTables.UIReward, "ui.reward.kind.permanent_slot", "Legacy Slot Reward"),
            _ => HumanizeIdentifier(choice.PayloadId),
        };
    }

    private string BuildChoiceFallbackDescription(RewardChoiceViewModel choice)
    {
        return choice.Kind switch
        {
            RewardChoiceKind.Gold => Localize(GameLocalizationTables.UIReward, "ui.reward.choice.gold_fallback", "Immediate gold for recruit and service costs."),
            RewardChoiceKind.Echo => Localize(GameLocalizationTables.UIReward, "ui.reward.choice.echo_fallback", "Echo reserve for scouting and recovery."),
            RewardChoiceKind.Item => Localize(GameLocalizationTables.UIReward, "ui.reward.choice.item_fallback", "Equipment candidate: {0}.", _contentText.GetItemName(choice.PayloadId)),
            RewardChoiceKind.TemporaryAugment => Localize(GameLocalizationTables.UIReward, "ui.reward.choice.augment_fallback", "Temporary run thesis: {0}.", _contentText.GetAugmentName(choice.PayloadId)),
            RewardChoiceKind.PermanentAugmentSlot => Localize(GameLocalizationTables.UIReward, "ui.reward.choice.permanent_slot_fallback", "Permanent build slot reward."),
            _ => HumanizeIdentifier(choice.PayloadId),
        };
    }

    private static string SanitizePlayerFacingSummary(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var separators = new[] { ' ', ',', '\n', '\r', '\t', '/', ':', ';', '(', ')', '[', ']' };
        var tokens = value.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        var result = value;
        foreach (var token in tokens.Where(LooksLikeIdentifierToken).Distinct(StringComparer.Ordinal).OrderByDescending(token => token.Length))
        {
            result = result.Replace(token, HumanizeIdentifier(token), StringComparison.Ordinal);
        }

        return result;
    }

    private static string ClampPanelLine(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var collapsed = string.Join(" ", value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= maxLength ? collapsed : $"{collapsed[..Math.Max(1, maxLength - 1)]}…";
    }

    private static bool LooksLikeIdentifierToken(string value)
    {
        return value.Contains('_', StringComparison.Ordinal)
               || value.StartsWith("reward.", StringComparison.Ordinal)
               || value.StartsWith("content.", StringComparison.Ordinal)
               || value.StartsWith("ui.", StringComparison.Ordinal);
    }

    private static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var token = value.Trim();
        if (token.Contains('.', StringComparison.Ordinal))
        {
            var parts = token.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
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

        foreach (var prefix in new[] { "item_", "augment_", "reward_source_", "site_", "skirmish_", "ember_", "gold_" })
        {
            if (token.StartsWith(prefix, StringComparison.Ordinal))
            {
                token = token[prefix.Length..];
                break;
            }
        }

        var words = token.Replace('_', ' ').Replace('-', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0
            ? value
            : string.Join(" ", words.Select(word => char.ToUpperInvariant(word[0]) + (word.Length > 1 ? word[1..] : string.Empty)));
    }
}
