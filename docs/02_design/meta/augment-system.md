# augment 시스템

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

이 문서는 run 중 temporary augment와 run 밖 permanent augment의 관계를 정의한다.
목표는 한 판의 빌드 변화와 장기 진척을 분리하면서도 서로 연결되게 만드는 것이다.

## 핵심 규칙

### temporary augment

MVP는 9개의 temporary augment를 둔다.

- silver 3
- gold 3
- platinum 3

temporary augment는 run 중에만 적용되는 강화다.

### permanent augment

MVP는 permanent augment slot 1개를 둔다.
permanent augment는 다음 run들에도 유지되는 장기 강화다.

### 최초 선택 해금 문법

**temporary augment를 특정 계열에서 최초 선택하면, 관련 permanent augment 후보가 해금된다.**

예시 문법:

- run 중 `TempAugment.Fireline`을 처음 선택
- 계열 태그 `fireline`이 영구 해금 후보 풀에 추가
- expedition 종료 또는 특정 milestone에서 `PermAugment.Fireline_Core`가 후보로 등장 가능

즉 temporary augment는 단순 즉시 보상일 뿐 아니라, permanent augment 후보를 여는 "발견/해금 문법" 역할도 한다.

### augment 방향성 규칙

augment는 아래 두 방향을 모두 허용한다.

- 시너지 강화형
- 비활성 시너지 유닛 강화형

즉 활성 조합을 밀어주는 것과, 혼합 조합/외로운 유닛을 살리는 것 둘 다 시스템적으로 허용한다.

## MVP 구현 범위

- temporary augment 3-choice 보상
- permanent augment slot 1개
- temporary augment 최초 선택 -> related permanent candidate unlock 문법
- 일부 augment의 synergy 강화형 / off-synergy 보정형 방향 정의

## 장기 규칙

- permanent augment 다중 슬롯
- branch형 permanent tree
- account-level progression
- archetype-specific augment family 확장

## 밸런스 기준

- temporary augment가 기본 roster/gameplay를 덮어쓰면 안 된다.
- permanent augment는 희귀하고 기대감 있어야 하지만, 금방 power inflation을 만들면 안 된다.
- off-synergy augment도 실제 선택지가 되어야 한다.
