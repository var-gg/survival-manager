# 드롭 resolve와 ledger 파이프라인

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/03_architecture/drop-resolution-and-ledger-pipeline.md`
- 관련문서:
  - `docs/02_design/meta/drop-table-rarity-bracket-and-source-matrix.md`
  - `docs/02_design/meta/reward-economy.md`
  - `docs/03_architecture/replay-persistence-and-run-audit.md`

## 목적

이 문서는 battle result가 automatic loot와 reward ledger로 흘러가는 단일 파이프라인을 정의한다.

## canonical model

- `DropTableDefinition`
- `RewardSourceDefinition`
- `LootBundleDefinition`
- `TraitTokenDefinition`
- `LootBundleResult`
- `LootEntry`

## resolve 흐름

1. battle 종료 후 run state가 현재 `RewardSourceId`와 `BattleSeed`를 가진다.
2. `LootResolutionService.TryResolveBundle(...)`이 source와 seed를 받아 automatic loot bundle을 계산한다.
3. guaranteed drop과 weighted drop을 합친다.
4. 같은 source를 가진 `LootBundleDefinition` bonus bundle을 추가한다.
5. 결과를 currency, inventory, reward ledger, inventory ledger에 기록한다.
6. 그 뒤 Reward scene의 3-choice card가 별도 채널로 열린다.

## deterministic 규칙

- 같은 `RewardSourceId` + 같은 `BattleSeed`는 같은 loot bundle을 반환해야 한다.
- weighted drop은 seed 기반 `System.Random` 하나로 계산한다.
- source/tag mapping은 validator에서 중복과 누락을 막는다.

## ledger ownership

- currency 증감은 `SaveProfile.Currencies`가 가진다.
- item 획득은 `InventoryItemRecord` + `InventoryLedgerEntryRecord`가 기록한다.
- automatic loot 전체 요약은 `RewardLedgerEntryRecord`로 남긴다.
- Reward scene은 automatic loot summary를 read-only로 보여 준다.

## source 운영 규칙

- skirmish / elite / boss / shrine_event / extract / salvage는 모두 `RewardSourceDefinition`으로 catalog화한다.
- `UsesRewardCards`는 automatic loot와 별개로 reward card UI를 여는지 나타낸다.
- `RewardSourceKindValue.ExtractEndRun`은 extract bonus bundle을 추가로 붙일 수 있다.

## validator / test oracle

- missing source entries
- unreachable rarity bands
- duplicate source-tag mappings
- same seed/context => same loot bundle
- reward scene이 automatic loot 이후에도 기존 3-card choice를 유지하는지 검증
