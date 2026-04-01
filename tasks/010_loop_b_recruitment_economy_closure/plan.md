# 작업 계획

## 메타데이터

- 작업명: Loop B Recruitment / Retrain / Economy Closure
- 담당: Codex
- 상태: 완료
- 최종수정일: 2026-04-01

## Preflight

- Loop A 6-slot/loadout contract 위에서만 작업
- 기존 `trait reroll / echo crystal` 경로와 충돌하는 active source-of-truth 문서 식별
- Town을 recruit/camp phase equivalent로 고정

## Phase 1 code-only

- recruit/retrain/duplicate/dismiss data contract 추가
- `GameSessionState` ad-hoc 로직을 `SM.Meta.Services`로 분리
- 4-slot recruit pack, pity, scout, duplicate, dismiss, retrain pity 구현
- Town UI와 scene binder를 4 recruit card / Gold-Echo wording에 맞춤

## Phase 2 asset authoring

- sample content recruit tier / plan tag / scout tag / flex pool / banned pairing 반영
- localization foundation wording을 Echo 기준으로 정리
- runtime fallback과 canonical seed drift 완화

## Phase 3 validation

- content validator에 Loop B pool/coherence/banned pairing rule 추가
- editmode deterministic simulation 추가
- playmode smoke 확인
- docs / task / harness 동기화

## rollback / escape hatch

- recruit/retrain rule drift가 생기면 `RuntimeCombatContentLookup` fallback과 test oracle을 우선 보고 수정
- Town scene UI 회귀 시 `FirstPlayableSceneInstaller`와 `FirstPlayableRuntimeSceneBinder`를 함께 확인

## tool usage plan

- 코드/문서 읽기: `rg`, `Get-Content`
- 편집: `apply_patch`
- Unity compile/test: `pwsh -File tools/unity-bridge.ps1 compile|test-edit|test-play`
- docs harness: `tools/docs-policy-check.ps1`, `tools/docs-check.ps1`, `tools/smoke-check.ps1`

## loop budget

- code closure
- validator/test recovery
- docs/task packet sync
- final verification / commit
