# 작업 상태

## 메타데이터

- 작업명: System Deepening Pass
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-01

## Current state

- task packet 생성 완료
- current design/architecture/source-of-truth 확인 완료
- PR 1 schema/validator/report scaffold 완료
- PR 2 catalog 문서 추가 완료
- compile / docs-policy / smoke evidence 수집 완료
- targeted EditMode test 결과 회수는 여전히 불안정
- Loop A authority/cadence/targeting/summon closure는 follow-up task `tasks/009_loop_a_contract_closure/`에서 완료했다

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | schema scaffold 후 compile green | 완료 | `pwsh -File tools/unity-bridge.ps1 compile` |
| validator | 새 schema consistency와 기존 `2 / 3 / 4` 계약 유지 | 완료 | `ContentDefinitionValidator` 확장, deep schema drift test 추가 |
| targeted tests | schema workflow와 report field shape test | 부분 | test code 추가 완료, `test-edit` 결과 회수는 연결 종료로 보류 |
| runtime smoke | docs/smoke harness green | 부분 | `smoke-check` green, Unity battle report는 test loop 뒤 timeout |

## Evidence

- 기준 문서:
  - `docs/index.md`
  - `docs/02_design/index.md`
  - `docs/03_architecture/index.md`
  - `tasks/005_battle_contract_closure/status.md`
- 현재 확정 결정:
  - synergy breakpoint `2 / 3 / 4` 유지
  - battle compile 4-slot 유지
  - fixed core + rolled flex + retrain은 meta layer 계약으로만 도입
  - catalog source-of-truth는 Markdown
- 실행 근거:
  - `pwsh -File tools/unity-bridge.ps1 compile`
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
  - `pwsh -File tools/docs-check.ps1 -RepoRoot .`
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
  - `pwsh -File tools/unity-bridge.ps1 test-edit`
- 결과 요약:
  - compile green
  - docs policy green
  - docs check red: repo-wide markdownlint `773`건
  - smoke check green
  - `test-edit`: `run_tests sent (connection closed before response)`
  - 이후 `report-battle`, `console`: Unity port `8090` timeout

## Remaining blockers

- Unity EditMode test runner 결과 회수 불안정
- Unity가 새 schema field를 기존 content asset에 광범위하게 reserialize해 diff footprint가 큼
- docs-check는 repo-wide debt와 이번 task line-length debt 때문에 계속 red

## Deferred / debug-only

- full runtime content rollout
- 5-slot skill compile
- meta progression / encounter scripting / summon ownership full closure
- Markdown line-length debt 정리
- authority matrix / 6-slot energy / summon ownership runtime는 `tasks/009_loop_a_contract_closure/`에서 진행

## Loop budget consumed

- compile-fix: 1
- refresh/read-console: 1
- asset authoring retry: 0
- budget 초과 시 남긴 diagnosis:
  - `unity-cli test` 이후 editor 응답 채널이 잠기면 `console` / `report-*`도 timeout으로 이어진다.

## Handoff notes

- 다음 세션 시작 문서:
  - `tasks/006_system_deepening_pass/status.md`
  - `tasks/006_system_deepening_pass/spec.md`
  - `tasks/006_system_deepening_pass/plan.md`
  - `tasks/006_system_deepening_pass/implement.md`
- Loop A follow-up는 `tasks/009_loop_a_contract_closure/status.md`를 새 시작점으로 본다.
- phase split 문서가 있으므로 PR 1과 PR 2 범위를 섞지 않는다.
- current worktree에는 의도한 code/doc diff 외에 `Assets/Resources/_Game/Content/Definitions/**` 재직렬화 흔적이 크다.
- 이 asset diff를 유지할지 줄일지는 다음 세션에서 명시적으로 판단해야 한다.
