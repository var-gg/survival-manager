# 작업 구현

## 메타데이터

- 작업명: Loop D Telemetry / Pruning / Readability Gate / First Playable Balance Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-02

## Phase log

- Phase 0 preflight
  - Loop C runtime summary, slice pressure, compiled slot 분류 기준을 재확인했다.
  - `RecruitTier`와 `ContentRarity`의 경계는 Loop C 규칙을 그대로 유지했다.
- Phase 1 code-only
  - Loop D telemetry/readability/pruning/balance model을 `SM.Combat`에 추가했다.
  - battle runtime과 meta runtime에 telemetry emit point를 연결했다.
  - replay bundle이 telemetry, readability report, battle summary를 함께 담도록 확장했다.
  - first playable slice asset/runtime projection과 slice-aware canonical pool filtering을 추가했다.
  - Loop D balance runner, slice generator, validation entrypoint, explain-stamp validator를 추가했다.
  - smoke runner가 에디터를 과도하게 점유해 시나리오 수와 seed 수를 별도 smoke path로 줄였다.
- Phase 2 asset/doc sync
  - Loop D task packet과 telemetry/readability/slice/pruning 문서를 신규 작성했다.
  - architecture/design/production index에 Loop D 문서를 연결했다.

## deviation

- `BattleEvent`는 즉시 제거하지 않고 telemetry의 legacy projection으로 유지했다.
- first playable health/prune는 ML 없이 heuristic evaluator와 prune ledger로 닫았다.
- full suite는 wall-clock 부담이 커서 smoke path를 별도 menu/gate로 열었다.

## blockers

- smoke 메뉴 실행 초기에 Unity main thread가 장시간 점유되어 에디터를 한 번 강제 재시작했다.
- full suite 1회 실증과 final artifact 회수는 Unity 재기동 뒤 다시 확인이 필요하다.

## diagnostics

- compile는 Loop D runner/telemetry 추가 후 다시 green을 회복했다.
- Unity safe mode 원인은 새 test의 `Newtonsoft.Json` 참조였고 `JsonUtility`로 교체했다.
- 남은 검증은 smoke runner artifact, edit/play test, docs/smoke script 재실행이다.

## why this loop happened

- Loop C까지는 content governance는 닫혔지만, 실제 playable slice를 측정하고 설명하고
  빼낼 시스템이 부족했다.
- Loop D는 더 많은 content를 넣는 루프가 아니라, 무엇을 남기고 무엇을 V1에서 빼야 하는지
  시스템이 스스로 말하게 만드는 루프다.
