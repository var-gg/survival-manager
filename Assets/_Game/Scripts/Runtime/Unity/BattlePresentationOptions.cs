namespace SM.Unity;

public sealed class BattlePresentationOptions
{
    public bool ShowWorldActorHp { get; private set; } = true;
    public bool ShowOverlayActorHp { get; private set; }
    public bool ShowTeamHpSummary { get; private set; }

    public static BattlePresentationOptions CreateDefault()
    {
        return new BattlePresentationOptions
        {
            ShowWorldActorHp = true,
            ShowOverlayActorHp = false,
            ShowTeamHpSummary = false
        };
    }

    public void ToggleWorldActorHp() => ShowWorldActorHp = !ShowWorldActorHp;
    public void ToggleOverlayActorHp() => ShowOverlayActorHp = !ShowOverlayActorHp;
    public void ToggleTeamHpSummary() => ShowTeamHpSummary = !ShowTeamHpSummary;
}
