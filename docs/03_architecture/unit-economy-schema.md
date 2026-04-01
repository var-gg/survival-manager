# Unit Economy Schema

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/03_architecture/unit-economy-schema.md`
- 관련문서:
  - `docs/02_design/meta/economy-protection-contract.md`
  - `docs/02_design/meta/retrain-contract.md`
  - `docs/03_architecture/recruit-offer-schema.md`

## 목적

Loop B 기준 wallet, retrain state, duplicate conversion, dismiss refund가 persistence와 runtime에서 같은 필드 구조를 갖게 한다.

## 핵심 타입

### `EconomyWallet`

| 필드 | 의미 |
| --- | --- |
| `gold` | recruit / refresh / shop |
| `echo` | retrain / scout / duplicate recovery |

### `UnitEconomyFootprint`

| 필드 | 의미 |
| --- | --- |
| `recruitGoldPaid` | 해당 유닛 recruit에 실제로 지불한 gold |
| `retrainEchoPaid` | 해당 유닛 retrain 누적 echo |

### `UnitRetrainState`

| 필드 | 의미 |
| --- | --- |
| `retrainCount` | 누적 retrain 횟수 |
| `previousFlexActiveId` | immediate previous flex active |
| `previousFlexPassiveId` | immediate previous flex passive |
| `totalEchoSpent` | retrain 누적 echo |
| `consecutivePlanIncoherentRetrains` | plan miss pity counter |

### `RetrainCostTable`

| 필드 | 기본값 |
| --- | --- |
| `flexActiveBaseEchoCost` | `40` |
| `flexPassiveBaseEchoCost` | `30` |
| `fullRetrainBaseEchoCost` | `60` |
| `perUnitEscalation` | `10` |
| `escalationCap` | `30` |

### `DuplicateConversionResult`

| 필드 | 의미 |
| --- | --- |
| `resolution` | V1에서는 `ConvertToEcho` only |
| `sourceTier` | duplicate source tier |
| `echoGranted` | 지급 Echo |

## Persistence ownership

- profile wallet truth는 `CurrencyRecord`가 가진다.
- hero-local economy truth는 `HeroInstanceRecord`의 `UnitEconomyFootprint`, `UnitRetrainState`가 가진다.
- recruit phase-local state는 `ActiveRunRecord.RecruitPhase`, `ActiveRunRecord.RecruitPity`가 가진다.
- runtime orchestration은 `GameSessionState`, business rule은 `SM.Meta.Services`가 가진다.

## Invariants

- retrain은 `UnitEconomyFootprint.retrainEchoPaid`를 누적해야 한다.
- dismiss refund는 footprint 기반으로 계산해야 한다.
- duplicate는 hero instance 추가 없이 `DuplicateConversionResult`와 wallet 변화만 남겨야 한다.
- current/previous exclusion memory는 save/load 후에도 유지돼야 한다.

## Fallback rules

- `UnitRetrainState`가 비어 있으면 zero state로 간주한다.
- `UnitEconomyFootprint`가 비어 있으면 refund `0/0`으로 간주한다.
- legacy reroll token/echo crystal은 Loop B wallet source-of-truth가 아니다.

## 예시

### 예시 1. retrain 후 저장 상태

```json
{
  "retrainCount": 3,
  "previousFlexActiveId": "hunter_mark",
  "previousFlexPassiveId": "support_longshot",
  "totalEchoSpent": 190,
  "consecutivePlanIncoherentRetrains": 0
}
```

### 예시 2. dismiss refund footprint

```json
{
  "recruitGoldPaid": 10,
  "retrainEchoPaid": 20
}
```

위 footprint는 dismiss 시 `Gold 5`, `Echo 10` 환급 근거가 된다.
