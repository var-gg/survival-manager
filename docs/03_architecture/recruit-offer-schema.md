# Recruit Offer Schema

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/03_architecture/recruit-offer-schema.md`
- 관련문서:
  - `docs/02_design/meta/recruitment-contract.md`
  - `docs/03_architecture/unit-economy-schema.md`

## 목적

Loop B recruit preview와 candidate scoring/pity/scout 상태가 runtime, save, UI, harness에서 같은 필드로 읽히도록 schema를 고정한다.

## 핵심 enum

| 타입 | 값 |
| --- | --- |
| `EconomyCurrencyKind` | `Gold`, `Echo` |
| `RecruitTier` | `Common`, `Rare`, `Epic` |
| `RecruitOfferSlotType` | `StandardA`, `StandardB`, `OnPlan`, `Protected` |
| `RecruitOfferSource` | `RecruitPhase`, `CombatReward`, `EventReward`, `DirectGrant` |
| `ScoutDirectiveKind` | `None`, `Frontline`, `Backline`, `Physical`, `Magical`, `Support`, `SynergyTag` |
| `CandidatePlanFit` | `OffPlan`, `Bridge`, `OnPlan` |
| `FlexRollBiasMode` | `NativeBiased`, `NativePlusPlanBiased` |

## Offer preview DTO

### `RecruitUnitPreview`

| 필드 | 설명 |
| --- | --- |
| `unitBlueprintId` | 현재 저장소에서는 `UnitArchetypeDefinition.Id` |
| `unitInstanceSeed` | preview deterministic reproduction seed |
| `flexActiveId` | 구매 전 표시되는 rolled flex active |
| `flexPassiveId` | 구매 전 표시되는 rolled flex passive |
| `metadata` | 아래 `RecruitOfferMetadata` |

### `RecruitOfferMetadata`

| 필드 | 설명 |
| --- | --- |
| `slotType` | 4-slot pack 내 위치 |
| `tier` | pack tier / gold cost 기준 |
| `planFit` | `OffPlan / Bridge / OnPlan` |
| `planScore` | 점수 breakdown |
| `protectedByPity` | pity floor로 보호됐는지 |
| `biasedByScout` | scout directive bias 적용 여부 |
| `goldCost` | 구매 cost |

### `CandidatePlanScoreBreakdown`

| 필드 | 의미 |
| --- | --- |
| `breakpointProgressScore` | 다음 breakpoint를 채우는 기여 |
| `nativeTagMatchScore` | 현재 top synergy/native tag 정합 |
| `roleNeedScore` | frontline/backline/support deficit 해소 |
| `augmentHookScore` | active augment와 hook 정합 |
| `scoutDirectiveScore` | scout directive bias |
| `oversaturationPenalty` | 과잉 role bucket 패널티 |
| `Total` | 합계 |

## Phase/pity state

| 타입 | 필드 |
| --- | --- |
| `RecruitPityState` | `packsSinceRarePlusSeen`, `packsSinceEpicSeen` |
| `RecruitPhaseState` | `freeRefreshesRemaining`, `paidRefreshCountThisPhase`, `scoutUsedThisPhase`, `pendingScoutDirective` |
| `ScoutDirective` | `kind`, `synergyTagId` |

## Invariants

- `RecruitUnitPreview`는 항상 `metadata`와 함께 저장/전달된다.
- `OnPlan` 슬롯은 `planFit=OnPlan`이 가능한 경우 반드시 그렇게 저장된다.
- `Protected` 슬롯은 pity floor와 함께 저장돼 UI/debug가 원인을 설명할 수 있어야 한다.
- recruit preview 없는 unit card는 runtime assert 대상이다.

## Fallback rules

- `OnPlan` 후보가 없으면 `Bridge`, 그것도 없으면 최고점 fallback을 쓴다.
- `ScoutDirective.kind != SynergyTag`이면 `synergyTagId`는 무시한다.

## 예시

### 예시 1. on-plan preview

```json
{
  "unitBlueprintId": "marksman",
  "unitInstanceSeed": "recruit:marksman:OnPlan:203",
  "flexActiveId": "skill_marksman_utility",
  "flexPassiveId": "support_longshot",
  "metadata": {
    "slotType": "OnPlan",
    "tier": "Rare",
    "planFit": "OnPlan",
    "protectedByPity": false,
    "biasedByScout": true,
    "goldCost": 7
  }
}
```

### 예시 2. protected pity preview

```json
{
  "slotType": "Protected",
  "tier": "Epic",
  "protectedByPity": true,
  "biasedByScout": false
}
```
