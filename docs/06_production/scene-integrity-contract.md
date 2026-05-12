# Scene Integrity Contract

- 상태: draft
- 최종수정일: 2026-04-06
- 단계: prototype

## 목적

first playable 씬이 marker-only 또는 runtime wiring 누락 상태로 push되지 않도록, editor automation과 테스트가 공유하는 최소 계약을 정의한다.

## 검사 대상 씬

- `Boot`
- `Town`
- `Expedition`
- `Battle`
- `Reward`

internal visual sandbox, asset intake, authoring preview scene는 이 계약의 검사 대상이 아니다. 이들은 build settings 진입 경로와 분리된 editor-only verification lane으로 취급한다.

## 최소 계약

### Boot

- `GameBootstrap` 존재
- `Main Camera` 존재
- `BootCanvas` 존재

### Town

- `TownRuntimeRoot` 존재
- `TownRuntimePanelHost` 존재
- `TownRuntimePanelHost`는 `RuntimePanelHost`와 current backend component를 가진다
- `TownScreenController` 존재
- `TownScreen.uxml`은 최소 다음 named element를 가진다
  - `ReturnToStartButton`
  - `ExpeditionButton`
  - `DeployButton_FrontTop`
  - `TeamPostureButton`

### Expedition

- `ExpeditionRuntimeRoot` 존재
- `ExpeditionRuntimePanelHost` 존재
- `ExpeditionRuntimePanelHost`는 `RuntimePanelHost`와 current backend component를 가진다
- `ExpeditionScreenController` 존재
- `ExpeditionScreen.uxml`은 최소 다음 named element를 가진다
  - `NextBattleButton`
  - `DeployButton_BackCenter`
  - `TeamPostureButton`

### Battle

- `BattleRuntimeRoot` 존재
- `BattleRuntimePanelHost` 존재
- `BattleRuntimePanelHost`는 `RuntimePanelHost`와 current backend component를 가진다
- `BattleScreenController` 존재
- `BattlePresentationRoot`, `BattleStageRoot`, `ActorOverlayCanvas`, `ActorOverlayRoot`, `BattleCameraRoot` 존재
- `BattleScreen.uxml`은 최소 다음 named element를 가진다
  - `PauseButton`
  - `SettingsButton`
  - `SettingsPanel`
  - `ProgressFill`

### Reward

- `RewardRuntimeRoot` 존재
- `RewardRuntimePanelHost` 존재
- `RewardRuntimePanelHost`는 `RuntimePanelHost`와 current backend component를 가진다
- `RewardScreenController` 존재
- `RewardScreen.uxml`은 최소 다음 named element를 가진다
  - `ChoiceCard1Button`
  - `ChoiceCard2Button`
  - `ChoiceCard3Button`
  - `ReturnTownButton`

## EventSystem 계약

- `Active Input Handling = Input System` 또는 `Both`인 scene에서는 `EventSystem + InputSystemUIInputModule`을 요구한다.
- pure legacy Input Manager + pure UITK-only scene에서는 scene component가 필수는 아니다.
- 현재 repo는 Input System과 mixed UI mode를 사용하므로 first playable scene contract에서는 계속 `EventSystem + InputSystemUIInputModule`을 provision / 검증한다.
- `StandaloneInputModule` fallback이나 수동 클릭 workaround는 허용하지 않는다.

## Content 계약

sample content는 `Assets/Resources/_Game/Content/Definitions/**` 아래에 존재해야 하며, 최소 다음 타입 asset이 있어야 한다.

- stat
- race
- class
- archetype

## 자동 검증 목적

- scene installer가 만든 runtime root와 binding이 실제로 남아 있는지 검사
- Boot start screen이 `Start Local Run` 대기 상태를 유지하는지 검사
- Town -> Expedition -> Battle -> Reward -> Town 최소 1회전이 가능한지 검사
- scene YAML contract와 UXML named element contract가 함께 유지되는지 검사
