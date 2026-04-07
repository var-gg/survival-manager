# 출시 기준 로스터와 아키타입 스코프

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `docs/02_design/deck/roster-archetype-launch-scope.md`
- 관련문서:
  - `docs/02_design/systems/launch-content-scope-and-balance.md`
  - `docs/02_design/systems/launch-floor-content-matrix.md`
  - `docs/02_design/meta/synergy-breakpoints-and-soft-counters.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`

## 목적

이 문서는 paid launch 기준으로 roster family를 얼마나 유지하고, archetype package를 어디까지 distinct하게 가져갈지 정리한다.

## 기본 원칙

- races는 3으로 유지한다.
- classes는 4로 유지한다.
- class canonical id는 `vanguard / duelist / ranger / mystic`를 유지한다.
- 문서상 역할 family 이름으로는 `Vanguard / Striker / Ranger / Mystic`를 사용한다.
- `duelist`는 runtime/content canonical id이고, `Striker`는 역할 설명용 라벨이다.

## 출시 기준 archetype 구조

### paid launch floor

- `12 archetypes`
- `3 races x 4 classes`의 모든 조합을 최소 1개씩 채운다.

### paid launch safe target

- `16 archetypes`
- `12 core archetypes`
- `4 specialist archetypes`

## core와 specialist 구분

### core archetype

- race/class 조합 문법을 설명하는 기본 단위
- 시너지와 직무를 읽게 하는 archetype
- scope tag는 `core`

### specialist archetype

- 규칙 파괴형, 하이브리드, exception slot
- 기본 12종을 덮어쓰지 않고 recombination 축을 늘리는 archetype
- scope tag는 `specialist`

## character layer

- launch floor의 `12 core archetypes`는 이번 단계에서 `12 core characters`와 1:1로 대응한다.
- `Character`는 story/identity layer이고, `Archetype`는 전투 패키지다.
- 같은 race/class 조합 안에서도 이후 패스에서 다른 캐릭터를 추가할 수 있어야 하므로, archetype만으로 캐릭터를 대체하지 않는다.

## class role family

### Vanguard

- 진입, guard, taunt, protect, 공간 점유

### Striker

- 접근, 고립 타깃 제거, execution, dive

### Ranger

- 지속 단일딜, 후열 화력, kiting

### Mystic

- burst, sustain, control, debuff

## identity package 기준

아키타입 1개를 풀 신규 모델/리깅으로 해석하지 않는다.
런치 기준의 최소 package는 아래를 따른다.

- portrait bust 1
- roster/card icon 1
- combat silhouette variant 1
- weapon prop 1
- class 기반 attack/cast animation binding
- race 기반 VFX tint / hit FX
- signature effect 1

## 재사용 규칙

- animation은 class family 기준으로 재사용한다.
- race는 tint, silhouette accent, hit FX로 정체성을 추가한다.
- specialist 4종만 silhouette 차이를 강하게 준다.
- Unity presentation ref는 archetype truth에 넣지 않고 presentation layer가 별도로 소유한다.

## authoring acceptance

- paid launch floor를 주장하려면 `core` archetype 12개가 모두 존재해야 한다.
- `docs/02_design/systems/launch-floor-content-matrix.md`에 적힌 12개 id와 race/class 조합이 그대로 닫혀 있어야 한다.
- paid launch safe target을 주장하려면 `specialist` archetype 4개가 추가되어야 한다.
- 각 archetype은 `ScopeKind`, `RoleFamilyTag`, `PrimaryWeaponFamilyTag`를 authored field로 가져야 한다.

## build lane closure 규칙

- 각 class는 `3 build lane`을 가진다.
  - `vanguard`: `hold_guard / anti_dive_peel / attrition_hold`
  - `duelist`: `burst_execute / sticky_flank / sustain_break`
  - `ranger`: `longshot_focus / mobile_kite / armor_break_focus`
  - `mystic`: `sustain_cleanse / control_attrition / persistent_pressure`
- 각 archetype은 baseline lane 1개와 alt lane 1개를 가진다.
- lane variance는 새 class/race/site 추가가 아니라 support modifier, reward routing, affix/augment bias로 만든다.

## enemy usage closure 규칙

- 12 core archetype은 player roster와 enemy squad 양쪽에서 모두 사용한다.
- 각 archetype은 enemy squad에 최소 2회, elite/boss composition에는 최소 1회 등장하는 것을 floor로 본다.
- 이 규칙의 runtime source-of-truth는 `EnemySquads/*.asset`과
  `docs/02_design/systems/launch-encounter-variety-and-answer-lane-matrix.md`다.
