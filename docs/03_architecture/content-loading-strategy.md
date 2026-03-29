# 콘텐츠 로딩 전략

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/content-loading-strategy.md`
- 관련문서:
  - `docs/03_architecture/content-authoring-model.md`
  - `docs/03_architecture/content-seed-assets.md`
  - `docs/05_setup/first-playable-bootstrap.md`

## 목적

이 문서는 prototype 단계의 sample content 로딩 계약을 하나로 고정한다.

## 현재 채택안

- sample content root: `Assets/Resources/_Game/Content/Definitions/**`
- runtime load contract: `Resources.LoadAll("_Game/Content/Definitions/Stats")`
- bootstrap menu: `SM/Bootstrap/Ensure Sample Content`

## 현재 역할 분담

### editor 측

- `SampleSeedGenerator`는 `Assets/Resources/_Game/Content/Definitions/**`에 asset을 생성한다.
- 반복 실행 시 기존 asset을 재사용하거나 갱신하는 idempotent 흐름을 유지한다.

### runtime 측

- `GameBootstrap`은 `Resources` 경로만 사용한다.
- 최소 검증 기준은 `Stats` asset 존재 여부다.

### bootstrap 측

- `SM/Bootstrap/Ensure Sample Content`는 sample content가 없을 때만 seed 생성 흐름을 실행한다.

## 향후 방향

장기적으로는 catalog/bootstrap asset 방식으로 이동할 수 있다.
다만 현재 prototype에서는 clean clone 재현성과 단순성을 우선한다.
