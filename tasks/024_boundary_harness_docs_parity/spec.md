# boundary harness docs parity 사양

## 메타데이터

- 작업명: boundary harness docs parity
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-19
- 소스오브트루스: `tasks/024_boundary_harness_docs_parity/spec.md`

## Goal

Pro 감사에서 확인된 단기 하네스/문서 drift를 닫는다. pure boundary 회귀는 기본 `test-batch-fast`에서 보이게 하고, 문서는 실제 asmdef/test/session 상태를 따라가게 한다.

## Authoritative boundary

- `SM.Meta`, `SM.Meta.Serialization`, `SM.Core`, `SM.Combat`, `SM.Persistence.Abstractions`는 Unity engine/editor/content direct dependency를 갖지 않는다.
- `SM.Tests.PlayMode`는 `SM.Editor`를 참조하지 않는다.
- `GameSessionState` service object extraction은 1차 delegation 완료 상태이며, ownership migration은 별도 후속 작업이다.

## In scope

- lightweight asmdef/source/line-budget guard를 `FastUnit` 테스트로 분리.
- `tools/test-harness-lint.ps1` semantic pattern 확장.
- stale 테스트 수/시간 문구와 test asmdef 문서 drift 정리.
- task 019/023 status의 historical/current state 표현 정리.

## Out of scope

- `GameSessionState` `*Core` body를 service object 내부로 옮기는 2차 ownership migration.
- 새 asmdef 추가.
- scene/prefab/asset authoring.

## asmdef impact

- 없음. 새 asmdef를 만들지 않고 `SM.Tests.EditMode` 안에 새 FastUnit 테스트 파일만 추가한다.

## persistence impact

- 없음.

## validator / test oracle

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildCompileAuditTests`
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## Done definition

- pure boundary guard가 `FastUnit` lane에서 실행된다.
- lint가 runtime unguarded `UnityEditor.`/`AssetDatabase`와 non-`BatchOnly` direct resource/content lookup을 잡는다.
- 문서가 현재 test asmdef와 session extraction 상태를 반영한다.
- 검증 명령이 통과한다.

## Deferred

- `GameSessionState` ownership migration phase 2는 `025` 후보로 남긴다.
