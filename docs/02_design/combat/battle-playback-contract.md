# 전투 플레이백 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-06
- 소스오브트루스: `docs/02_design/combat/battle-playback-contract.md`
- 관련문서:
  - `docs/02_design/combat/battlefield-and-camera.md`
  - `docs/02_design/ui/battle-observer-ui.md`
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/02_design/combat/authoritative-replay-and-ledger.md`

## 목적

이 문서는 전투 플레이백(재생/탐색/리플레이) 시스템의 계약을 정의한다.
영상 플레이어 수준의 탐색(seek), 되감기(rewind), 리플레이(replay)를 지원하면서
QuickBattle과 InGame 모드를 우아하게 분리하는 것이 목표다.

## 핵심 구조

### BattleTimelineController (순수 C#)

- 시뮬레이터(`BattleSimulator`)를 보유하고 전진 재생을 구동한다.
- `List<BattleSimulationStep>`으로 매 스텝을 전수 녹화한다.
- 300 스텝 × 8유닛 read model = 메모리 무시 가능.

### Seek 메커니즘

- 뒤로 탐색 (`target ≤ furthestIndex`): `_recordedSteps[target]`를 직접 인덱싱한다. 즉시 반영된다.
- 앞으로 탐색 (`target > furthestIndex`): `_simulator.Step()` 루프로 빈 구간을 채운다.

- 탐색 후 `(steps[i-1], steps[i])` 쌍이 presentation에 전달된다.
- 뒤로 탐색해도 시뮬레이터 상태는 변하지 않는다 (녹화된 read model만 재사용).

### 시간 진행

- `TryAdvance(deltaTime)`: 프레임마다 호출. 내부 누적기가 `fixedStepSeconds` 이상이면 다음 스텝 소비.
- `PlaybackSpeed`: 1x, 2x, 4x 등. 범위 [0.25, 8].
- `IsPaused`: true면 누적기 정지.

### 리플레이 vs 새 전투

- **리플레이**: 스크러버를 0으로 드래그하여 처음부터 재생. 녹화된 데이터 재사용.
- **새 전투 (rebattle)**: `RebattleNewSeed()`로 새 시드 생성. 시뮬레이터와 타임라인 모두 재초기화.
- **같은 시드 재시작**: `RestartSameSeed()`로 동일 시드 재실행. 타임라인 리셋.

## BattlePlaybackPolicy

- `CanPause`: `QuickBattle`에서는 `always`, `InGame`에서는 `isFinished`
- `CanSeek`: `QuickBattle`에서는 `always`, `InGame`에서는 `isFinished`
- `CanControlSpeed`: `QuickBattle`에서는 `always`, `InGame`에서는 `isFinished`
- `CanReplay`: `QuickBattle`에서는 `always`, `InGame`에서는 `isFinished`

- InGame 모드에서 전투 진행 중에는 모든 플레이백 조작이 비활성화된다.
- 전투 종료 후 리플레이 모드에서 전체 기능이 해금된다.

## BattleScreenView scrubber

- battle shell의 scrubber는 `BattleScreenView` 내부 UITK pointer callback으로 구현한다.
- `ProgressTrack`의 포인터 위치를 normalized 0..1로 변환해 `BattleScreenController.HandleScrubberSeek()`에 전달한다.
- interactable 정책은 `BattleScreenView.SetScrubberInteractable(bool)`로 제어한다.
- drag 중에는 fill width를 임시 반영하고, drag 외 구간은 presenter state progress를 따른다.

## 불변 규칙

- 타임라인은 presentation 계층 소속이다. `SM.Combat`를 수정하지 않는다.
- `BattleSimulator`는 forward-only다. 되감기는 녹화된 read model로 해결한다.
- 탐색이 카메라 위치에 영향을 주지 않는다 (공간 독립).
- 새 전투 시작만 카메라를 기본 위치로 리셋한다.
