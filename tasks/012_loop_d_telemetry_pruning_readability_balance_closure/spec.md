# 작업 명세

## 메타데이터

- 작업명: Loop D Telemetry / Pruning / Readability Gate / First Playable Balance Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-02
- 관련경로:
  - `Assets/_Game/Scripts/Runtime/Combat/**`
  - `Assets/_Game/Scripts/Runtime/Meta/**`
  - `Assets/_Game/Scripts/Runtime/Unity/**`
  - `Assets/_Game/Scripts/Editor/Validation/**`
  - `Assets/_Game/Scripts/Editor/Authoring/**`
  - `Assets/Tests/**`
  - `docs/**`
- 관련문서:
  - `docs/03_architecture/telemetry-contract.md`
  - `docs/03_architecture/readability-gate-contract.md`
  - `docs/02_design/systems/first-playable-slice.md`
  - `docs/03_architecture/first-playable-balance-targets.md`
  - `docs/03_architecture/pruning-playbook.md`
  - `docs/06_production/loop-d-closure-note.md`

## Goal

- simulation/runtime truth를 기반으로 Loop D telemetry, readability gate, pruning, first
  playable balance suite를 실제 코드/하네스/문서/CI gate로 닫는다.

## Authoritative boundary

- 전투/경제/모집/재훈련/중복 처리 계측의 source-of-truth는 runtime telemetry event다.
- `BattleEvent`는 legacy replay/debug projection으로 남기고, Loop D 판단은
  `TelemetryEventRecord`를 기준으로 한다.
- first playable slice의 runtime source-of-truth는
  `FirstPlayableSliceDefinitionAsset`과 그 runtime projection이다.
- player-facing UI는 `RecruitTier`와 role/tag/counter hint만 노출하고, readability/budget
  drift는 dev/debug에서만 노출한다.

## In scope

- Loop D telemetry model, ExplainStamp propagation, summary/readability report 생성
- first playable slice asset 생성과 runtime slice-aware filtering
- content health card / prune ledger / readability watchlist 생성
- Loop D balance runner와 validation batch entrypoint 추가
- dev sandbox readability/governance summary 표시
- Loop D task packet과 지속 문서 작성 및 index 동기화

## Out of scope

- live analytics SDK 연동
- polished replay viewer
- ML/autotune/auto-balancer
- ranked ladder balance
- 장기 retention telemetry

## asmdef impact

- 주요 영향 asmdef:
  - `SM.Combat`
  - `SM.Meta`
  - `SM.Unity`
  - `SM.Editor`
  - `SM.Tests.EditMode`
- 허용 의존:
  - `SM.Editor` -> runtime telemetry/readability service 참조
  - `SM.Unity` / `SM.Meta` -> runtime telemetry model 참조
- 금지 의존:
  - `SM.Combat`에서 editor runner/viewer 참조
  - `SM.Persistence.Abstractions`로 editor-only artifact type 누수

## persistence impact

- `MatchRecordBlob`는 Loop D digest와 telemetry artifact 경로를 저장할 수 있다.
- save/system state에 `RecruitTier`와 `ContentRarity`의 역할 경계는 Loop C 규칙을 그대로
  유지한다.
- external analytics backend serialization은 이번 루프 범위가 아니다.

## validator / test oracle

- 필수 runner/gate:
  - `FirstPlayableSliceGenerator`
  - `FirstPlayableBalanceRunner`
  - `ValidationBatchEntryPoint.RunLoopDReadabilityAndBalance()`
- 필수 EditMode test:
  - telemetry serialization round-trip
  - ExplainStamp completeness validator
  - readability aggregation / violation 계산
  - first playable slice cap / artifact 생성
- 필수 artifact:
  - `Logs/loop-d-balance/purekit_report.json`
  - `Logs/loop-d-balance/systemic_slice_report.json`
  - `Logs/loop-d-balance/runlite_report.json`
  - `Logs/loop-d-balance/content_health_cards.csv`
  - `Logs/loop-d-balance/prune_ledger_v1.json`
  - `Logs/loop-d-balance/readability_watchlist.json`
  - `Logs/loop-d-balance/first_playable_slice.md`

## done definition

- first playable slice asset/config가 실제로 존재한다.
- recruit/flex/augment/affix generation path가 slice-aware하다.
- battle/economy/recruit/retrain/duplicate 핵심 event가 telemetry로 기록된다.
- 필수 영향 event에 ExplainStamp 누락이 없다.
- readability report와 prune ledger가 artifact로 생성된다.
- Loop D suite가 최소 smoke 경로에서 deterministic하게 실행된다.
- Loop D docs/task/index가 같은 결론을 가리킨다.

## deferred

- full-run suite wall-clock 최적화
- authored encounter ladder 기반 RunLite 확장
- offscreen major event의 실제 viewport heuristic 정교화
