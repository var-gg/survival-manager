using System;
using System.Collections.Generic;

namespace SM.Core.Contracts;

public enum ActionSlotKind
{
    BasicAttack = 0,
    SignatureActive = 1,
    FlexActive = 2,
    SignaturePassive = 3,
    FlexPassive = 4,
    MobilityReaction = 5,
}

public enum ActivationModel
{
    Passive = 0,
    Energy = 1,
    Cooldown = 2,
    Trigger = 3,
}

public enum ResourceKind
{
    Energy = 0,
}

public enum ActionLane
{
    Primary = 0,
    Reaction = 1,
    Locomotion = 2,
}

public enum ActionLockRule
{
    None = 0,
    SoftCommit = 1,
    HardCommit = 2,
}

public static class ActionSlotKindCatalog
{
    public static readonly IReadOnlyList<ActionSlotKind> Ordered = new[]
    {
        ActionSlotKind.BasicAttack,
        ActionSlotKind.SignatureActive,
        ActionSlotKind.FlexActive,
        ActionSlotKind.SignaturePassive,
        ActionSlotKind.FlexPassive,
        ActionSlotKind.MobilityReaction,
    };

    public static readonly IReadOnlyCollection<ActionSlotKind> MutableFlexSlots = new[]
    {
        ActionSlotKind.FlexActive,
        ActionSlotKind.FlexPassive,
    };

    public static bool IsMutableFlexSlot(ActionSlotKind slotKind)
    {
        return slotKind is ActionSlotKind.FlexActive or ActionSlotKind.FlexPassive;
    }
}
