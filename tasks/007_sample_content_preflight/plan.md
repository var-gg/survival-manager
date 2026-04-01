# 작업 계획

## 메타데이터

- 작업명: sample content preflight
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-01
- 의존:
  - `tasks/2026-03-unity-cli-local-lane/status.md`
  - `tasks/005_battle_contract_closure/status.md`

## Preflight

- current worktree에서 unrelated 변경을 확인하고 덮어쓰지 않는다.
- sample content regeneration 호출 지점과 wrapper/test lane 상태를 먼저 확인한다.
- task spec/status에 acceptance와 deferred를 먼저 기록한다.

## Phase 1 code-only

- `SampleSeedGenerator`에 read-only readiness API를 추가한다.
- `RuntimeCombatContentLookup`, `FirstPlayableContentBootstrap`, EditMode tests에서 implicit regeneration 호출을 제거한다.
- runtime/editor 경로에서는 helpful failure message만 남기고 asset rewrite를 하지 않게 만든다.

## Phase 2 asset authoring

- explicit preflight command로 canonical sample content를 1회 regenerate한다.
- canonical content diff를 확인하고, test run 중 추가 rewrite가 생기지 않는지 본다.
- broad scene surgery는 하지 않는다.

## Phase 3 validation

- compile
- explicit seed preflight
- targeted filtered test 또는 full `test-edit` artifact 회수
- docs policy / smoke check

## rollback / escape hatch

- runtime lookup가 hard fail만 남기고 editor 사용성을 과도하게 해치면 read-only warning + caller fail-fast로 낮춘다.
- regenerate asset diff가 예상 범위를 넘으면 current task를 asset normalization child task로 분리한다.

## tool usage plan

- file-first로 code/test/doc 수정
- Unity compile/seed/test는 `tools/unity-bridge.ps1` 우선
- direct `unity-cli`는 filtered test와 artifact 확인처럼 wrapper verb가 없는 좁은 진단에만 사용

## loop budget

- compile-fix 허용 횟수: 4
- refresh/read-console 반복 허용 횟수: 4
- blind asset generation 재시도 허용 횟수: 2
