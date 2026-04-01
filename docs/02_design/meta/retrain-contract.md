# Retrain Contract

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/meta/retrain-contract.md`
- 관련문서:
  - `docs/02_design/meta/recruitment-contract.md`
  - `docs/02_design/meta/economy-protection-contract.md`
  - `docs/03_architecture/unit-economy-schema.md`

## 목적

Loop B 기준 retrain을 `Echo 기반 flex-only correction tool`로 고정한다.

## V1 allowed

- retrain 대상 슬롯은 `FlexActive`, `FlexPassive` 두 개뿐이다.
- 연산은 `RerollFlexActive`, `RerollFlexPassive`, `FullRetrain`만 허용한다.
- retrain cost는 `Echo`만 사용한다.
- current result, immediate previous result 재등장은 금지한다.
- native coherence, banned pairing, signature same-family 금지는 항상 유지한다.
- `consecutivePlanIncoherentRetrains >= 2`면 다음 retrain에서 가능한 경우 최소 1개 plan-coherent 결과를 강제한다.

## V1 forbidden

- signature slot 변경
- exact skill 지정 reroll
- Gold 사용 retrain
- native role / formation line 재작성
- same family flex/signature 중복

## 비용 곡선

| 연산 | base cost |
| --- | --- |
| `RerollFlexActive` | `40 Echo` |
| `RerollFlexPassive` | `30 Echo` |
| `FullRetrain` | `60 Echo` |

추가 규칙:

- unit별 retrain 시도마다 `+10 Echo`
- 최대 추가 비용은 `+30 Echo`
- 즉 `1회차 base`, `2회차 base+10`, `3회차 base+20`, `4회차 이후 base+30`

## 결과 제한

- current 결과 재등장 금지
- previous 결과 재등장 금지
- signature effect family 중복 금지
- native coherence 위반 금지
- banned pairing 위반 금지

## Retrain pity

| 상태 | 동작 |
| --- | --- |
| 결과가 current plan과 정합적 | `consecutivePlanIncoherentRetrains = 0` |
| 결과가 current plan과 비정합적 | counter `+1` |
| counter `>= 2` + 다음 retrain 실행 | 가능한 후보 중 최소 1개 plan-coherent 보장 |
| plan-coherent 후보 자체가 없음 | native coherence만 유지, pity 미소모 |

## FullRetrain 정의

- `FlexActive`, `FlexPassive`를 동시에 다시 뽑는다.
- 두 슬롯을 동시에 다시 뽑아도 아래 제약은 풀리지 않는다.
  - native coherence
  - signature same-family 금지
  - previous/current immediate recurrence 금지
  - banned pairing 금지

## Fallback rules

- plan-coherent 후보가 전혀 없으면 native-coherent 후보 집합으로 내려간다.
- native-coherent 후보도 없으면 content authoring이 잘못된 상태로 보고 validator가 차단해야 한다.
- pity는 `plan-coherent candidate exists`일 때만 강제 발동한다.

## 예시

### 예시 1. 일반 full retrain

- 현재 `hunter`가 `FlexActive=hunter_mark`, `FlexPassive=hunter_support`를 들고 있다.
- `FullRetrain` 실행 시 두 결과는 둘 다 바뀌고, 바로 직전 결과는 다시 나오지 않는다.

### 예시 2. retrain pity

- 같은 유닛이 두 번 연속 plan-coherent 결과를 못 얻었다.
- 세 번째 retrain에서 `mark` 또는 `backline` 정합 후보가 있으면 최소 1개는 그 방향으로 강제된다.
