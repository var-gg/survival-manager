# boundary harness docs parity 계획

## 메타데이터

- 작업명: boundary harness docs parity
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-19

## Preflight

- clean `main`에서 시작한다.
- `BuildCompileAuditTests`의 `BatchOnly` guard 배치와 문서 drift를 확인한다.
- `TownBuildHotPathTests.cs` 존재 여부를 확인한다.

## Phase 1 code-only

- `BuildBoundaryGuardFastTests`를 추가해 파일시스템/asmdef 기반 guard를 `FastUnit`으로 실행한다.
- 기존 `BuildCompileAuditTests`에는 asset-loading/`RuntimeCombatContentLookup`/`AssetDatabase` 기반 audit만 남긴다.
- `test-harness-lint.ps1`에 unguarded runtime editor API와 non-`BatchOnly` resource/content lookup 감지를 추가한다.

## Phase 2 docs parity

- stale FastUnit count/time 문구를 제거한다.
- 실제 test asmdef 3종과 `SM.Editor` dependency를 architecture docs에 반영한다.
- 019/023 task status의 historical/current state 표현을 정리한다.

## Phase 3 validation

- fast lane, focused BatchOnly audit, lint, docs policy/check/smoke를 순차 실행한다.
- Unity가 자동으로 prefab을 건드리면 scope 밖 diff로 보고 되돌린다.

## rollback / escape hatch

- 새 FastUnit guard가 Unity asset API를 요구하면 해당 assertion은 `BatchOnly`에 남긴다.
- lint 확장에 false positive가 많으면 resource/content lookup 감지는 테스트 파일 대상으로만 제한한다.

## tool usage plan

- 코드/문서 변경은 `apply_patch`로 수행한다.
- Unity 검증은 `tools/unity-bridge.ps1`를 사용한다.

## loop budget

- compile/test retry: 2
- lint false positive fix: 2
- docs-check retry: 1
