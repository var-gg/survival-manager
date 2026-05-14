using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Permanent Augment V1 surface ViewState — runtime 모델 정합 (audit §4.1, P0-4 다운스코프).
/// 모델: "장착한 영구 augment 1개 + 해금 후보 풀". MaxPermanentAugmentSlots = 1 (equip = 덮어쓰기).
///
/// - ✅ Unlocked = Profile.UnlockedPermanentAugmentIds · IsEquipped = PermanentAugmentLoadouts.EquippedAugmentIds (단일 슬롯)
/// - ◐ KoLabel / FamilyBucket / SignatureEffect = AugmentDefinition 파생 (NameKey / FamilyId / Effects·Modifiers·DescriptionKey)
/// - ❌ 폐기: PostureId·PostureLabel (AugmentDefinition에 posture field 없음 — 매핑은 설명문 플레이버로만) ·
///   ProgressCurrent·Max + UnlockHint (AugmentFamilyPickHistory backing 없음) · FamilyBucket 색축
/// </summary>
public sealed record PermanentAugmentCellViewState(
    string AugmentId,            // augment_perm_citadel_doctrine 등
    string KoLabel,              // ◐ AugmentDefinition NameKey resolved
    string FamilyBucket,         // ◐ AugmentDefinition.FamilyId — 정보 표기 (색축 아님)
    bool Unlocked,               // ✅ UnlockedPermanentAugmentIds 포함 여부
    bool IsEquipped,             // ✅ EquippedAugmentIds — 현재 장착된 1개
    bool IsSelected,             // detail panel 표시 중
    Texture2D? IconSprite
);

public sealed record PermanentAugmentMetaRowViewState(
    string Label,                // "FAMILY" / "강화 효과"
    string Value
);

public sealed record PermanentAugmentDetailViewState(
    string SelectedAugmentId,
    string KoLabel,              // "수호 분대"
    string EnLabel,              // "Guardian Detail" — 영문 별칭
    string FamilyBucket,         // ◐ AugmentDefinition.FamilyId — subtitle (posture 매핑 폐기)
    string SignatureEffect,      // ◐ AugmentDefinition Effects/Modifiers/DescriptionKey 파생
    string FlavorText,           // 한국어 플레이버 산문
    bool IsUnlocked,             // ✅ equip 가능 여부
    bool IsEquipped,             // ✅ 이미 장착됨 (Equip CTA 상태 분기)
    Texture2D? IconSprite,       // 선택 augment icon — detail hero + equip slot 공용
    IReadOnlyList<PermanentAugmentMetaRowViewState> MetaRows
);

public sealed record PermanentAugmentViewState(
    IReadOnlyList<PermanentAugmentCellViewState> Cells,
    PermanentAugmentDetailViewState Detail
);
