using System;
using SM.Core.Content;
using SM.Core.Results;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Equipment Refit player-facing copy. The presenter supplies semantic state and
/// structured failure causes; this formatter owns localization keys and fallback formatting.
/// </summary>
internal sealed class EquipmentRefitText
{
    private readonly EquipmentRefitPresenter.TextResolver _resolve;

    internal EquipmentRefitText(EquipmentRefitPresenter.TextResolver? resolve)
    {
        _resolve = resolve ?? ((_, _, fallback, arguments) =>
            FormatFallback(fallback, arguments));
    }

    internal string SelectItemReason =>
        Town("ui.town.refit.reason.select_item", "Select an item to refit.");

    internal string UnequippedLabel =>
        Town("ui.town.refit.status.unequipped", "Unequipped");

    internal string PanelTitle =>
        Town("ui.town.refit.title", "EQUIPMENT REFIT");

    internal string OperationSelectorLabel =>
        Town("ui.town.refit.operation.label", "Choose operation");

    internal string ReforgeOperationLabel =>
        Town("ui.town.refit.operation.reforge", "Reforge");

    internal string SealOperationLabel =>
        Town("ui.town.refit.operation.seal", "Seal");

    internal string ConfirmLabel =>
        Common("ui.common.confirm", "Confirm");

    internal string CancelLabel =>
        Common("ui.common.cancel", "Cancel");

    internal string Equipped(string heroName) =>
        Town("ui.town.refit.status.equipped", "Equipped: {0}", heroName);

    /// <summary>
    /// 접사 계층 머리글. GroupKey는 AffixTierValue enum 이름이라 스타일 후크로만 쓰고, 화면에 나가는
    /// 문구는 여기서 소유한다 (enum ToString이 대문자로 렌더돼 IMPLICIT/PREFIX가 노출되던 결함).
    /// </summary>
    internal string AffixGroupHeader(string groupKey) => groupKey switch
    {
        "implicit" => Town("ui.town.refit.affix_group.implicit", "고유"),
        "prefix" => Town("ui.town.refit.affix_group.prefix", "접두"),
        "suffix" => Town("ui.town.refit.affix_group.suffix", "접미"),
        _ => Town("ui.town.refit.affix_group.other", "기타"),
    };

    internal string LockLabel(bool isLocked) =>
        isLocked
            ? Town("ui.town.refit.lock.locked", "Locked")
            : Town("ui.town.refit.lock.open", "Lock");

    internal string SealUnavailable(string localizedReason) =>
        Town(
            "ui.town.refit.operation.seal_unavailable",
            "Seal unavailable: {0}",
            localizedReason);

    internal string CostLabel(int echoCost) =>
        echoCost > 0
            ? Town("ui.town.refit.quote.cost", "{0} Echo", echoCost)
            : "—";

    internal string Confirmation(
        int echoCost,
        CraftOperationKindValue operation) =>
        Town(
            "ui.town.refit.confirmation.body",
            "Spend {0} Echo on {1}? The previous roll cannot be restored.",
            echoCost,
            OperationLabel(operation));

    internal string BuildOperationStatus(
        RefitQuote quote,
        CraftOperationKindValue operation,
        int lockedAffixCount,
        int totalAffixCount,
        OperationFailure? purchaseBlockFailure)
    {
        if (quote.RefitMaxed)
        {
            return Town(
                "ui.town.refit.reason.quality_maxed",
                "The item is already at the maximum Refit quality.");
        }

        if (purchaseBlockFailure != null)
        {
            return LocalizeFailure(purchaseBlockFailure, operation, quote);
        }

        if (!quote.CanPurchase)
        {
            return LocalizeFailure(quote.Failure, operation, quote);
        }

        return operation == CraftOperationKindValue.Seal
            ? Town(
                "ui.town.refit.status.seal_quote",
                "{0}/{1} affixes locked · quality {2:0.0}% → guaranteed floor {3:0.0}% · service quote {4} Echo",
                lockedAffixCount,
                totalAffixCount,
                ToPercent(quote.CurrentPercentileQ64),
                ToPercent(quote.TargetFloorQ64),
                quote.EchoCost)
            : Town(
                "ui.town.refit.status.reforge_quote",
                "Quality {0:0.0}% → guaranteed floor {1:0.0}% · service quote {2} Echo",
                ToPercent(quote.CurrentPercentileQ64),
                ToPercent(quote.TargetFloorQ64),
                quote.EchoCost);
    }

