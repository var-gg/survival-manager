# 시너지 시스템

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/combat/synergy-system.md`
- 관련문서:
  - `docs/02_design/meta/synergy-breakpoints-and-soft-counters.md`
  - `docs/02_design/systems/launch-content-scope-and-balance.md`

## 목적

이 문서는 prototype 기준의 시너지 축과 MVP 범위를 설명한다.
paid launch 기준의 breakpoint와 soft counter 숫자는 별도 launch 문서가 소유한다.

## MVP 규칙

### 시너지 축

MVP는 두 축을 사용한다.

- race synergy
- class synergy

### 권장 threshold

- race synergy: 2 / 3
- class synergy: 2 / 4

### MVP 종족/직업

- Human
- Beastkin
- Undead

- Vanguard
- Duelist
- Ranger
- Mystic

## augment와의 관계

augment는 다음 두 방향을 모두 허용한다.

1. **시너지 강화형 augment**
   - 특정 race/class threshold를 더 강하게 만든다.
   - 특정 synergy 활성 시 추가 보너스를 준다.

2. **비활성 시너지 유닛 강화형 augment**
   - 현재 synergy를 못 받는 유닛
   - threshold를 못 채운 유닛
   - 혼합 편성 때문에 외로운 유닛
   를 보정하는 강화도 허용한다.

즉 augment는 "활성 시너지 몰빵"만 지원하는 시스템이 아니다.
실패한 조합, 비주류 조합, 한 장의 좋은 유닛을 살리는 방향도 허용한다.

## MVP 구현 범위

- race/class 2축 시너지
- 단순 threshold 발동
- 단순 수치형 보너스
- 일부 augment가 시너지 강화형 또는 비활성 시너지 유닛 강화형으로 설계되는 문법

## 장기 규칙

- cross-axis synergy interaction
- anti-synergy / rivalry
- expedition/town synergy
- condition-heavy synergy hooks

## 밸런스 기준

- 시너지가 unit 개별 가치보다 너무 세면 안 된다.
- 반대로 시너지가 약해서 무시돼도 안 된다.
- augment가 항상 활성 시너지 쪽만 정답으로 만들면 안 된다.
- same-archetype variance가 시너지에 의해 완전히 사라지면 안 된다.

## launch 기준 연결

- paid launch에서는 `2 / 3 / 4` breakpoint를 사용한다.
- counter는 hard counter가 아니라 평균 `10~15%` swing의 soft counter를 목표로 한다.
- 위 숫자의 source-of-truth는 `docs/02_design/meta/synergy-breakpoints-and-soft-counters.md`다.
