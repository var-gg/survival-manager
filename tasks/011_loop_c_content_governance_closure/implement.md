# 작업 구현

## 메타데이터

- 작업명: Loop C Content Governance Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-01
- 실행범위:
  - content governance schema
  - validator / audit export
  - runtime summary / UI boundary
  - threat scenario / debug overlay
  - docs / task packet sync

## Phase log

- Phase 0 preflight
  - Loop A/Loop B closure 문서와 현재 runtime/template/UI 경계를 재확인했다.
  - `RecruitTier`와 `ContentRarity`를 혼용하지 않는 boundary를 먼저 고정했다.
- Phase 1 code-only
  - `BudgetCard`, `ContentRarity`, 8-lane topology, forbidden flag contract를
    `SM.Content` authoring seam에 연결했다.
  - `RuntimeCombatContentLookup`가 compact governance summary를 runtime template과
    snapshot으로 carry-through 하도록 맞췄다.
  - `TownScreenController`는 player-safe recruit badge/tag/counter hint만 노출하고,
    budget/rarity/drift는 editor sandbox와 combat debug view에서만 보이게 분리했다.
  - Loop C validator pass와 `ValidateAndWriteReport()` artifact export를 추가했다.
- Phase 2 asset authoring
  - fallback combat content와 permanent augment asset을 Loop C 규칙에 맞게 보정했다.
  - threat topology scenario 8종과 counter coverage aggregation path를 추가했다.
- Phase 3 validation
  - compile green을 회복했다.
  - content validation artifact가 green으로 생성되는 것을 확인했다.
  - EditMode runner 결과 회수는 한 번 더 확인이 필요하다.

## deviation

- summon/status rider는 standalone domain을 새로 만들지 않고 owner definition budget에
  흡수했다.
- `DamageType.True` enum 값은 serialization compatibility 때문에 삭제하지 않고 validator
  fatal fail로 막는 방향으로 정리했다.

## blockers

- Unity EditMode runner가 간헐적으로 connection closed로 끝나 결과 회수가 불안정하다.
- docs/task packet/index가 아직 같은 시점 evidence로 동기화돼야 한다.

## diagnostics

- compile: green
- content validation: green
- permanent augment floor gap: 0
- audit artifact:
  - `Logs/content-validation/content_budget_audit.json`
  - `Logs/content-validation/content_budget_audit.md`
  - `Logs/content-validation/counter_coverage_matrix.md`
  - `Logs/content-validation/v1_forbidden_feature_report.md`

## why this loop happened

- Loop B까지는 power/economy 구조가 닫혔지만, authoring contract가 약해서 새 content가
  들어올 때 drift를 조기에 막을 장치가 부족했다.
- Loop C는 sim을 더 깊게 만드는 대신 authored ledger + validator + debug overlay를
  공통 계약으로 세워서 content scaling cost를 낮추는 루프다.

## 기록 규칙

- compile/validator/test 미시 로그는 status에 누적하지 않는다.
- phase별 구조 변화와 blocker만 요약한다.
- docs index와 task status는 같은 변경 단위에서 갱신한다.
