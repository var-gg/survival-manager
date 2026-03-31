# 작업 상태

## 메타데이터

- 작업명: Battle Contract Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-01

## Current state

- task packet 생성 완료
- battle contract 기존 구조 조사 완료
- code-only / asset authoring / docs 반영 완료
- validation은 runtime smoke green, EditMode test runner 결과 회수만 보류

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | battle/runtime/profile 변경 후 compile green | 완료 | `pwsh -File tools/unity-bridge.ps1 compile` green |
| validator | profile/localization drift 검증 | 부분 | generator/profile asset, `UI_Battle` coverage test 추가. 별도 report loop는 없음 |
| targeted tests | spacing, mobility, hit resolution, localization fallback | 보류 | `pwsh -File tools/unity-bridge.ps1 test-edit`가 결과 회수 전 hang |
| runtime smoke | overhead UI / spacing / localization 확인 | 완료 | `bootstrap`, `report-battle`, `console` green |
| docs policy | docs/task 거버넌스 검증 | 완료 | `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .` |
| docs lint | markdownlint | 기존 debt로 red | repo-wide 710건, task 신규/기존 debt 분리 기록 |
| smoke check | repo smoke | 완료 | `pwsh -File tools/smoke-check.ps1 -RepoRoot .` |

## Evidence

- 구현 요약:
  - footprint/behavior/mobility profile authoring + runtime carry-through
  - slot reservation / range band / mobility scaffold / hit resolution 도입
  - screen-space overhead UI + HeadAnchor + localization fallback 적용
  - gizmo 확장과 contract 문서 추가
- 검증:
  - `pwsh -File tools/unity-bridge.ps1 compile`
  - `pwsh -File tools/unity-bridge.ps1 bootstrap`
  - `pwsh -File tools/unity-bridge.ps1 report-battle`
  - `pwsh -File tools/unity-bridge.ps1 console -Lines 160 -Filter error,warning,log`
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
  - `pwsh -File tools/docs-check.ps1 -RepoRoot .`
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
- noteworthy loop:
  - first seed generation collided with observer/play-mode loop and left
    `ForceReserializeAssets` 예외가 남았다.
  - Play Mode 밖에서 seed generation을 다시 실행해
    footprint/behavior/mobility profile asset을 생성하고
    archetype reference를 복구했다.

## Remaining blockers

- `unity-cli` 기반 EditMode test runner가 결과를 돌려주지 못하고
  editor를 일시적으로 멈춘다.
- battle smoke와 docs/smoke checks는 끝났지만,
  EditMode test 결과를 본문 evidence로 확정하지 못했다.

## Deferred / debug-only

- facing 기반 guard arc
- melee engage dash/lunge runtime rollout
- public mental stat 승격

## Loop budget consumed

- compile-fix: 1
- refresh/read-console: 2
- asset authoring retry: 1
- budget 초과 시 남긴 diagnosis:
  - `unity-cli test` / `console` 계열이 장시간 응답을 끊는 경우가 있어
    test 결과 회수 채널을 별도 정리할 필요가 있다.

## Handoff notes

- 시작 문서: 이 status -> spec -> plan -> implement
- compile와 asset authoring을 같은 micro-loop로 섞지 않는다.
- Unity battle observer smoke는 `tools/unity-bridge.ps1` 경로를 먼저 사용한다.
- launch-floor extended archetype 4종(`bulwark`, `reaver`, `marksman`,
  `shaman`)은 patch path가 누락돼 수동 복구했다.
