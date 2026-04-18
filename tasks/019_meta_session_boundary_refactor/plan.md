# SM.Meta/Session boundary refactor 계획

## 메타데이터

- 작업명: SM.Meta/Session boundary refactor
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19
- 의존:
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/00_governance/implementation-review-checklist.md`

## Preflight

- `SM.Meta`가 `SM.Content`를 참조하고 `noEngineReferences: false`인 상태를 확인한다.
- `SM.Tests.PlayMode`가 `SM.Editor`를 참조하는 drift를 확인한다.
- `GameSessionState.cs`가 3,695줄 이상으로 session/orchestration 책임을 과도하게 가진 상태를 확인한다.
- task spec/status에 asmdef impact, persistence impact, test oracle, loop budget을 먼저 둔다.

## Phase 1 code-only

- content schema enum을 `SM.Core.Content`로 이동한다.
- `SM.Content` definition, parser, converter, editor/test 코드가 새 namespace를 사용하도록 바꾼다.
- `SM.Meta` model/service에서 `SM.Content` authored type 직접 사용을 제거한다.
- narrative ScriptableObject 변환은 `SM.Unity.ContentConversion` adapter로 이동한다.
- `GameSessionState` facade 파일을 줄이고 session 흐름 파일을 `SM.Unity/Session` 아래로 분리한다.

## Phase 2 asset authoring

- 이번 task에서는 scene/prefab/component/asset authoring을 하지 않는다.
- asset refresh가 필요한 경우에도 코드 컴파일과 test 결과 확인만 수행한다.

## Phase 3 validation

- `BuildCompileAuditTests`에 asmdef/source/line budget guard를 추가한다.
- `test-batch-fast`로 FastUnit oracle을 확인한다.
- `tools/test-harness-lint.ps1`로 harness 정책을 확인한다.
- docs policy/check/smoke로 문서 구조를 확인한다.

## rollback / escape hatch

- compile-fix 3회 초과 시 schema 이동과 GameSessionState 분해를 별도 child task로 쪼갠다.
- `SM.Meta`가 `SM.Content` 없이 컴파일되지 않으면 누락 pure model을 `SM.Meta.Model`에 먼저 추가한다.
- PlayMode asmdef에서 editor helper가 필요하면 runtime-safe test support asmdef로 분리하는 후속 task를 만든다.

## tool usage plan

- file-first로 asmdef, source guard, docs를 확인한다.
- code-only 단계는 CLI/PowerShell과 Unity batch compile/test를 사용한다.
- scene/prefab typed edit가 없으므로 MCP는 사용하지 않는다.

## loop budget

- compile-fix 허용 횟수: 3
- refresh/read-console 반복 허용 횟수: 2
- blind asset generation 재시도 허용 횟수: 0
