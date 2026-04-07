using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Unity.UI.Reward;

public sealed class RewardScreenPresenter
{
    private readonly GameSessionRoot _root;
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly RewardScreenView _view;

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

    public void ReturnToTown()
    {
        _root.SessionState.ReturnToTownAfterReward();
        _root.SaveProfile();
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
            _root.SaveProfile();
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
        return new RewardScreenViewState(
            Localize(GameLocalizationTables.UIReward, "ui.reward.title", "Reward Operator UI"),
            BuildLocaleStatus(),
            GetLocaleButtonLabel("ko", "한국어"),
            GetLocaleButtonLabel("en", "English"),
            BuildRunDeltaText(session),
            BuildBuildContextText(session),
            Localize(GameLocalizationTables.UIReward, "ui.reward.choices.header", "Choose one reward card"),
            BuildChoiceCards(session),
            string.IsNullOrWhiteSpace(message)
                ? defaultStatus
                : message,
            BuildReturnTownLabel(session));
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
                choice == null ? string.Empty : BuildChoiceContextText(choice, session),
                Localize(GameLocalizationTables.UIReward, "ui.reward.action.choose", "Choose"),
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
        sb.AppendLine(session.LastBattleSummary.HasValue
            ? session.LastBattleSummary.Resolve(_localization, _contentText)
            : BuildFallbackSummary(session));
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
            sb.AppendLine($"Permanent Candidate Pending: {_contentText.GetAugmentName(pendingUnlockId)}");
        }
        else if (session.LastPermanentUnlockSummary.HasValue)
        {
            sb.AppendLine(session.LastPermanentUnlockSummary.Resolve(_localization, _contentText));
        }

        var profile = _root.ProfileQueries.GetProfileView(_root.ActiveProfileId);
        sb.AppendLine($"Wallet Now: {profile.Gold} Gold / {profile.Echo} Echo");
        sb.AppendLine($"Inventory Now: {profile.InventoryCount} items");
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
        builder.AppendLine($"Posture: {session.SelectedTeamPosture}");
        builder.AppendLine($"Equipped Permanent: {FormatAugmentName(equippedPermanentId)}");
        builder.AppendLine($"Bench Candidates: {FormatAugmentList(benchPermanentIds)}");
        builder.AppendLine($"Current Temp Augments: {FormatAugmentList(session.Expedition.TemporaryAugmentIds)}");
        builder.AppendLine($"Build Thesis: {BuildThesisLine(session, equippedPermanentId)}");
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
            RewardChoiceKind.Gold => "Economy rail: recruit and refresh.",
            RewardChoiceKind.Echo => "Economy rail: scout, retrain, refit, and recovery.",
            RewardChoiceKind.Item => BuildItemChoiceContext(choice.PayloadId),
            RewardChoiceKind.TemporaryAugment => BuildTemporaryAugmentChoiceContext(choice.PayloadId, session),
            RewardChoiceKind.PermanentAugmentSlot => "Normal lane does not generate permanent slot rewards.",
            _ => string.Empty,
        };
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
            return "Hero hook: inventory-ready permanent item.";
        }

        return item.SlotType switch
        {
            SM.Content.Definitions.ItemSlotType.Weapon => "Hero hook: offensive or rule-changing weapon line.",
            SM.Content.Definitions.ItemSlotType.Armor => "Hero hook: frontline durability or protection line.",
            SM.Content.Definitions.ItemSlotType.Accessory => "Hero hook: utility or sustain accessory line.",
            _ => "Hero hook: inventory-ready permanent item.",
        };
    }

    private string BuildTemporaryAugmentChoiceContext(string augmentId, GameSessionState session)
    {
        var builder = new StringBuilder();
        builder.Append(BuildAugmentSupportText(augmentId));
        var previewUnlockId = session.PreviewPermanentUnlockFromTemporaryAugment(augmentId);
        if (!string.IsNullOrWhiteSpace(previewUnlockId))
        {
            builder.Append($" / First temp pick unlocks {_contentText.GetAugmentName(previewUnlockId)}");
        }
        else if (!string.IsNullOrWhiteSpace(session.ActiveRun?.Overlay.FirstSelectedTemporaryAugmentId))
        {
            builder.Append(" / Permanent unlock already fixed for this run");
        }

        return builder.ToString();
    }

    private string BuildAugmentSupportText(string augmentId)
    {
        if (!_root.CombatContentLookup.TryGetAugmentDefinition(augmentId, out var augment))
        {
            return "Run hook: temporary tactical spike.";
        }

        return augment.FamilyId switch
        {
            "hunt_line" => "Run hook: supports front-line pressure and finishing.",
            "ward_line" => "Run hook: supports sustain and protection pivots.",
            "tempo_drive" => "Run hook: supports tempo and snowball lines.",
            "hex_line" => "Run hook: supports control and attrition lines.",
            _ => augment.IsPermanent
                ? "Build hook: permanent thesis choice."
                : "Run hook: temporary tactical spike.",
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
                "hunt_line" => "Frontline pressure",
                "ward_line" => "Sustain pivot",
                "tempo_drive" => "Tempo snowball",
                "hex_line" => "Control attrition",
                _ => _contentText.GetAugmentName(equippedPermanentId),
            });
        }
        else
        {
            thesisParts.Add("No permanent thesis");
        }

        var tempCount = session.Expedition.TemporaryAugmentIds.Count;
        thesisParts.Add(tempCount == 0 ? "No temp overlay yet" : $"{tempCount} temp overlay");
        return string.Join(" / ", thesisParts);
    }

    private string GetEquippedPermanentAugmentId(GameSessionState session)
    {
        return session.Profile.PermanentAugmentLoadouts
            .FirstOrDefault(record => string.Equals(record.BlueprintId, session.Profile.ActiveBlueprintId, StringComparison.Ordinal))
            ?.EquippedAugmentIds.FirstOrDefault() ?? string.Empty;
    }

    private string FormatAugmentName(string augmentId)
        => string.IsNullOrWhiteSpace(augmentId) ? "None" : _contentText.GetAugmentName(augmentId);

    private string FormatAugmentList(IEnumerable<string> augmentIds)
    {
        var names = augmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(FormatAugmentName)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return names.Count == 0 ? "None" : string.Join(", ", names);
    }

    private string ResolveChoiceTitle(RewardChoiceViewModel choice) => Localize(GameLocalizationTables.UIReward, choice.TitleKey, choice.PayloadId);
    private string ResolveChoiceDescription(RewardChoiceViewModel choice) => Localize(GameLocalizationTables.UIReward, choice.DescriptionKey, choice.PayloadId);
    private string Localize(string table, string key, string fallback, params object[] args) => _localization.LocalizeOrFallback(table, key, fallback, args);
}
