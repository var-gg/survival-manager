# 작업 명세

## 메타데이터

- 작업명: EditMode failure closure
- 담당: Codex
- 상태: active
- 최종수정일: 2026-04-01

## Goal

- full `EditMode` suite에 남아 있는 9개 failing test를 재현하고 수선한다.
- 전투 simulator / hit-resolution / content validator 경로를 다시 green 상태로 되돌린다.
- 이전 `sample content preflight` task와 범위를 섞지 않고, failure closure만 별도 handoff 단위로 남긴다.

## Authoritative boundary

- 테스트 소스: `Assets/Tests/EditMode/**`
- 전투 런타임: `Assets/_Game/Scripts/Runtime/Combat/**`
- validator / editor validation: `Assets/_Game/Scripts/Editor/Validation/**`
- 필요 시 canonical content asset: `Assets/Resources/_Game/Content/Definitions/**`

## In scope

- `BattleResolutionTests` failure 수선
- `BattleSimulationSpatialTests` failure 수선
- `CombatContractsTests` hit-resolution failure 수선
- `ContentValidationWorkflowTests` deep schema drift failure 수선
- 위 수리에 필요한 최소 코드/asset 수정
- 새 task 문서와 handoff 기록

## Out of scope

- unrelated scene 변경 정리
- localization settings 변경 정리
- sample content serialization format(`m_Script: {fileID: 0}`)의 근본 원인 전체 분석
- PlayMode suite 또는 전투 외 시스템 확장

## asmdef impact

- 없음

## persistence impact

- 없음

## validator / test oracle

- `pwsh -File tools/unity-bridge.ps1 compile`
- targeted `unity-cli test --mode EditMode --filter ...`
- `pwsh -File tools/unity-bridge.ps1 test-edit`
- 필요 시 `pwsh -File tools/unity-bridge.ps1 seed-content`

## done definition

- 현재 남은 9개 EditMode failure가 0건이 된다.
- full `test-edit`가 green이거나, repo-wide known debt와 무관한 새 blocker가 없음을 evidence와 함께 설명한다.
- task `status.md`에 failure별 원인과 수정 결과를 남긴다.

## deferred

- canonical asset serialization 형식의 구조적 원인 추적
- 전투 acceptance 임계값 재설계가 필요한 경우의 추가 design contract 개정
