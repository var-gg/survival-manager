# loadout compiler와 battle snapshot

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`
- 관련문서:
  - `docs/03_architecture/data-model.md`
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`
  - `docs/02_design/combat/resource-cadence-loadout.md`
  - `docs/04_decisions/adr-0015-build-compile-audit-pipeline.md`

## 목적

이 문서는 authored definition, hero progression, squad blueprint, run overlay를 `BattleLoadoutSnapshot`으로 컴파일하는 기술 경계를 고정한다.

## 파이프라인

1. `SM.Content`가 authored asset을 runtime-friendly catalog로 번역한다.
2. `SM.Meta`가 hero loadout, progression, active run, blueprint를 소유한다.
3. `LoadoutCompiler`가 modifier package와 compile tag를 병합해 `BattleLoadoutSnapshot`을 만든다.
4. `SM.Combat`는 snapshot만 받아 deterministic simulation을 수행한다.

## 경계 규칙

- combat는 profile/save 모델을 직접 읽지 않는다.
- Unity는 compile 입력을 직접 조립하지 않는다.
- `BattleSetupBuilder`는 migration 동안만 호환 래퍼로 유지한다.
- encounter authoring이 닫히기 전까지 enemy setup smoke는 `BattleSetupBuilder` 경유를 허용한다.
- 이 경로를 새 source-of-truth로 승격하지 않는다.

## compile 계약

- persistence-friendly legacy slot string은 읽을 수 있다.
- compile 결과의 canonical slot은 `ActionSlotKind` 기준 6-slot topology다.
- legacy slot alias `active_core`, `core_active`, `utility_active`, `support`는 migration 과정에서만 normalize한다.
- compile hash는 skill coeff, delivery, target rule, crit 허용 여부, energy profile, behavior profile, entity kind를 포함한다.
- compile hash는 numeric package payload, rule package payload, team tactic profile, role instruction profile, normalized base stat, summon ownership profile을 포함한다.
- compile provenance는 source kind별 상세를 남겨야 하며, balance sweep와 audit가 그 provenance를 artifact로 읽을 수 있어야 한다.

## snapshot 포함 항목

- explicit 6-slot loadout
- energy state 초기값
- targeting/behavior profile
- `CombatEntityKind`
- optional `OwnershipLink`와 `SummonProfile`
- mutable flex slot persistence 정보

## validation과 sweep 연결

- launch floor authoring drift는 validator report로 먼저 막는다.
- compiled snapshot determinism과 battle determinism은 `docs/03_architecture/sim-sweep-and-balance-kpis.md` 기준으로 검증한다.