    internal string LocalizeFailure(
        OperationFailure? failure,
        CraftOperationKindValue operation,
        RefitQuote quote)
    {
        if (failure == null)
        {
            return string.Empty;
        }

        if (failure.IsInvariantViolation)
        {
            return Town(
                "ui.town.refit.reason.operation_failed",
                "The operation could not be completed. Please try again.");
        }

        if (string.Equals(
                failure.Code,
                SessionOperationFailureCodes.RefitItemSelectionRequired,
                StringComparison.Ordinal))
        {
            return SelectItemReason;
        }

        if (string.Equals(
                failure.Code,
                MetaOperationFailureCodes.RefitOperationNotAllowed,
                StringComparison.Ordinal))
        {
            return operation == CraftOperationKindValue.Seal
                ? Town(
                    "ui.town.refit.reason.seal_not_allowed",
                    "This item does not allow Seal.")
                : Town(
                    "ui.town.refit.reason.reforge_not_allowed",
                    "This item does not allow Reforge.");
        }

        if (string.Equals(
                failure.Code,
                MetaOperationFailureCodes.RefitSealAllAffixesLocked,
                StringComparison.Ordinal))
        {
            return Town(
                "ui.town.refit.reason.all_affixes_locked",
                "Seal must leave at least one affix unlocked.");
        }

        if (string.Equals(
                failure.Code,
                MetaOperationFailureCodes.RefitSealSelectionRequired,
                StringComparison.Ordinal))
        {
            return Town(
                "ui.town.refit.reason.seal_selection_required",
                "Select the affixes to lock before sealing.");
        }

        if (string.Equals(
                failure.Code,
                MetaOperationFailureCodes.RefitSealSelectionInvalid,
                StringComparison.Ordinal))
        {
            return Town(
                "ui.town.refit.reason.seal_selection_invalid",
                "The selected affix locks are no longer valid.");
        }

        if (string.Equals(
                failure.Code,
                MetaOperationFailureCodes.RefitAffixIllegalForSlot,
                StringComparison.Ordinal))
        {
            return Town(
                "ui.town.refit.reason.affix_illegal_for_slot",
                "This item's affixes are not valid for its equipment slot.");
        }

        if (string.Equals(
                failure.Code,
                MetaOperationFailureCodes.RefitAffixExclusiveConflict,
                StringComparison.Ordinal))
        {
            return Town(
                "ui.town.refit.reason.affix_conflict",
                "This item has mutually exclusive affixes.");
        }

        if (string.Equals(
                failure.Code,
                MetaOperationFailureCodes.RefitQualityMaxed,
                StringComparison.Ordinal))
        {
            return Town(
                "ui.town.refit.reason.quality_maxed",
                "The item is already at the maximum Refit quality.");
        }

        if (string.Equals(
                failure.Code,
                SessionOperationFailureCodes.RefitUnaffordable,
                StringComparison.Ordinal))
        {
            return operation == CraftOperationKindValue.Seal
                ? Town(
                    "ui.town.refit.reason.seal_unaffordable",
                    "Not enough Echo. Seal requires {0} Echo.",
                    quote.EchoCost)
                : Town(
                    "ui.town.refit.reason.reforge_unaffordable",
                    "Not enough Echo. Reforge requires {0} Echo.",
                    quote.EchoCost);
        }

        if (string.Equals(
                failure.Code,
                SessionOperationFailureCodes.RefitTownOnly,
                StringComparison.Ordinal))
        {
            return Town(
                "ui.town.refit.reason.town_only",
                "Crafting is available only in Town.");
        }

        if (string.Equals(
                failure.Code,
                SessionOperationFailureCodes.ItemNotFound,
                StringComparison.Ordinal))
        {
            return Town(
                "ui.town.refit.reason.item_missing",
                "The selected item is no longer available.");
        }

        return Town(
            "ui.town.refit.reason.unavailable",
            "This operation is currently unavailable.");
    }

    internal string BuildCraftActionLabel(
        CraftOperationKindValue operation,
        int echoCost)
    {
        if (echoCost <= 0)
        {
            return OperationLabel(operation);
        }

        return operation == CraftOperationKindValue.Seal
            ? Town("ui.town.refit.action.seal", "Seal (-{0} Echo)", echoCost)
            : Town("ui.town.refit.action.reforge", "Reforge (-{0} Echo)", echoCost);
    }

    private string OperationLabel(CraftOperationKindValue operation) =>
        operation == CraftOperationKindValue.Seal
            ? SealOperationLabel
            : ReforgeOperationLabel;

    private string Town(string key, string fallback, params object[] arguments) =>
        _resolve(GameLocalizationTables.UITown, key, fallback, arguments);

    private string Common(string key, string fallback, params object[] arguments) =>
        _resolve(GameLocalizationTables.UICommon, key, fallback, arguments);

    private static double ToPercent(ulong probabilityQ64) =>
        probabilityQ64 / (double)ulong.MaxValue * 100d;

    private static string FormatFallback(string fallback, object[] arguments)
    {
        if (arguments.Length == 0)
        {
            return fallback;
        }

        try
        {
            return string.Format(fallback, arguments);
        }
        catch (FormatException)
        {
            return fallback;
        }
    }
}
