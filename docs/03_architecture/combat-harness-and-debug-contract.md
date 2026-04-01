# 전투 하네스와 디버그 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/03_architecture/combat-harness-and-debug-contract.md`
- 관련문서:
  - `docs/03_architecture/editor-sandbox-tooling.md`
  - `docs/03_architecture/validation-and-acceptance-oracles.md`
  - `docs/02_design/combat/battle-presentation-contract.md`
  - `docs/02_design/combat/combat-spatial-contract.md`
  - `docs/02_design/combat/targeting-and-ai-vocabulary.md`
  - `docs/02_design/combat/summon-ownership-and-deployables.md`

## 목적

전투 contract 변경을 scene gizmo, sandbox preset, smoke loop로 반복 검증하는 기준을 정의한다.

## 최소 시나리오

- `A1 authority validation`
- `A2 cadence baseline`
- `A3 target lock / anti-jitter`
- `A4 backline exposed`
- `A5 summon ownership`
- `A6 direct ally targeting vs team aura`
- `A7 summon-chain block`

## gizmo 목록

- head anchor line + cap
- navigation radius
- separation radius
- combat reach / preview range
- preferred range band
- engagement slot ring + slot markers
- current target line
- current selector / fallback state
- retarget lock remaining time
- frontline guard radius
- AoE cluster radius
- entity kind

## acceptance

- authority / summon-chain invalid content가 import 단계에서 실패한다.
- 첫 signature cast가 기본 시나리오에서 대체로 `5~9s`에 나온다.
- target lock이 frame 단위로 흔들리지 않는다.
- `BacklineExposedEnemy` selector가 frontline 붕괴 후에만 caster/healer를 잡는다.
- summon kill은 mirrored owner credit를 남기되 owner energy와 generic on-kill proc를 주지 않는다.
- direct ally heal은 summon을 기본 target으로 삼지 않고 team aura는 summon을 포함한다.
- `ui.battle.*` missing key가 0건이다.

## known non-goals

- final animation polish
- advanced tactical AI beyond Loop A vocabulary
- stamina / morale / equipment 신규 대형 시스템
