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
        return new RewardScreenViewState(
            Localize(GameLocalizationTables.UIReward, "ui.reward.title", "Reward Operator UI"),
            BuildLocaleStatus(),
            GetLocaleButtonLabel("ko", "한국어"),
            GetLocaleButtonLabel("en", "English"),
            Localize(GameLocalizationTables.UICommon, "ui.common.help", "Help"),
            BuildHelpState(),
            Localize(GameLocalizationTables.UIReward, "ui.reward.panel.summary", "Summary"),
            BuildRunDeltaText(session),
            Localize(GameLocalizationTables.UIReward, "ui.reward.panel.build_context", "Build Impact"),
            BuildBuildContextText(session),
            Localize(GameLocalizationTables.UIReward, "ui.reward.choices.header", "Choose one reward card"),
            BuildChoiceCards(session),
            string.IsNullOrWhiteSpace(message)
                ? defaultStatus
                : message,
            BuildReturnTownLabel(session),
            BuildReturnTownTooltip(session),
            canReturnToTown,
            canReturnToTown);
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
        var sb = new StringBuilder();
        sb.AppendLine(Localize(
            GameLocalizationTables.UIReward,
            "ui.reward.summary.settlement_result",
            "Settlement: {0}",
            BuildSettlementHeadline(session)));
        sb.AppendLine(Localize(
            GameLocalizationTables.UIReward,
            "ui.reward.summary.base_reward",
            "Base Reward: {0}",
            session.LastBattleSummary.HasValue
                ? session.LastBattleSummary.Resolve(_localization, _contentText)
                : BuildFallbackSummary(session)));
        sb.AppendLine(Localize(
            GameLocalizationTables.UIReward,
            "ui.reward.summary.auto_loot",
            "Auto Loot: {0}",
            session.LastAutomaticLootBundle == null ? Localize(GameLocalizationTables.UICommon, "ui.common.none", "None") : LootResolutionService.FormatSummary(session.LastAutomaticLootBundle)));
        if (session.LastRewardApplicationSummary.HasValue)
        {
            sb.AppendLine(Localize(
                GameLocalizationTables.UIReward,
                "ui.reward.summary.choice_applied",
                "Chosen Reward: {0}",
                session.LastRewardApplicationSummary.Resolve(_localization, _contentText)));
        }

        var firstTempId = session.ActiveRun?.Overlay.FirstSelectedTemporaryAugmentId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(firstTempId))
        {
            sb.AppendLine($"First Temp Thesis: {_contentText.GetAugmentName(firstTempId)}");
        }

        var pendingUnlockId = session.ActiveRun?.Overlay.PendingPermanentUnlockId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(pendingUnlockId))
        {
            sb.AppendLine(Localize(GameLocalizationTables.UIReward, "ui.reward.summary.permanent_unlock", "Permanent Candidate Pending: {0}", _contentText.GetAugmentName(pendingUnlockId)));
        }
        else if (session.LastPermanentUnlockSummary.HasValue)
        {
            sb.AppendLine(session.LastPermanentUnlockSummary.Resolve(_localization, _contentText));
        }

        var profile = _root.ProfileQueries.GetProfileView(_root.ActiveProfileId);
        sb.AppendLine(Localize(GameLocalizationTables.UIReward, "ui.reward.summary.wallet", "Wallet Now: {0} Gold / {1} Echo", profile.Gold, profile.Echo));
        sb.AppendLine(Localize(GameLocalizationTables.UIReward, "ui.reward.summary.inventory_now", "Inventory Now: {0} items", profile.InventoryCount));
        sb.AppendLine(Localize(
            GameLocalizationTables.UIReward,
            "ui.reward.summary.continuation",
            "Continuation: {0}",
            session.PendingRewardChoices.Count == 0
                ? BuildContinuationText(session)
                : Localize(GameLocalizationTables.UIReward, "ui.reward.summary.awaiting_choice", "Choose one reward before returning.")));
        return sb.ToString();
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
        builder.AppendLine(Localize(GameLocalizationTables.UIReward, "ui.reward.build.posture", "Posture: {0}", session.SelectedTeamPosture));
        builder.AppendLine(Localize(GameLocalizationTables.UIReward, "ui.reward.build.equipped_permanent", "Equipped Permanent: {0}", FormatAugmentName(equippedPermanentId)));
        builder.AppendLine(Localize(GameLocalizationTables.UIReward, "ui.reward.build.bench", "Bench Candidates: {0}", FormatAugmentList(benchPermanentIds)));
        builder.AppendLine(Localize(GameLocalizationTables.UIReward, "ui.reward.build.temp_augments", "Current Temp Augments: {0}", FormatAugmentList(session.Expedition.TemporaryAugmentIds)));
        builder.AppendLine(Localize(GameLocalizationTables.UIReward, "ui.reward.build.thesis", "Build Thesis: {0}", BuildThesisLine(session, equippedPermanentId)));
        return builder.ToString();
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
            return Localize(GameLocalizationTables.UIReward, "ui.reward.status.default.quick", "Quick Battle smoke settlement: pick one card and return to Town.");
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
            return Localize(GameLocalizationTables.UIReward, "ui.reward.action.return_town_smoke", "Return to Town / Smoke Complete");
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
            return Localize(GameLocalizationTables.UIReward, "ui.reward.result.quick_smoke", "Quick Battle Smoke");
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
            return Localize(GameLocalizationTables.UIReward, "ui.reward.continuation.smoke", "Smoke lane closes and returns to Town.");
        }

        return IsFinalExtractSettlement(session)
            ? Localize(GameLocalizationTables.UIReward, "ui.reward.continuation.complete", "Run closes after this return to Town.")
            : Localize(GameLocalizationTables.UIReward, "ui.reward.continuation.resume", "Run stays active and can resume from Town.");
    }

    private string ResolveChoiceTitle(RewardChoiceViewModel choice) => Localize(GameLocalizationTables.UIReward, choice.TitleKey, choice.PayloadId);
    private string ResolveChoiceDescription(RewardChoiceViewModel choice) => Localize(GameLocalizationTables.UIReward, choice.DescriptionKey, choice.PayloadId);
    private string Localize(string table, string key, string fallback, params object[] args) => _localization.LocalizeOrFallback(table, key, fallback, args);
}
