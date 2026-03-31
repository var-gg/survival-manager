# 전투 메커니즘 용어집

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/combat/combat-mechanics-glossary.md`
- 관련문서:
  - `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
  - `docs/03_architecture/combat-state-and-event-model.md`
  - `docs/03_architecture/status-runtime-stack-and-cleanse-rules.md`

## 목적

crit, dodge, block, armor, stability를 v1 기준으로 명확히 고정한다.

## 정의

- `Crit`
  - 공격자 측 기대값 상승
  - v1에서는 public stat `crit_chance`, `crit_multiplier`를 사용한다.
- `Dodge / Evasion`
  - 드문 full avoid
  - 저체급/민첩 archetype에만 높은 값을 준다.
  - v1에서는 hidden behavior coefficient다.
- `Block / Guard`
  - 방어자 identity
  - v1에서는 facing arc 없이 `mitigation + internal cooldown`만 쓴다.
  - v1에서는 hidden behavior coefficient다.
- `Armor / Resistance`
  - 가장 예측 가능한 기본 방어
  - public stat이다.
- `Tenacity / Stability`
  - tenacity는 CC duration 쪽, stability는 hit reaction/guard identity 쪽 숨은 계수다.

## resolution order

1. target validity / range
2. dodge
3. crit
4. block / guard
5. armor / resistance
6. on-hit / status

## cap 정책

- dodge는 hard cap 또는 낮은 authored baseline만 허용한다.
- block chance는 internal cooldown과 함께만 쓴다.
- dodge와 block을 동시에 높게 올리는 archetype은 만들지 않는다.
- accuracy / hit rate는 v1 범위에서 제외한다.
