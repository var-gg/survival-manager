# Scene Integrity Contract

- 상태: draft
- 최종수정일: 2026-04-05
- 단계: prototype

## 목적

first playable 씬이 marker-only 또는 wiring 누락 상태로 push되지 않도록, editor automation과 테스트가 공유하는 최소 계약을 정의한다.

## 검사 대상 씬

- `Boot`
- `Town`
- `Expedition`
- `Battle`
- `Reward`

## 최소 계약

### Boot
- `GameBootstrap` 존재
- `Main Camera` 존재
- `BootCanvas` 존재

### Town
- `TownCanvas` 존재
- `EventSystem` 존재
- `EventSystem`은 `InputSystemUIInputModule`을 사용하고 `StandaloneInputModule` fallback이나 수동 클릭 workaround를 포함하지 않는다.
- `TownScreenController` 존재
- 핵심 버튼 존재:
  - `RecruitButton1/2/3`
  - `RerollButton`
  - `SaveButton`
  - `LoadButton`
  - `DebugStartButton`

### Expedition
- `ExpeditionCanvas` 존재
- `EventSystem` 존재
- `EventSystem`은 `InputSystemUIInputModule`을 사용하고 `StandaloneInputModule` fallback이나 수동 클릭 workaround를 포함하지 않는다.
- `ExpeditionScreenController` 존재
- 핵심 버튼 존재:
  - `NextBattleButton`
  - `ReturnTownButton`

### Battle
- `BattleCanvas` 존재
- `EventSystem` 존재
- `EventSystem`은 `InputSystemUIInputModule`을 사용하고 `StandaloneInputModule` fallback이나 수동 클릭 workaround를 포함하지 않는다.
- `BattleScreenController` 존재
- 핵심 버튼 존재:
  - `Speed1Button`
  - `Speed2Button`
  - `Speed4Button`
  - `ContinueButton`

### Reward
- `RewardCanvas` 존재
- `EventSystem` 존재
- `EventSystem`은 `InputSystemUIInputModule`을 사용하고 `StandaloneInputModule` fallback이나 수동 클릭 workaround를 포함하지 않는다.
- `RewardScreenController` 존재
- 핵심 버튼 존재:
  - `Choice1Button`
  - `Choice2Button`
  - `Choice3Button`
  - `ReturnTownButton`

## Content 계약

sample content는 `Assets/Resources/_Game/Content/Definitions/**` 아래에 존재해야 하며, 최소 다음 타입 asset이 있어야 한다.

- stat
- race
- class
- archetype

## 자동 검증 목적

- scene installer가 만든 wiring이 실제로 남아 있는지 검사
- Boot -> Town 자동 진입이 유지되는지 검사
- Town -> Expedition -> Battle -> Reward -> Town 최소 1회전이 가능한지 검사
