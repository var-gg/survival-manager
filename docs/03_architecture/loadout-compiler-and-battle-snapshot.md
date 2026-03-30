# loadout compiler와 battle snapshot

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`
- 관련문서:
  - `docs/03_architecture/data-model.md`
  - `docs/03_architecture/combat-runtime-architecture.md`
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
