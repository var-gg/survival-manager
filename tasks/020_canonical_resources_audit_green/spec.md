# canonical Resources audit green 명세

## 메타데이터

- 작업명: canonical Resources audit green
- 담당: Codex
- 상태: done
- 최종수정일: 2026-04-19
- 관련경로:
  - `Assets/Resources/_Game/Content/Definitions/**`
  - `Assets/_Game/Scripts/Editor/SeedData/SampleSeedGenerator.cs`
  - `Assets/Tests/EditMode/BuildCompileAuditTests.cs`
- 관련문서:
  - `docs/03_architecture/content-loading-contract.md`
  - `docs/05_setup/unity-cli.md`
  - `tasks/019_meta_session_boundary_refactor/status.md`

## Goal

- canonical Resources content 누락을 복구해 `BuildCompileAuditTests` 전체 클래스를 editor fallback 없이 green으로 만든다.

## Authoritative boundary

- 이 task는 committed Resources authoring root의 runtime-readiness만 닫는다.
- runtime source-of-truth는 `Assets/Resources/_Game/Content/Definitions/**`로 유지한다.
- encounter tuning, localization tone, session service extraction은 후속 task에서 닫는다.

## In scope

- `seed-content` explicit lane으로 canonical Resources asset을 복구한다.
- 생성/수정된 Resources asset diff를 검토하고, 불필요한 prefab/scene 변경은 제외한다.
- `BuildCompileAuditTests` 전체 클래스를 green으로 만든다.
- `content-validate`는 실행해 canonical missing issue가 닫혔는지 확인하고, specialist/tuning governance failure는 021로 넘긴다.
- task evidence와 loop budget을 갱신한다.

## Out of scope

- implicit runtime regeneration 재도입
- scene/prefab/component 구조 편집
- specialist encounter 배치나 밸런스 수치 조정
- docs markdownlint repo-wide cleanup

## asmdef impact

- production asmdef 변경은 기본적으로 없다.
- `SM.Meta`, `SM.Meta.Serialization`, `SM.Core`, `SM.Combat` pure boundary는 유지한다.
- `SM.Tests.PlayMode`가 `SM.Editor`를 다시 참조하면 실패로 본다.

## persistence impact

- 저장 모델과 persistence contract 변경은 없다.
- Resources asset 복구는 runtime content lookup 입력만 복구한다.

## validator / test oracle

- `pwsh -File tools/unity-bridge.ps1 seed-content`
- editor menu lane이 불가할 경우 `pwsh -File tools/unity-execute-method.ps1 -Method SM.Editor.SeedData.SampleSeedGenerator.Generate`를 explicit repair lane으로 사용한다.
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter SM.Tests.EditMode.BuildCompileAuditTests`
- `pwsh -File tools/unity-bridge.ps1 content-validate`
- 필요 시 `RuntimeCombatContentLookup` 기반 snapshot이 editor fallback 없이 성공하는 focused 확인을 추가한다.

## done definition

- `BuildCompileAuditTests` 전체 클래스 failed 0.
- `content-validate`에서 `first_playable.asset_missing` 및 Resources typed-load missing issue 0.
- `content-validate` 잔여 실패가 021 specialist/tuning 범위로 분류되어 있다.
- 생성/수정 asset diff에 scene/prefab 구조 변경이 없다.
- `status.md`에 명령, 결과, 남은 리스크를 기록한다.

## deferred

- specialist enemy coverage
- balance sweep 기반 수치 튜닝
- localization tone pass
- session service object extraction
