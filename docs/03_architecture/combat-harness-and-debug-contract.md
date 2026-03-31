# 전투 하네스와 디버그 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/03_architecture/combat-harness-and-debug-contract.md`
- 관련문서:
  - `docs/03_architecture/editor-sandbox-tooling.md`
  - `docs/03_architecture/validation-and-acceptance-oracles.md`
  - `docs/02_design/combat/battle-presentation-contract.md`
  - `docs/02_design/combat/combat-spatial-contract.md`

## 목적

전투 contract 변경을 scene gizmo, sandbox preset, smoke loop로 반복 검증하는 기준을 정의한다.

## 최소 시나리오

- `1v1 melee`
- `3v3 melee`
- `melee vs ranged`
- `mixed squad`
- `large footprint placeholder`

## gizmo 목록

- head anchor line + cap
- navigation radius
- separation radius
- combat reach / preview range
- preferred range band
- engagement slot ring + slot markers

## acceptance

- overhead UI가 screen-space로 보인다.
- 3v3 melee가 target pivot blob으로 무너지지 않는다.
- ranged/mage가 최소한의 backstep 또는 short disengage를 보인다.
- large footprint placeholder가 spacing을 유지한다.
- `ui.battle.*` missing key가 0건이다.

## known non-goals

- final animation polish
- advanced tactical AI
- stamina / morale / equipment 신규 대형 시스템
