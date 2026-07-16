namespace SM.Editor.Validation;

/// <summary>직전 site reward/recruit 선택 하나를 바꿔 target site에 재진입한 profile 분기.</summary>
internal sealed record H100SunkenLookbackVariant(
    string VariantId,
    string ProfileSnapshot,
    string SourceProfileSnapshot,
    int RecruitOfferIndex,
    string AddedRosterArchetypeId,
    int RewardOptionIndex,
    string RewardPayloadId);
