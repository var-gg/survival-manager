# 콘텐츠 로딩 전략

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `docs/03_architecture/content-loading-strategy.md`
- 관련문서:
  - `docs/03_architecture/content-loading-contract.md`
  - `docs/03_architecture/content-authoring-model.md`
  - `docs/03_architecture/content-seed-assets.md`
  - `docs/05_setup/first-playable-bootstrap.md`

## 목적

이 문서는 prototype 단계에서 왜 현재 sample content 계약을 채택했는지 설명하는 **rationale 문서**다.
구체적 계약 자체는 `content-loading-contract.md`를 따른다.

## 현재 채택 전략

- canonical root는 `Assets/Resources/_Game/Content/Definitions/**`로 고정한다.
- default playable runtime은 `Resources.LoadAll("_Game/Content/Definitions/Stats")`만 본다.
- committed asset을 runtime truth로 유지한다.
- editor sweep / raw file fallback은 explicit diagnostic lane으로만 연다.
- 기본 복구 메뉴는 `SM/Setup/Ensure Sample Content`다.
- `SM/Setup/Generate Sample Content`는 repair/bootstrap 전용이다.

## 이 전략을 택한 이유

- clean clone 재현성을 우선하기 쉽다.
- prototype 단계에서 별도 catalog/bootstrap asset 없이도 바로 복구 가능하다.
- 런타임 쪽 판단을 최소 `Stats` 존재 여부로 단순화할 수 있다.
- editor code, scene asset, 테스트, MCP 검증 경로를 같은 contract 위에 올릴 수 있다.

## block24 기준 메모

- block24에서는 content contract와 진단 정확성을 먼저 고정한다.
- scene repair는 existing temporary bridge를 테스트에만 활용하고, 공식 계층은 block25에서 정리한다.
- legacy path가 남아 있어도 canonical root를 우선 복구한다.

## 관련 구현

- committed asset: runtime/content truth
- `SampleSeedGenerator`: bootstrap repair helper
- `SM/Setup/Migrate Legacy Sample Content`: legacy path 이동 시도
- `SM/Setup/Ensure Sample Content`: minimum bootstrap repair
- `GameBootstrap`: canonical root missing 시 부팅 차단
- `SceneIntegrityTests`: block24 repository integrity 확인

## 향후 방향

장기적으로는 catalog/bootstrap asset 방식으로 이동할 수 있다.
다만 현재 prototype에서는 clean clone 재현성과 단순성을 우선한다.
