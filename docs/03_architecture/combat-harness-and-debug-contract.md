# 전투 하네스와 디버그 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-21
- 소스오브트루스: `docs/03_architecture/combat-harness-and-debug-contract.md`
- 관련문서:
  - `docs/03_architecture/editor-sandbox-tooling.md`
  - `docs/03_architecture/validation-and-acceptance-oracles.md`
  - `docs/05_setup/unity-long-running-workloads.md`
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

## normal lane vs debug lane

- normal playable lane(F3 off):
  - current actor ring
  - current target reticle
  - current actor source -> target line
  - selected tactical card
  - selected-only range / slot / tether / guard surface
- debug smoke lane(F3 on):
  - 모든 actor target line
  - raw selector / fallback / retarget lock
  - full anchor / radius / slot / cluster truth
  - OnGUI inspector overlay

- harness는 두 lane이 서로 다른 결과를 말하지 않는지 확인해야 한다.
- normal lane이 전투 의미 이해를 위해 debug overlay에 의존하면 실패다.

## acceptance

- authority / summon-chain invalid content가 import 단계에서 실패한다.
- 첫 signature cast가 기본 시나리오에서 대체로 `5~9s`에 나온다.
- target lock이 frame 단위로 흔들리지 않는다.
- `BacklineExposedEnemy` selector가 frontline 붕괴 후에만 caster/healer를 잡는다.
- summon kill은 mirrored owner credit를 남기되 owner energy와 generic on-kill proc를 주지 않는다.
- direct ally heal은 summon을 기본 target으로 삼지 않고 team aura는 summon을 포함한다.
- `ui.battle.*` missing key가 0건이다.
- seek/reset/replay 시 floating text, hit flash, impact cue가 재발화하지 않는다.
- x2/x4 speed에서도 transient cue가 smear되지 않는다.

## known non-goals

- final animation polish
- advanced tactical AI beyond Loop A vocabulary
- stamina / morale / equipment 신규 대형 시스템

## Loop C dev overlay

combat harness와 sandbox는 아래 governance view를 제공해야 한다.

- team 8-lane coverage report
- `None` 또는 `Light` lane 경고
- selected unit governance summary
  - `ContentRarity`
  - role profile
  - budget final score
  - derived delta
  - declared threat/counter
  - forbidden drift

위 정보는 dev/debug view 전용이며 player-facing battle UI에 그대로 노출하지 않는다.

## Loop C scenario set

`BalanceSweepScenarioFactory`는 아래 threat scenario를 가져야 한다.

- `ArmorFrontlineScenario`
- `ResistanceShellScenario`
- `GuardBulwarkScenario`
- `EvasiveSkirmishScenario`
- `ControlChainScenario`
- `SustainBallScenario`
- `DiveBacklineScenario`
- `SwarmFloodScenario`

## Loop D telemetry/readability overlay

combat sandbox와 battle debug view는 Loop D에서 아래를 추가로 보여줘야 한다.

- current 1초 salience weight
- 최근 5초 major/critical timeline
- unexplained event 누적치
- top damage source / top decision reason
- decisive moments summary
- current readability violation 목록

readability gate는 참고 리포트가 아니라 dev/harness/CI fail 조건이다.

## Loop D 실행 lane

- Loop D slice/readability/balance artifact는 기본 `test-edit`나 장시간 menu callback에 넣지 않는다.
- 기본 evidence 회수 순서는 `loopd-slice -> loopd-purekit -> loopd-systemic -> loopd-runlite`다.
- release floor wrapper도 같은 shard 순서를 유지하고, Loop D를 default short lane으로 재흡수하지 않는다.
- full smoke가 정말 필요할 때만 `pwsh -File tools/unity-bridge.ps1 loopd-smoke`를 사용한다.
- `LoopDTelemetryAndBalanceTests`의 장시간 smoke는 manual lane 전용이며, default EditMode suite 통과 기준이 아니다.
- symmetric mirror 4v4 timeout/draw policy는 `LoopASymmetricMirrorPolicyTests`의 explicit `ManualLoopD` probe로 추적한다. `BattleResolutionTests.LoopA_4v4_AsymmetricBattleEndsBeforeTimeout`는 FastUnit deterministic end oracle일 뿐 mirror draw/balance policy closure가 아니다.
- Quick Battle은 debug/smoke lane이고 authored campaign acceptance를 대체하지 않는다.
