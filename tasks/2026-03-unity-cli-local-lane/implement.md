# Task Implement

## Metadata
- Task Name: unity-cli local lane pilot
- Owner: Codex
- Status: in_progress
- Last Updated: 2026-03-30
- Execution Scope:
  - `docs/**`
  - `tools/**`
  - `prompts/**`
  - `Packages/**`
  - `Assets/_Game/Scripts/Editor/**`

## Working Method
- file-first를 기본으로 유지한다.
- code/docs/tests/repo-tracked YAML은 먼저 파일로 수정한다.
- Unity bridge는 compile/bootstrap/smoke/report/verification surface로만 사용한다.
- 한 번의 menu/console/test/report로 끝나는 작업은 CLI lane을 먼저 검토한다.
- 구조적 편집이나 typed guardrail이 중요한 작업은 MCP lane을 쓴다.

## Scope Limits
- gameplay 구현 블록에 `unity-cli` 의존을 끼워 넣지 않는다.
- local optional setup을 팀 전체 mandatory dependency처럼 다루지 않는다.
- broad scene authoring, hidden global mutation, package churn을 raw `unity-cli exec` 기본 경로로 만들지 않는다.
- `Assets/ThirdParty/**`는 수정하지 않는다.

## CLI Lane / MCP Lane Rules
- CLI lane:
  - `status`, `list`, `compile`, `clear-console`, `console`, `bootstrap`, `test-edit`, `test-play`
  - aggregate report와 one-shot read probe
  - text-based asset edit 뒤 필요한 `reserialize` 검토
- MCP lane:
  - scene/prefab/component/package의 구조적 조작
  - typed schema가 안전한 작업
  - 기존 MCP custom tool이 이미 있는 작업
  - CLI 결과만으로 충분하지 않은 경우의 targeted fallback

## Raw Exec Guardrail
- raw `unity-cli exec`는 read-first다.
- broad write, hidden global mutation, large scene surgery에 쓰지 않는다.
- 반복 probe는 wrapper verb 또는 project-owned custom CLI tool로 승격한다.
- wrapper는 arbitrary exec를 기본 차단하고 명시 opt-in일 때만 허용한다.

## Documentation Update Rules
- package/dependency/workflow 변경은 setup 문서와 decision 문서를 같은 작업 단위에서 갱신한다.
- 문서 인덱스와 운영 프롬프트를 함께 맞춘다.
- `status.md`를 handoff 기준 문서로 유지한다.

## Test And Smoke Rules
- 수정 후 `unity-cli --project . status`, `list`, `editor refresh --compile`를 우선 확인한다.
- smoke/report는 wrapper 기준 verb로 재실행한다.
- CLI 결과가 불충분할 때만 targeted MCP를 사용한다.
