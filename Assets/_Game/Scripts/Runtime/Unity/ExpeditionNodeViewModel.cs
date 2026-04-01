using System.Collections.Generic;

namespace SM.Unity;

public enum ExpeditionNodeEffectKind
{
    None = 0,
    Gold = 1,
    Echo = 2,
    TraitRerollCurrency = Echo,
    TemporaryAugment = 3,
    PermanentAugmentSlot = 4
}

public sealed record ExpeditionNodeViewModel(
    int Index,
    string Id,
    string LabelKey,
    string PlannedRewardKey,
    string DescriptionKey,
    bool RequiresBattle,
    ExpeditionNodeEffectKind EffectKind,
    int EffectAmount,
    string EffectPayloadId,
    IReadOnlyList<int> NextNodeIndices);
