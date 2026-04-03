namespace SM.Combat.Model;

public sealed record BattlefieldLayout(
    float FrontRowX,
    float BackRowX,
    float TopLaneY,
    float CenterLaneY,
    float BottomLaneY,
    float SpawnOffsetX)
{
    public static readonly BattlefieldLayout Default = new(2.8f, 4.9f, 1.8f, 0f, -1.8f, 1.25f);

    public CombatVector2 ResolveAnchorPosition(TeamSide side, DeploymentAnchorId anchor)
    {
        var frontX = side == TeamSide.Ally ? -FrontRowX : FrontRowX;
        var backX = side == TeamSide.Ally ? -BackRowX : BackRowX;
        var y = anchor switch
        {
            DeploymentAnchorId.FrontTop or DeploymentAnchorId.BackTop => TopLaneY,
            DeploymentAnchorId.FrontCenter or DeploymentAnchorId.BackCenter => CenterLaneY,
            _ => BottomLaneY,
        };

        return new CombatVector2(anchor.IsFrontRow() ? frontX : backX, y);
    }

    public CombatVector2 ResolveSpawnPosition(TeamSide side, DeploymentAnchorId anchor)
    {
        var anchorPos = ResolveAnchorPosition(side, anchor);
        var offset = side == TeamSide.Ally ? -SpawnOffsetX : SpawnOffsetX;
        return anchorPos + new CombatVector2(offset, 0f);
    }
}
