# canonical Resources audit green 계획

## 메타데이터

- 작업명: canonical Resources audit green
- 담당: Codex
- 상태: done
- 최종수정일: 2026-04-19
- 의존: `tasks/019_meta_session_boundary_refactor/status.md`

## Preflight

- `git status --short --branch`로 baseline commit 이후 clean state를 확인한다.
- `BuildCompileAuditTests` 실패가 canonical Resources 누락인지 확인한다.
- Resources repair 전후 diff에서 scene/prefab 변경을 분리한다.
- `SM.Meta` forbidden using guard가 유지되는지 후속 검증에 포함한다.

## Phase 1 code-only

- 이 task는 기본적으로 asset repair가 중심이다.
- compile 또는 lookup contract 문제가 발견될 때만 최소 코드 수정을 한다.
- ScriptableObject typed load가 file-scoped namespace와 충돌하면 해당 authored content type만 block namespace로 전환한다.
- implicit regeneration, editor filesystem fallback 기본값 변경은 금지한다.

## Phase 2 asset authoring

- `pwsh -File tools/unity-bridge.ps1 seed-content`를 explicit repair lane으로 1회 실행한다.
- editor menu lane이 실행 불가하면 batch `executeMethod`로 `SM.Editor.SeedData.SampleSeedGenerator.Generate`를 호출한다.
- 생성/수정된 `Assets/Resources/_Game/Content/Definitions/**` asset만 검토한다.
- Unity import가 scene/prefab을 건드리면 해당 변경은 제외한다.

## Phase 3 validation

- `BuildCompileAuditTests` 전체 클래스를 batch EditMode로 실행한다.
- `content-validate`를 실행해 canonical missing issue가 닫혔는지 확인한다.
- specialist coverage, Loop C tuning, localization tone은 021에서 별도로 닫는다.
- 기본 fast/lint/docs smoke는 후속 커밋 전 확인한다.

## rollback / escape hatch

- `seed-content` 2회 후에도 canonical root가 unreadable이면 중단하고 failure list를 `status.md`에 남긴다.
- asset diff가 과도하게 넓어지면 specialist/tuning과 섞지 않고 이 task에서 Resources readiness만 커밋한다.

## tool usage plan

- file-first 확인 후 Unity wrapper를 사용한다.
- scene/prefab/component authoring은 MCP로도 수행하지 않는다.
- Unity batchmode 명령은 순차 실행한다.

## loop budget

- compile-fix: 1회
- refresh/read-console: 2회
- asset authoring retry: 2회
- validation retry: 2회
