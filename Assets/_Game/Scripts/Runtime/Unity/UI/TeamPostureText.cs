using SM.Combat.Model;

namespace SM.Unity.UI;

internal static class TeamPostureText
{
    internal static string Resolve(
        GameLocalizationController localization,
        TeamPostureType posture)
    {
        var (key, fallback) = posture switch
        {
            TeamPostureType.HoldLine => ("ui.common.posture.hold_line", "Hold Line"),
            TeamPostureType.StandardAdvance => ("ui.common.posture.standard_advance", "Standard Advance"),
            TeamPostureType.ProtectCarry => ("ui.common.posture.protect_carry", "Protect Carry"),
            TeamPostureType.CollapseWeakSide => ("ui.common.posture.collapse_weak_side", "Collapse Weak Side"),
            TeamPostureType.AllInBackline => ("ui.common.posture.all_in_backline", "All In Backline"),
            _ => ("ui.common.posture.unknown", "Unknown posture"),
        };

        return localization.LocalizeOrFallback(
            GameLocalizationTables.UICommon,
            key,
            fallback);
    }
}
