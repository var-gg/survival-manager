# synergy and augment taxonomy

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/meta/synergy-and-augment-taxonomy.md`
- 관련문서:
  - `docs/02_design/meta/synergy-breakpoints-and-soft-counters.md`
  - `docs/02_design/meta/synergy-family-catalog.md`
  - `docs/02_design/meta/augment-system.md`
  - `docs/02_design/meta/augment-catalog-v1.md`

## 목적

이 문서는 synergy와 augment가 각각 무엇을 건드리는 시스템인지 역할 경계를 고정한다.

## 분리 규칙

- synergy: 조합 기준선, count grammar, build guide
- augment: 한 run 전체의 규칙을 휘는 글로벌 레버
- affix: 개별 unit / item 최적화

## synergy policy

- 한 unit은 synergy tag를 최대 `2개`만 가진다.
- breakpoint는 계속 `2 / 3 / 4`다.
- 현재 committed live family는 7개를 유지한다.
- schema capacity는 `12~16 family`까지 수용하지만, runtime validator와 launch floor는 현재 live subset을 우선한다.

## augment bucket

### `NeutralCombat`

- front/back / tempo / burst / sustain 같은 폭넓은 전투 레버

### `EconomyRoster`

- recruit, reroll, retrain, bench, loot value 같은 운영 레버

### `SynergyLinked`

- 현재 팀 조합과 태그 count를 밀어주는 레버

### `WildcardRisk`

- 큰 고점과 리스크를 같이 주는 방향 강제 레버

## augment offer rule

- 항상 3안
- 기본 분배:
  - on-board or mildly aligned 1
  - flex 1
  - wildcard or economy 1
- 완전 dead option 3개는 금지

## overlap 금지

- synergy와 augment가 같은 축을 같은 강도로 중복 복제하면 안 된다.
- augment 하나가 synergy 4-piece identity를 통째로 대체하면 안 된다.
- 작은 stat buff만 가진 augment를 양산하지 않는다.
