# Runtime Hardening Contract

- 상태: active
- 최종수정일: 2026-04-08
- 단계: prototype
- 소스오브트루스: `docs/06_production/runtime-hardening-contract.md`
- 관련문서:
  - `docs/05_setup/local-runbook.md`
  - `docs/05_setup/quick-battle-smoke.md`
  - `tasks/015_session_realm_authority_boundary/status.md`

## 목적

prototype pre-art 단계에서 `OfflineLocal` runtime을 silent save corruption, reward duplication, smoke contamination 없이 유지하기 위한 최소 운영 계약을 정리한다.

## authoritative boundary

- playable realm은 `OfflineLocal` 하나만 인정한다.
- `OnlineAuthoritative`는 hidden future seam으로만 유지하고 mid-run realm switch는 허용하지 않는다.
- `GameSessionState`는 현재 slice의 business truth를 유지한다.
- file I/O, integrity verify, backup fallback, smoke lane 분기는 `OfflineLocalSessionAdapter`와 JSON persistence 경계에서 처리한다.

## save / recovery contract

- load 결과는 최소 `MissingCreated`, `LoadedPrimary`, `LoadedBackupRecovered`, `FailedCorrupt`를 구분한다.
- existing artifact가 있었는데 verify에 실패한 경우를 `MissingCreated`로 처리하지 않는다.
- save protocol은 `serialize -> manifest/hash 생성 -> temp write -> primary replace -> backup 유지/회전 -> verify` 순서를 따른다.
- load protocol은 `primary verify -> backup verify -> quarantine/fail` 순서를 따른다.
- primary가 손상되고 backup가 유효하면 backup로 bind하고 suspicion/recovery telemetry를 남긴다.
- primary와 backup가 모두 손상되면 normal playable path는 새 profile을 자동 생성하지 않고 fail로 남긴다.

## checkpoint contract

- canonical playable checkpoint는 `TownExit`, `BattleResolved`, `RewardSettled`, `ReturnToStart` 네 지점을 기준으로 본다.
- `RewardApplied`는 reward duplication 방지를 위한 내부 durability anchor로 유지한다.
- Town manual `Load`는 idle profile recovery 용도로만 허용한다.
- active expedition, pending reward settlement, quick battle smoke overlay 중에는 Town `Load`를 block하고 이유를 UI/telemetry에 남긴다.

## quick battle isolation

- `SM/Quick Battle` direct entry는 dedicated smoke namespace를 사용한다.
- dedicated smoke auto-clear는 `<profile>.smoke` namespace만 삭제한다.
- Town `Quick Battle (Smoke)`는 canonical checkpoint 후 transient overlay로 진입한다.
- transient Town smoke 중에는 canonical save write를 허용하지 않는다.
- Town smoke 종료 시에는 disk에서 canonical profile을 다시 bind해서 overlay를 폐기한다.

## reward settlement contract

- reward settlement는 `ActiveRunRecord.RewardSourceId`와 `RewardLedgerEntryRecord.SourceId`를 idempotency anchor로 사용한다.
- 같은 settlement source에 대해 `:reward_choice` ledger entry가 이미 있으면 추가 지급하지 않는다.
- battle resolved 이후 reward scene 진입 전 재기동되면 pending reward anchor를 기준으로 settlement를 재개한다.
- reward 적용 후 Town 저장 전 재기동되면 ledger source 검사로 중복 지급 없이 settlement를 마무리한다.

## instrumentation policy

- normal playable lane은 `SummaryNormal` 정책을 사용한다.
- smoke lane은 `VerboseSmoke` 정책을 사용한다.
- normal playable lane에서는 per-step/per-frame runtime log를 남기지 않고 checkpoint summary와 warning/failure만 남긴다.
- smoke lane에서는 timing, seed, artifact, restore/recovery sequence를 bundle로 남길 수 있다.

## operational telemetry minimum

- session / persistence: `CheckpointStarted`, `CheckpointSucceeded`, `CheckpointRecoveredFromBackup`, `CheckpointFailed`, `CorruptSaveQuarantined`, `ManualReloadBlocked`, `SmokeRestoreFromDisk`
- reward / economy: `RewardOptionsPresented`, `RewardOptionChosen`, `RewardSettlementResumed`, `RewardSettlementDuplicatePrevented`, `EconomySnapshot`
- combat/readability/build variance는 기존 Loop D telemetry schema를 유지하고, session/recovery/reward는 lightweight operational schema로 분리한다.

## acceptance order

1. compile
2. validator
3. fast deterministic balance smoke
4. slice artifact
5. explicit balance runner artifact
6. Quick Battle smoke evidence
7. normal playable smoke with restart injection

## out of scope

- `GameSessionState`에서 persistence concern을 완전히 분리하는 deep refactor
- cloud save, live telemetry backend, official online settlement
- paid asset pass 이후 readability retune과 target hardware certification
- `Resources.LoadAll(...)` sample content pipeline 교체, expedition topology 확장
