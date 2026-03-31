namespace SM.Unity;

public sealed class BattlePresentationOptions
{
    public bool ShowOverheadUi { get; private set; } = true;
    public bool ShowDamageText { get; private set; } = true;
    public bool ShowTeamHpSummary { get; private set; }
    public bool ShowDebugOverlay { get; private set; }

    public bool ShowWorldActorHp => false;
    public bool ShowOverlayActorHp => ShowOverheadUi;

    public static BattlePresentationOptions CreateDefault()
    {
        return new BattlePresentationOptions
        {
            ShowOverheadUi = true,
            ShowDamageText = true,
            ShowTeamHpSummary = false,
            ShowDebugOverlay = false
        };
    }

    public void ToggleOverheadUi() => ShowOverheadUi = !ShowOverheadUi;
    public void ToggleDamageText() => ShowDamageText = !ShowDamageText;
    public void ToggleTeamHpSummary() => ShowTeamHpSummary = !ShowTeamHpSummary;
    public void ToggleDebugOverlay() => ShowDebugOverlay = !ShowDebugOverlay;

    public void ToggleWorldActorHp() => ToggleOverheadUi();
    public void ToggleOverlayActorHp() => ToggleDamageText();
}
