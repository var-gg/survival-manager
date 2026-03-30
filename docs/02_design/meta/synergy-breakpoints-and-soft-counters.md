# 시너지 breakpoint와 soft counter

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/meta/synergy-breakpoints-and-soft-counters.md`
- 관련문서:
  - `docs/02_design/combat/synergy-system.md`
  - `docs/02_design/deck/roster-archetype-launch-scope.md`
  - `docs/02_design/systems/launch-content-scope-and-balance.md`

## 목적

이 문서는 launch 기준 synergy family, breakpoint, counter 규칙을 고정한다.

## synergy family

- race family: 3
- class family: 4
- 총 family: 7

family 수는 늘리기보다, 각 family가 `2 / 3 / 4` breakpoint에서 무엇을 주는지를 명확히 만드는 편을 우선한다.

## breakpoint 규칙

- `2-piece`: 방향 제시
- `3-piece`: 조합 정체성
- `4-piece`: 조합 헌신 보상

## 금지 규칙

- 2-piece에서 정답 조합처럼 과도하게 강한 효과
- 4-piece가 아니면 의미가 사라지는 설계
- 하나의 family가 offense / defense / utility를 전부 다 가져가는 만능화

## soft counter 원칙

- baseline swing은 평균 `10~15%` win-rate 차이 안으로 묶는다.
- 아이템, 전술, passive, augment로 뒤집을 여지가 있어야 한다.
- 특정 class family 하나만으로 승패가 고정되는 hard counter는 금지한다.

## class family 기준 관계

### Vanguard

- dive와 burst를 막는다.
- armor shred, dot, ignore-front comp에 약하다.

### Striker

- 노출된 Ranger/Mystic을 잘 잡는다.
- peel, guard redirect, hard CC에 약하다.

### Ranger

- 느린 frontline을 녹인다.
- dive와 blind성 접근 압박에 약하다.

### Mystic

- clump, armor stack, 긴 교전을 처벌한다.
- silence, mana deny, fast dive에 약하다.

## 운영 메모

- class canonical id는 `duelist`를 유지한다.
- 문서상 soft counter 설명에서만 `Striker` 용어를 사용한다.
- paid launch floor와 safe target 숫자는 `docs/02_design/systems/launch-content-scope-and-balance.md`가 소유한다.
