# Recruitment Contract

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/meta/recruitment-contract.md`
- 관련문서:
  - `docs/02_design/meta/retrain-contract.md`
  - `docs/02_design/meta/economy-protection-contract.md`
  - `docs/03_architecture/recruit-offer-schema.md`
  - `docs/05_setup/recruitment-and-retrain-harness.md`

## 목적

Loop B 기준 recruit phase를 `4-slot pack + on-plan/protected guarantee + scout/pity` 계약으로 고정한다.

## V1 allowed

- recruit offer pack은 정확히 `StandardA`, `StandardB`, `OnPlan`, `Protected` 4슬롯만 사용한다.
- pack마다 blueprint 중복은 금지한다.
- owned blueprint는 recruit pool에서 제외한다.
- on-plan 후보가 존재하면 pack마다 최소 1개의 on-plan 결과를 보여준다.
- `Protected` 슬롯은 pity tier floor를 따른다.
- recruit phase 진입 시 무료 refresh `1회`를 제공한다.
- paid refresh cost는 `2 -> 4 -> 6 cap`이고 phase 종료 시 reset된다.
- `Scout`는 다음 refresh 1회에만 적용되는 방향성 bias다.

## V1 forbidden

- recruit offer에 owned blueprint duplicate를 노출하는 것
- on-plan 후보가 있는데 `OnPlan` 슬롯을 off-plan으로 생성하는 것
- `Protected` 슬롯이 pity floor를 위반하는 것
- recruit slot lock / refresh lock
- scout로 exact unit을 지정하는 것
- refresh escalation이 phase를 넘어 누적되는 것

## Offer pack topology

| 슬롯 | 역할 | 생성 규칙 |
| --- | --- | --- |
| `StandardA` | 일반 모집 | 전체 pool weighted random |
| `StandardB` | 일반 모집 | 전체 pool weighted random |
| `OnPlan` | 최소 1개 plan rescue | `OnPlan` pool 우선, 없으면 `Bridge`, 그것도 없으면 최고점 fallback |
| `Protected` | pity tier 보호 | pity floor 이상 후보 중 weighted/random + on-plan/bridge 우선 |

## Candidate scoring

| 규칙 | 점수 |
| --- | --- |
| 다음 breakpoint를 정확히 채움 | `+3` |
| top synergy tag 공유 + soft saturation 전 | `+2` |
| formation deficit 해소 | `+2` |
| augment hook 일치 | `+1` |
| scout directive 일치 | `+2` |
| 과잉 role bucket 추가 | `-2` |

## Plan fit bucket

| 합계 | 분류 |
| --- | --- |
| `>= 4` | `OnPlan` |
| `2~3` | `Bridge` |
| `<= 1` | `OffPlan` |

## Refresh와 scout

| 항목 | 계약 |
| --- | --- |
| 무료 refresh | recruit phase당 `1회` |
| paid refresh 1회차 | `2 Gold` |
| paid refresh 2회차 | `4 Gold` |
| paid refresh 3회차 이후 | `6 Gold` cap |
| scout cost | `35 Echo` |
| scout 사용 제한 | recruit phase당 `1회` |
| scout 영향 범위 | 다음 pack의 `OnPlan` 슬롯 scoring only |
| pity 진행 | player가 실제로 본 pack 수 기준 |

## Pity invariants

- `3` pack 연속 Rare+ 부재면 다음 `Protected` 슬롯은 최소 `Rare`
- `8` pack 연속 Epic 부재면 다음 `Protected` 슬롯은 최소 `Epic`
- `OnPlan pity`는 별도 카운터를 두지 않는다.
- `OnPlan` 슬롯과 `Protected` 슬롯이 둘 다 off-plan인 pack은 recoverable 후보가 있을 때 금지한다.

## Fallback rules

- `OnPlan` 후보가 없으면 `Bridge` 최고점으로 내려간다.
- `Bridge`도 없으면 전체 후보 최고점으로 내려간다.
- `Protected`는 pity floor를 먼저 만족하고, 같은 floor 안에서는 `OnPlan > Bridge > OffPlan` 우선순위를 따른다.
- native coherence와 plan coherence가 충돌하면 native coherence를 우선한다.

## Offer preview visibility

offer 카드 구매 전 아래를 반드시 보여준다.

- unit name / tier / tags / formation line
- signature active / signature passive 요약
- rolled `FlexActive`, `FlexPassive`
- gold cost
- `OnPlan`, `Protected`, `Scout` badge

## 예시

### 예시 1. 기본 pack

- 현재 roster가 `guardian`, `hunter`, `hexer`, `shaman`이고 backline physical deficit가 있다.
- `OnPlan` 슬롯은 `marksman`을 뽑고, `Protected`는 pity가 없으면 `Rare` 이하 weighted pick을 수행한다.
- 결과 pack 전체가 off-plan이면 생성 자체가 실패다.

### 예시 2. scout 적용

- player가 `Scout(Backline)`을 사용한다.
- 다음 refresh 1회에서만 `OnPlan` 슬롯 scoring에 `+2` bias가 들어간다.
- `Protected` 슬롯의 rare/epic pity는 그대로 유지된다.

## Loop C rarity boundary

recruitment의 canonical rarity는 계속 `RecruitTier`다.

- `RecruitTier`
  - offer generation
  - pity
  - save serialization
  - player-facing badge
- `ContentRarity`
  - validator
  - compiler
  - runtime combat template
  - battle snapshot
  - debug overlay

일반 town recruit card에는 아래만 노출한다.

- `RecruitTier`
- role fantasy
- key tag
- counter hint
- Gold cost / `OnPlan` / `Protected` / `Scout`

`ContentRarity`, budget final score, derived delta, forbidden drift는
`RecruitmentSandboxWindow` 같은 dev inspect에서만 노출한다.
