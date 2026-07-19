using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Editor.Validation;

/// <summary>
/// authored enemy member/mechanic은 그대로 두고 8개 결정적 order/anchor variant를 만든다.
/// 콘텐츠 자산이나 EncounterResolutionService를 수정하지 않는 measurement-input adapter다.
/// </summary>
internal static class CampaignBalanceGridProjector
{
    public static IReadOnlyList<BattleUnitLoadout> ProjectEnemyComposition(
        IReadOnlyList<BattleUnitLoadout> authored,
        int variantIndex)
    {
        if (authored == null)
        {
            throw new ArgumentNullException(nameof(authored));
        }

        return variantIndex switch
        {
            0 => authored.ToArray(),
            1 => authored.Select(unit => unit with { PreferredAnchor = MirrorLane(unit.PreferredAnchor) }).ToArray(),
            2 => authored.Select(unit => unit with { PreferredAnchor = SwapRow(unit.PreferredAnchor) }).ToArray(),
            3 => authored.Select(unit => unit with { PreferredAnchor = SwapRow(MirrorLane(unit.PreferredAnchor)) }).ToArray(),
            4 => authored.Select(unit => unit with { PreferredAnchor = RotateAnchor(unit.PreferredAnchor, +1) }).ToArray(),
            5 => authored.Select(unit => unit with { PreferredAnchor = RotateAnchor(unit.PreferredAnchor, -1) }).ToArray(),
            6 => RotateMemberAnchors(authored),
            7 => authored.Reverse().ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(variantIndex), variantIndex, "Enemy composition variant must be 0..7."),
        };
    }

    private static IReadOnlyList<BattleUnitLoadout> RotateMemberAnchors(IReadOnlyList<BattleUnitLoadout> authored)
    {
        if (authored.Count <= 1)
        {
            return authored.Select(unit => unit with { PreferredAnchor = MirrorLane(unit.PreferredAnchor) }).ToArray();
        }

        return authored.Select((unit, index) => unit with
        {
            PreferredAnchor = authored[(index + 1) % authored.Count].PreferredAnchor,
        }).ToArray();
    }

    private static DeploymentAnchorId MirrorLane(DeploymentAnchorId anchor)
        => anchor switch
        {
            DeploymentAnchorId.FrontTop => DeploymentAnchorId.FrontBottom,
            DeploymentAnchorId.FrontBottom => DeploymentAnchorId.FrontTop,
            DeploymentAnchorId.BackTop => DeploymentAnchorId.BackBottom,
            DeploymentAnchorId.BackBottom => DeploymentAnchorId.BackTop,
            _ => anchor,
        };

    private static DeploymentAnchorId SwapRow(DeploymentAnchorId anchor)
        => anchor switch
        {
            DeploymentAnchorId.FrontTop => DeploymentAnchorId.BackTop,
            DeploymentAnchorId.FrontCenter => DeploymentAnchorId.BackCenter,
            DeploymentAnchorId.FrontBottom => DeploymentAnchorId.BackBottom,
            DeploymentAnchorId.BackTop => DeploymentAnchorId.FrontTop,
            DeploymentAnchorId.BackCenter => DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.BackBottom => DeploymentAnchorId.FrontBottom,
            _ => anchor,
        };

    private static DeploymentAnchorId RotateAnchor(DeploymentAnchorId anchor, int offset)
    {
        const int anchorCount = 6;
        var value = (((int)anchor + offset) % anchorCount + anchorCount) % anchorCount;
        return (DeploymentAnchorId)value;
    }
}
