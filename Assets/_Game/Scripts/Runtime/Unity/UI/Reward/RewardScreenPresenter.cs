using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Meta.Model;
using SM.Meta.Services;

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
            Localize(GameLocalizationTables.UIReward, "ui.reward.panel.summary", "Summary"),
            BuildRunDeltaText(session),
            Localize(GameLocalizationTables.UIReward, "ui.reward.panel.build_context", "Build Impact"),
            BuildBuildContextText(session),
            Localize(GameLocalizationTables.UIReward, "ui.reward.panel.progression", "Progression"),
            Localize(GameLocalizationTables.UIReward, "ui.reward.panel.timeline", "Event Timeline"),
            Localize(GameLocalizationTables.UIReward, "ui.reward.choices.header", "Choose one reward card"),
            BuildChoiceCards(session),
            string.IsNullOrWhiteSpace(message)
                ? defaultStatus
                : message,
            BuildReturnTownLabel(session),
            BuildReturnTownTooltip(session),
            canReturnToTown,
            canReturnToTown,
            BuildSettlementSummaryState(session),
            BuildProgressionRows(session, profile),
            BuildTimelineTicks(session, profile));
    }

    public RewardSettlementSummaryViewState BuildSettlementSummaryState(GameSessionState session)
    {
        return BuildSettlementSummaryStateCore(
            session,
            (key, fallback) => Localize(GameLocalizationTables.UIReward, key, fallback),
            (key, fallback, percent) => Localize(GameLocalizationTables.UIReward, key, fallback, percent));
    }

    internal static RewardSettlementSummaryViewState BuildSettlementSummaryStateForTest(GameSessionState session)
    {
        return BuildSettlementSummaryStateCore(
            session,
            (_, fallback) => fallback,
            (_, fallback, percent) => string.Format(System.Globalization.CultureInfo.InvariantCulture, fallback, percent));
    }

    internal static IReadOnlyList<RewardProgressionRowViewState> BuildProgressionRowsForTest(GameSessionState session, ProfileView profile)
    {
        return BuildProgressionRowsCore(
            session,
            profile,
            token => token.HasValue ? "recorded" : "none",
            BuildContinuationTextForTest);
    }

    internal static IReadOnlyList<RewardTimelineTickViewState> BuildTimelineTicksForTest(GameSessionState session, ProfileView profile)
    {
        return BuildTimelineTicksCore(session, profile);
    }

    private IReadOnlyList<RewardProgressionRowViewState> BuildProgressionRows(GameSessionState session, ProfileView profile)
    {
        return BuildProgressionRowsCore(
            session,
            profile,
            token => token.HasValue ? SanitizePlayerFacingSummary(token.Resolve(_localization, _contentText)) : Localize(GameLocalizationTables.UICommon, "ui.common.none", "None"),
            BuildContinuationText,
            SanitizePlayerFacingSummary);
    }

    private static IReadOnlyList<RewardProgressionRowViewState> BuildProgressionRowsCore(
        GameSessionState session,
        ProfileView profile,
        Func<SessionTextToken, string> resolveToken,
        Func<GameSessionState, string> resolveContinuation,
        Func<string, string>? sanitizeText = null)
    {
        sanitizeText ??= value => value;
        var battleValue = session.LastBattleSummary.HasValue
            ? sanitizeText(resolveToken(session.LastBattleSummary))
            : session.LastBattleVictory ? "Victory" : "Defeat";
        var lootValue = session.LastAutomaticLootBundle == null
            ? "None"
            : sanitizeText(LootResolutionService.FormatSummary(session.LastAutomaticLootBundle));
        var choiceValue = session.LastRewardApplicationSummary.HasValue
            ? sanitizeText(resolveToken(session.LastRewardApplicationSummary))
            : session.PendingRewardChoices.Count == 0 ? "Applied" : "Awaiting choice";

        return new[]
        {
            new RewardProgressionRowViewState("Battle", battleValue, session.LastBattleVictory ? "victory" : "defeat"),
            new RewardProgressionRowViewState("Auto Loot", lootValue, session.LastAutomaticLootBundle == null ? "empty" : "loot"),
            new RewardProgressionRowViewState("Reward Choice", choiceValue, session.PendingRewardChoices.Count == 0 ? "applied" : "pending"),
            new RewardProgressionRowViewState("Wallet", $"{profile.Gold} Gold / {profile.Echo} Echo", "economy"),
            new RewardProgressionRowViewState("Inventory", $"{profile.InventoryCount} items", "inventory"),
            new RewardProgressionRowViewState("Continuation", sanitizeText(resolveContinuation(session)), session.PendingRewardChoices.Count == 0 ? "ready" : "locked"),
        };
    }

    private IReadOnlyList<RewardTimelineTickViewState> BuildTimelineTicks(GameSessionState session, ProfileView profile)
    {
        return BuildTimelineTicksCore(session, profile);
    }

    private static IReadOnlyList<RewardTimelineTickViewState> BuildTimelineTicksCore(GameSessionState session, ProfileView profile)
    {
        var rewardApplied = session.LastRewardApplicationSummary.HasValue || session.PendingRewardChoices.Count == 0;
        return new[]
        {
            new RewardTimelineTickViewState(
                "Battle",
                session.LastBattleSummary.HasValue ? "settlement recorded" : "fallback settlement",
                true,
                false),
            new RewardTimelineTickViewState(
                "Auto Loot",
                session.LastAutomaticLootBundle == null ? "no automatic loot" : "automatic loot bundled",
                session.LastAutomaticLootBundle != null,
                false),
            new RewardTimelineTickViewState(
                "Reward Choice",
                rewardApplied ? "choice applied" : $"{session.PendingRewardChoices.Count} pending choices",
                rewardApplied,
                !rewardApplied),
            new RewardTimelineTickViewState(
                "Return",
                rewardApplied ? $"town return ready · {profile.InventoryCount} inventory" : "locked until reward choice",
                rewardApplied,
                rewardApplied),
        };
    }

    private static RewardSettlementSummaryViewState BuildSettlementSummaryStateCore(
        GameSessionState session,
        System.Func<string, string, string> textResolver,
        System.Func<string, string, int, string> percentResolver)
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

        var siteValue = string.IsNullOrWhiteSpace(overlay.SiteId) ? "-" : overlay.SiteId;
        var stageValue = BuildStageValueText(overlay, textResolver);
        var encounterValue = string.IsNullOrWhiteSpace(overlay.EncounterId) ? "-" : overlay.EncounterId;
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

    private static string BuildStageValueText(RunOverlayState overlay, System.Func<string, string, string> textResolver)
    {
        var chapter = string.IsNullOrWhiteSpace(overlay.ChapterId) ? "-" : overlay.ChapterId;
        var siteNodeIndex = overlay.SiteNodeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var format = textResolver("ui.reward.settlement.stage_value", "{0} / Node {1}");
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, format, chapter, siteNodeIndex);
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

    private IReadOnlyList<RewardChoiceCardViewState> BuildChoiceCards(GameSessionState session)
    {
        var cards = new List<RewardChoiceCardViewState>(3);
        for (var i = 0; i < 3; i++)
        {
            var choice = i < session.PendingRewardChoices.Count ? session.PendingRewardChoices[i] : null;
            cards.Add(new RewardChoiceCardViewState(
                choice == null ? Localize(GameLocalizationTables.UIReward, "ui.reward.choice.empty", "Empty Card") : ResolveChoiceTitle(choice),
                choice == null ? Localize(GameLocalizationTables.UIReward, "ui.reward.choice.none", "No reward choice is available.") : ResolveChoiceDescription(choice),
                choice == null ? "-" : BuildKindText(choice),
                choice == null ? string.Empty : Localize(GameLocalizationTables.UIReward, "ui.reward.choice.build_impact", "Build Impact: {0}", BuildChoiceContextText(choice, session)),
                Localize(GameLocalizationTables.UIReward, "ui.reward.action.choose", "Choose"),
                choice == null ? string.Empty : BuildChoiceTooltip(choice, session),
                choice != null));
        }

        return cards;
    }

    private string BuildRunDeltaText(GameSessionState session)
    {
        var lootSummary = session.LastAutomaticLootBundle == null
            ? Localize(GameLocalizationTables.UICommon, "ui.common.none", "None")
            : SanitizePlayerFacingSummary(LootResolutionService.FormatSummary(session.LastAutomaticLootBundle));
        var continuation = session.PendingRewardChoices.Count == 0
            ? BuildContinuationText(session)
            : Localize(GameLocalizationTables.UIReward, "ui.reward.summary.awaiting_choice", "Choose one reward before returning.");

        var lines = new[]
        {
            Localize(
            GameLocalizationTables.UIReward,
            "ui.reward.summary.settlement_result",
            "Settlement: {0}",
            BuildSettlementHeadline(session)),
            Localize(GameLocalizationTables.UIReward, "ui.reward.summary.auto_loot", "Auto Loot: {0}", lootSummary),
            Localize(GameLocalizationTables.UIReward, "ui.reward.summary.continuation", "Continuation: {0}", continuation),
        }.Select(line => ClampPanelLine(line, 92)).ToList();

        if (session.LastRewardApplicationSummary.HasValue)
        {
            lines.Add(ClampPanelLine(Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.summary.choice_applied",
                "Chosen Reward: {0}",
                SanitizePlayerFacingSummary(session.LastRewardApplicationSummary.Resolve(_localization, _contentText))), 92));
        }

        return string.Join("\n", lines);
    }

    private string BuildBuildContextText(GameSessionState session)
    {
        var equippedPermanentId = GetEquippedPermanentAugmentId(session);
        var benchPermanentIds = session.Profile.UnlockedPermanentAugmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Where(id => !string.Equals(id, equippedPermanentId, StringComparison.Ordinal))
            .Where(id => _root.CombatContentLookup.TryGetAugmentDefinition(id, out var augment) && augment.IsPermanent)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var builder = new StringBuilder();
        builder.AppendLine(ClampPanelLine(Localize(GameLocalizationTables.UIReward, "ui.reward.build.posture", "Posture: {0}", session.SelectedTeamPosture), 82));
        builder.AppendLine(ClampPanelLine(Localize(GameLocalizationTables.UIReward, "ui.reward.build.equipped_permanent", "Equipped Permanent: {0}", FormatAugmentName(equippedPermanentId)), 82));
        return builder.ToString().TrimEnd();
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
            Localize(GameLocalizationTables.UIExpedition, currentNode.LabelKey, currentNode.Id),
            Localize(GameLocalizationTables.UIExpedition, currentNode.PlannedRewardKey, currentNode.Id));
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
        var thesisParts = new List<string> { session.SelectedTeamPosture.ToString() };
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

    private string BuildContinuationText(GameSessionState session)
    {
        if (session.IsQuickBattleSmokeActive)
        {
            return Localize(GameLocalizationTables.UIReward, "ui.reward.continuation.smoke", "빠른 전투를 마치고 마을로 돌아갑니다.");
        }

        return IsFinalExtractSettlement(session)
            ? Localize(GameLocalizationTables.UIReward, "ui.reward.continuation.complete", "Run closes after this return to Town.")
            : Localize(GameLocalizationTables.UIReward, "ui.reward.continuation.resume", "Run stays active and can resume from Town.");
    }

    private static string BuildContinuationTextForTest(GameSessionState session)
    {
        if (session.IsQuickBattleSmokeActive)
        {
            return "빠른 전투를 마치고 마을로 돌아갑니다.";
        }

        return IsFinalExtractSettlement(session)
            ? "Run closes after this return to Town."
            : "Run stays active and can resume from Town.";
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
