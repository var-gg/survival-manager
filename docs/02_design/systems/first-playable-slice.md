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

- `UnitBlueprint = 12` (core archetypes, specialist는 parked)
- `SignatureActive = 12`
- `SignaturePassive = 12`
- `FlexActive = 8`
- `FlexPassive = 8`
- `Affix = 24`
- `SynergyFamily = 7` (race 3 + class 4)
- `TemporaryAugment = 12`
- `PermanentAugment = 4`
- `PassiveBoard = 4` (class board)

## synergy grammar

- race families (Human / Undead / Beastkin): `2 / 4`
- class families (Vanguard / Striker / Ranger / Mystic): `2 / 3`
- class 4-piece는 current live subset에서 제거한다.
- race 3-piece는 current live subset에서 제거한다.
- 도달 불가능한 breakpoint는 current live subset에서 허용하지 않는다.

## first playable recruit set

first playable은 12 core grid를 완성한 상태로 테스트한다.
8 archetype 체계로는 race/class 문법을 제대로 읽을 수 없다.

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
- synergy family는 7로 고정. schema capacity는 넓어도 live subset은 7이다.
- augment는 temporary 12 + permanent 4 = 16으로 고정.
- class synergy는 `2 / 3` grammar로 동작하며, class 4-piece는 live subset에 없다.
