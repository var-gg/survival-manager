# SM.Meta/Session boundary refactor 상태

## 메타데이터

- 작업명: SM.Meta/Session boundary refactor
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Current state

- 이 문서는 019 완료 시점의 historical snapshot이다. 이후 020 canonical Resources 복구, 022 docs markdownlint cleanup, 023 session service object extraction이 일부 blocker와 후속 상태를 해소했다.
- code-only refactor, asmdef guard, source guard, task/architecture/ADR 문서 갱신을 완료했다.
- handoff 가능 상태다. 남은 항목은 후속 구조 개선 후보이며 현재 compile/test를 막지 않는다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | Unity script compile error 0 | 통과 | `Unity.exe -batchmode -nographics -quit ... -logFile Logs/compile-refactor.log` exit 0 |
| validator | 구조 guard와 하네스 lint 통과 | 통과 | `BuildCompileAuditTests`, `pwsh -File tools/test-harness-lint.ps1` |
| targeted tests | FastUnit batch 통과 | 통과 | `pwsh -File tools/unity-bridge.ps1 test-batch-fast`: total 135, passed 132, failed 0, skipped 3 |
| guard tests | 신규 asmdef/source/line guard 통과 | 통과 | 신규 `BuildCompileAuditTests` guard 3개를 개별 `test-batch-edit -TestFilter`로 확인 |
| runtime smoke | scene/prefab authoring 없음 | 해당 없음 | 이번 task는 code/docs/asmdef/test만 수정 |
| docs | docs policy/check/smoke 통과 | 부분 통과 | policy/smoke와 변경 파일 markdownlint 통과. 전체 `docs-check`는 기존 backlog 1,716건으로 실패 |

## Evidence

- `SM.Meta.asmdef`: refs = `SM.Core`, `SM.Combat`, `noEngineReferences: true`
- `SM.Meta.Serialization.asmdef`: refs = `SM.Core`, `SM.Combat`, `SM.Meta`, `noEngineReferences: true`
- `SM.Tests.PlayMode.asmdef`: `SM.Editor` 참조 제거
- `Assets/_Game/Scripts/Runtime/Meta/**`: `using SM.Content`, `UnityEngine`, `UnityEditor` 검색 결과 없음
- `Assets/_Game/Scripts/Runtime/Unity/GameSessionState.cs`: 1,676줄
- `TestResults-Batch.xml`: FastUnit failed 0
- 신규 guard:
  - `RuntimeAssemblyDefinitions_KeepMetaAndPureLayerBoundaries`: passed 1
  - `MetaRuntimeSources_DoNotReferenceContentOrUnity`: passed 1
  - `GameSessionState_FacadeFileStaysBelowBoundaryBudget`: passed 1
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: 통과
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: 통과
- 변경 파일 대상 `npx markdownlint-cli2 ...`: 0 error

## Remaining blockers

- 전체 `pwsh -File tools/docs-check.ps1 -RepoRoot .`는 기존 markdownlint backlog로 실패한다. 이번 변경 파일은 별도 markdownlint에서 0 error다.
- `BuildCompileAuditTests` 전체 클래스 실행은 기존 canonical Resources content 누락으로 6개 legacy audit가 실패한다. 이번에 추가한 guard 3개는 개별 필터로 통과했다.

## Deferred / debug-only

- `SM.Meta`의 `*Record` rename은 deferred다.
- `ICombatContentLookup` 이동은 deferred다.
- `GameSessionState`를 완전한 독립 service 객체로 재구성하는 2차 작업은 deferred다.

## Loop budget consumed

- compile-fix: 1회
- refresh/read-console: 1회
- asset authoring retry: 0회
- budget 초과 시 남긴 diagnosis: 해당 없음

## Handoff notes

- 다음 구조 작업은 `docs/04_decisions/adr-0023-meta-content-adapter-boundary.md`와 이 `status.md`에서 시작한다.
- `SM.Meta`에 authored definition이나 Unity type을 다시 넣지 않는다.
- `GameSessionState.cs` 본 파일에 새 규칙/정산/상태전이를 직접 추가하지 않는다.
