# First Playable Slice

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-02
- 소스오브트루스: `docs/02_design/systems/first-playable-slice.md`
- 관련문서:
  - `docs/02_design/systems/launch-content-scope-and-balance.md`
  - `docs/03_architecture/first-playable-balance-targets.md`
  - `docs/03_architecture/pruning-playbook.md`

## 목적

Loop D에서 실제로 밸런스하고 검증하는 V1 playable subset을 cap과 coverage quota로 고정한다.

## exact cap

- `UnitBlueprint = 12`
- `SignatureActive = 12`
- `SignaturePassive = 12`
- `FlexActive = 8`
- `FlexPassive = 8`
- `Affix = 24`
- `SynergyFamily = 8`
- `Augment = 16`

## coverage quota

- `FrontlineAnchor >= 2`
- `MeleePressure >= 2`
- `BacklineCarry >= 2`
- `MagicSource >= 2`
- `SupportSource >= 2`
- `DiveSource >= 1`
- `SummonSource >= 1`
- `AntiSwarmSource >= 1`
- `AntiSustainSource >= 1`
- `AntiControlSource >= 1`
- 모든 `ThreatPattern` 최소 1개 producer
- 모든 `CounterTool` 최소 1개 provider

## runtime source-of-truth

- asset: `Assets/Resources/_Game/Content/Definitions/FirstPlayable/first_playable_slice.asset`
- runtime projection: `FirstPlayableSliceDefinition`
- generated markdown artifact: `Logs/loop-d-balance/first_playable_slice.md`

## selection policy

1. coverage quota 충족 후보 우선
2. identity distinctiveness 우선
3. readability debt가 낮은 후보 우선
4. Loop C budget/rarity/topology 정합 후보 우선
5. cap 초과 후보는 `MoveOutOfV1` 또는 `MergeWithSibling`

## pool filtering

slice 밖 content는 아래 live path에서 노출되면 안 된다.

- recruit pool
- flex pool
- augment offer pool
- affix generation pool

debug/dev menu도 same filter를 따르되, `ParkingLotContentIds`는 별도 inspect 대상으로만 남긴다.

## 현재 운영 메모

- 현재 authored count 기준으로 unit 12, affix 24는 cap과 같다.
- slice pressure는 skill, augment, synergy family에 집중된다.
- current skill 분류의 canonical source는 authored `LockedSignature*`가 아니라 compiled slot이다.
