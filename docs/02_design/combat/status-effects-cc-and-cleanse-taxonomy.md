# 상태이상, CC, 정화 taxonomy

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/combat/status-effects-cc-and-cleanse-taxonomy.md`
- 관련문서:
  - `docs/02_design/combat/status-keyword-and-proc-rulebook.md`
  - `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
  - `docs/02_design/combat/stat-system-and-power-budget.md`
  - `docs/03_architecture/status-runtime-stack-and-cleanse-rules.md`
  - `docs/03_architecture/combat-state-and-event-model.md`

## 목적

이 문서는 launch floor에서 실제로 켜 둘 status family와 cleanse floor를 고정한다.
stack / refresh / proc / ownership의 깊은 규칙은 `status-keyword-and-proc-rulebook.md`가 소유한다.

## launch floor status family

| 분류 | status ids | 메모 |
| --- | --- | --- |
| control | `stun`, `root`, `silence`, `slow` | `stun`, `root`, `silence`는 hard control로 취급한다. |
| attrition / vulnerability | `burn`, `bleed`, `wound`, `sunder` | `burn`, `bleed`는 periodic pressure, `wound`, `sunder`는 취약화 축이다. |
| tactical mark | `marked`, `exposed` | focus fire와 execute window를 만든다. |
| defensive boon | `barrier`, `guarded`, `unstoppable` | 보호와 control break의 최소 바닥이다. |

## non-status rule modifier

아래 항목은 status family가 아니라 기존 rule / tactic modifier로 유지한다.

- `taunt`
- `intercept`
- `dash`
- `reposition`

이 항목들을 generic status로 승격하지 않는다.

## cleanse taxonomy

| cleanse id | 제거 대상 | 추가 효과 |
| --- | --- | --- |
| `cleanse_basic` | `slow`, `burn`, `bleed`, `wound`, `sunder`, `marked`, `exposed` | 없음 |
| `cleanse_control` | `root`, `silence` + `cleanse_basic` 대상 | 없음 |
| `break_and_unstoppable` | `stun`, `root`, `silence` 중 현재 적용된 hard control | 짧은 `unstoppable`과 control resist window 부여 |

## hard CC diminishing rule

- hard CC 종료 후 `1.5초` 동안 `control_resist 50%`를 부여한다.
- 이 창은 `stun`, `root`, `silence` 재적용 시간을 줄이는 용도다.
- `tenacity`는 duration reduction에만 반영한다.
- `tenacity` 적용 비율:
  - `stun`, `root`: `100%`
  - `silence`: `50%`
- displacement 계열 status family는 launch floor에서 열지 않는다.

## class identity 연결

| class family label | canonical class id | launch status 얼굴 |
| --- | --- | --- |
| Vanguard | `vanguard` | `guarded`, `barrier`, 짧은 `unstoppable` |
| Striker | `duelist` | `bleed`, `marked`, `execute` 연계 |
| Ranger | `ranger` | `slow`, `exposed`, `sunder` |
| Mystic | `mystic` | `burn`, `silence`, `cleanse`, `barrier` |

이 연결은 class id를 바꾸지 않고, 전투에서 읽히는 역할 얼굴만 고정한다.

## launch floor 운영 규칙

- status family는 concise floor를 유지한다.
- fear, charm, sleep, polymorph, knockback, suppress 같은 확장 family는 deferred다.
- cleanse는 status가 아닌 rule modifier를 제거하지 않는다.
- `Faction`은 status family나 cleanse 분류에 영향을 주지 않는다.

## rulebook 경계

- launch floor taxonomy: 이 문서
- stack / cap / refresh / proc attribution / summon ownership: `status-keyword-and-proc-rulebook.md`
- runtime resolver / replay event contract: `docs/03_architecture/status-runtime-stack-and-cleanse-rules.md`
