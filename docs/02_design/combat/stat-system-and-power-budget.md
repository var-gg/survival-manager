# 전투 스탯 체계와 파워 예산

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/combat/stat-system-and-power-budget.md`
- 관련문서:
  - `docs/02_design/combat/team-tactics-and-unit-rules.md`
  - `docs/02_design/combat/authoritative-replay-and-ledger.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`
  - `docs/04_decisions/adr-0015-build-compile-audit-pipeline.md`

## 목적

이 문서는 전투 스탯 언어, modifier stacking, source별 파워 예산을 같은 기준으로 묶는다.
목표는 content authoring, loadout compile, replay/log, sandbox validation이 같은 스탯 체계를 보게 하는 것이다.

## 기본 원칙

1. 플레이어 노출 스탯과 내부 시스템 스탯을 분리한다.
2. 빌드 차별화는 숫자보다 규칙 변화와 타깃팅 차이에서 더 크게 만든다.
3. 숫자 증폭은 `Flat -> Increased -> More` 순서로 고정한다.
4. legacy stat id는 migration 동안 읽되, 새 authored data는 canonical stat id만 쓴다.

## 공개 핵심 스탯

- 생존: `max_health`, `armor`, `resist`, `barrier_power`, `tenacity`, `heal_power`
- 공격: `phys_power`, `mag_power`, `attack_speed`, `crit_chance`, `crit_multiplier`, `phys_pen`, `mag_pen`
- 전개: `move_speed`, `attack_range`, `mana_max`, `mana_gain_on_attack`, `mana_gain_on_hit`, `cooldown_recovery`

## 툴/시스템 전용 스탯

- 공간/타깃팅: `aggro_radius`, `leash_distance`, `target_switch_delay`, `preferred_distance`, `protect_radius`
- 액션 타이밍: `attack_windup`, `cast_windup`, `attack_cooldown`, `reposition_cooldown`
- 표현/충돌: `projectile_speed`, `collision_radius`

## legacy alias

- `attack` -> `phys_power`
- `defense` -> `armor`
- `speed` -> `attack_speed`

이 alias는 migration 동안만 허용한다. editor validation은 이를 warning으로 보고, unsupported stat id만 error로 처리한다.

## modifier 규칙

- `Flat`: 절대값 추가
- `Increased`: 같은 버킷 내 합연산 퍼센트
- `More`: 제한된 곱연산 퍼센트
- `Rule Modifier`: 숫자로 환원되지 않는 행동 규칙 변화

`Rule Modifier`는 compile 결과와 provenance에 남기되, 이 패스에서는 전투 판정식 전체를 갈아엎지 않는다.

## 파워 예산 앵커

- positive trait 1개: 평균 전투력 `+4~6%`
- 완성 아이템 1개: `+8~12%`
- small passive node: `+1~2%`
- notable: `+4~6%`
- keystone: 직접 파워보다 규칙 변화 우선
- synergy tier: 영향받는 유닛 기준 `+8~10 / +15~18 / +22~26%`
- temporary augment: 대략 `+6~8 / +10~12 / +14~18%`
- permanent augment: MVP 슬롯 1개 기준 `+5~7%`

## 현재 구현 연결

- `SM.Core.Stats.StatKey`는 canonical stat id와 legacy alias를 함께 관리한다.
- `SM.Core.Stats.StatBlock`은 alias와 canonical stat을 같은 값으로 합산 해석한다.
- `SM.Combat.Model.BattleSkillSpec`, `SM.Combat.Model.BattleUnitLoadout`, `SM.Meta.Services.LoadoutCompiler`는 stat v2 필드를 읽는다.
- `SM.Editor.Validation.ContentDefinitionValidator`는 legacy stat id를 warning으로 기록한다.

## 후속 기준

- accuracy/evasion, rule modifier의 실제 resolver 확장, full balance sweep UI는 다음 패스에서 연다.
- 수치 변경 시 sandbox preview와 compile determinism 테스트를 함께 갱신한다.
