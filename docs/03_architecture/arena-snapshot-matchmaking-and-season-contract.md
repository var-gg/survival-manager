# arena snapshot, matchmaking, season 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/03_architecture/arena-snapshot-matchmaking-and-season-contract.md`
- 관련문서:
  - `docs/02_design/meta/pvp-ruleset-and-arena-loop.md`
  - `docs/02_design/meta/pvp-boundary.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`
  - `docs/03_architecture/replay-persistence-and-run-audit.md`

## 목적

이 문서는 async arena를 위한 snapshot, local matchmaking, replay, season persistence contract를 정의한다.

## canonical model

- `ArenaDefenseSnapshot`
- `ArenaBlueprintSlot`
- `ArenaMatchRecord`
- `ArenaSeasonState`
- `ArenaRewardLedgerEntry`

대응 persistence record:

- `ArenaDefenseSnapshotRecord`
- `ArenaBlueprintSlotRecord`
- `ArenaMatchRecordRecord`
- `ArenaSeasonStateRecord`
- `ArenaRewardLedgerEntryRecord`

## snapshot 규칙

- arena는 compiled battle snapshot만 소비한다.
- snapshot은 permanent augment, trait, item, skill, passive board를 포함한다.
- snapshot은 temporary augment provenance를 포함하면 안 된다.
- freshness check는 `CompileVersion`과 `ContentVersion` 두 축으로 수행한다.

## local/offline arena scaffold

`ArenaSimulationService`는 이번 패스에서 아래만 제공한다.

- defense snapshot registration
- snapshot freshness/version check
- rating band 기반 opponent candidate `3`명 선택
- instant simulation
- replay bundle generation
- match record와 local rating delta 계산

## rating과 season ownership

- rating mutation은 local arena meta service가 소유한다.
- launch floor season cadence는 `4주`다.
- `ArenaSeasonState`는 `SeasonId`, `StartedAtUtc`, `EndsAtUtc`, `CurrentRating`, `WeeklyChestClaimed`, `IsActive`를 보존한다.

## replay 계약

- arena match는 기존 `MatchRecordHeader` / `MatchRecordBlob` 재생산 경로를 재사용한다.
- `ArenaMatchRecord`는 replay bundle의 `MatchId`를 참조한다.
- same defense snapshot + same seed면 same replay result를 반환해야 한다.

## deferred

- live matchmaking service
- leaderboard backend
- seasonal event ops
- remote reward delivery
