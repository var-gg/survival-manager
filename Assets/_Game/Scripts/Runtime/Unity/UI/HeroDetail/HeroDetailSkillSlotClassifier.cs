using SM.Core.Contracts;

namespace SM.Unity.UI.HeroDetail;

/// <summary>
/// V1 scoped 4-slot 위계 도출 규칙.
///
/// 런타임은 스킬 슬롯을 signature/flex 2축(<see cref="ActionSlotKind"/>)으로만 구분한다.
/// v0.5 디자인이 요구하는 4-state(signature-lock / flex-active / flex-retrain / late-unlock)를
/// 단일 권위 필드 없이 기존 데이터에서 도출한다:
/// - signature active/passive → 항상 SignatureLock (baseline 고정, 교체 불가)
/// - flex 슬롯이 progression 해금(HeroProgressionRecord.UnlockedSkillIds)으로 채워졌으면 → LateUnlock
/// - flex 슬롯에 hero 명시 선택(HeroInstanceRecord.FlexActiveId/FlexPassiveId)이 있으면 → FlexActive
/// - flex 슬롯이 baseline 기본값을 쓰면 → FlexRetrain (mutable, 아직 미커스텀)
///
/// precedence: progression unlock > 명시 선택 > 기본 retrain 후보.
/// per-hero stance는 런타임 부재로 본 분류 밖.
/// </summary>
public static class HeroDetailSkillSlotClassifier
{
    public static HeroDetailSlotKind Classify(
        ActionSlotKind slotKind,
        bool heroHasExplicitFlexChoice,
        bool isUnlockedViaProgression)
    {
        if (slotKind is ActionSlotKind.SignatureActive or ActionSlotKind.SignaturePassive)
        {
            return HeroDetailSlotKind.SignatureLock;
        }

        if (isUnlockedViaProgression)
        {
            return HeroDetailSlotKind.LateUnlock;
        }

        return heroHasExplicitFlexChoice
            ? HeroDetailSlotKind.FlexActive
            : HeroDetailSlotKind.FlexRetrain;
    }
}
