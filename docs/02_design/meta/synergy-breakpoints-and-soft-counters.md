# 시너지 breakpoint와 soft counter

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-02
- 소스오브트루스: `docs/02_design/meta/synergy-breakpoints-and-soft-counters.md`
- 관련문서:
  - `docs/02_design/deck/roster-archetype-launch-scope.md`
  - `docs/02_design/systems/launch-content-scope-and-balance.md`
  - `docs/02_design/meta/synergy-family-catalog.md`
  - `docs/02_design/meta/synergy-and-augment-taxonomy.md`

## 목적

이 문서는 launch 기준 synergy family, breakpoint, counter 규칙을 고정한다.

## synergy family

- race family: 3
- class family: 4
- 총 family: 7

family 수는 늘리기보다, 각 family의 breakpoint에서 무엇을 주는지를 명확히 만드는 편을 우선한다.
schema capacity는 더 넓게 열어 두더라도, current live subset validator는 계속 7 family를 기준으로 본다.

## breakpoint 규칙

race families와 class families는 서로 다른 breakpoint grammar를 쓴다.

### race family (Human / Undead / Beastkin)

- `2-piece`: 방향 제시
- `4-piece`: 조합 payoff (full commit)
- 12 core archetype 기준 race당 4명이므로 4-piece는 race 올인으로 도달 가능하다.

### class family (Vanguard / Striker / Ranger / Mystic)

- `2-piece`: 방향 제시
- `3-piece`: 조합 payoff (full class commit)
- 12 core archetype 기준 class당 3명이므로 3-piece가 class 올인의 최대다.
- class 4-piece는 current live subset에서 제거한다. duplicate 금지 + class당 3명이므로 도달 불가능하다.

도달 불가능한 breakpoint는 current live subset에서 허용하지 않는다.

exact 수치 payload와 stacking rule은 `docs/02_design/meta/synergy-family-catalog.md`가 소유한다.

## 금지 규칙

- 2-piece에서 정답 조합처럼 과도하게 강한 효과
- 4-piece가 아니면 의미가 사라지는 설계

## Loop C addendum

- race family: `2-piece = Standard`, `4-piece = Major`.
- class family: `2-piece = Standard`, `3-piece = Major`.
- race 3-piece는 삭제한다.
- class 4-piece는 future reserve로 내리고 current live subset에서 제거한다.
- topology 밖 새 counter lane은 synergy로 추가하지 않는다.
- 하나의 family가 offense, defense, utility를 전부 다 가져가는 만능화 금지.
- summon/deployable을 breakpoint count에 넣는 설계 금지.

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
- silence, exposed-backline punish, fast dive에 약하다.
