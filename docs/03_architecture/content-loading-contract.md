# 콘텐츠 로딩 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/content-loading-contract.md`
- 관련문서:
  - `docs/03_architecture/content-loading-strategy.md`
  - `docs/03_architecture/content-authoring-model.md`
  - `docs/03_architecture/content-seed-assets.md`
  - `docs/05_setup/local-runbook.md`

## 목적

이 문서는 prototype 단계 sample content의 **구체적 로딩 계약**을 고정한다.
이번 계약의 목적은 scene/UI 완성 전에라도 content 부재 때문에 구조가 무너지는 상황을 먼저 없애는 것이다.

## canonical root

- canonical authored root: `Assets/Resources/_Game/Content/Definitions/**`
- runtime load path: `Resources.LoadAll("_Game/Content/Definitions/Stats")`
- 최소 부팅 기준: `StatDefinition` 1개 이상 존재

이 경로가 현재 sample content의 유일한 canonical root다.

## editor contract

- committed `ScriptableObject` asset이 canonical root의 source of truth다.
- `SM/Bootstrap/Ensure Sample Content`는 minimum bootstrap content 누락만 복구한다.
- `SM/Seed/Generate Sample Content`는 canonical root를 repair할 때만 쓰는 bootstrap tool이다.
- `SM/Seed/Migrate Legacy Sample Content`는 legacy root가 있을 때만 `AssetDatabase.MoveAsset` 기반 이동을 시도한다.
- legacy 이동이 없거나 실패하면 canonical root 재생성으로 fallback 한다.

## legacy path 취급

- legacy path: `Assets/_Game/Content/Definitions/**`
- 이 경로는 이제 canonical root가 아니다.
- 남아 있는 legacy asset은 migration 대상이거나, migration 실패 시 regeneration 대상이다.

## runtime contract

- `GameBootstrap`은 `Resources` 경로만 기준으로 본다.
- boot 시 canonical root가 비어 있으면 플레이를 막고 정확한 recovery menu를 안내한다.
- 1차 recovery action은 `SM/Bootstrap/Ensure Sample Content`다.
- committed floor authoring이 깨진 경우에만 `SM/Seed/Generate Sample Content`를 repair로 사용한다.

## block24 임시 bridge

- `FirstPlayableContentBootstrap`
- `FirstPlayableSceneInstaller`

위 계층은 block24 동안 테스트와 로컬 진단에 쓰는 **temporary bridge**다.
scene repair를 공식 계약으로 승격하는 일은 block25에서 다룬다.

## 검증 기준

- EditMode integrity suite는 canonical root 아래 최소 `StatDefinition`, `RaceDefinition`, `ClassDefinition`, `UnitArchetypeDefinition` 존재를 확인한다.
- playable scene asset 존재 여부를 확인한다.
- Boot/Town/Battle wiring은 temporary bridge 기준으로만 확인한다.

## 운영 메모

- content contract 변경 시 `content-loading-strategy.md`와 관련 setup/runbook 문서를 함께 갱신한다.
- scene repair나 observer playable 인증은 이 문서 범위가 아니다.
