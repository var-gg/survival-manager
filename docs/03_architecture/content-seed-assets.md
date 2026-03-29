# 콘텐츠 시드 자산

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/content-seed-assets.md`
- 관련문서:
  - `docs/03_architecture/content-loading-contract.md`
  - `docs/03_architecture/content-loading-strategy.md`
  - `docs/03_architecture/content-authoring-model.md`

## 목적

이 문서는 sample seed generator가 만드는 초기 콘텐츠 자산 범위를 기록한다.

## 자산 생성 위치

- 생성 메뉴: `SM/Seed/Generate Sample Content`
- legacy migration 메뉴: `SM/Seed/Migrate Legacy Sample Content`
- 생성 경로: `Assets/Resources/_Game/Content/Definitions/**`
- 검증 메뉴: `SM/Validation/Validate Content Definitions`

## 주요 정의 타입

- `StatDefinition`
- `RaceDefinition`
- `ClassDefinition`
- `TraitPoolDefinition`
- `UnitArchetypeDefinition`
- `SkillDefinitionAsset`
- `AugmentDefinition`
- `ItemBaseDefinition`
- `AffixDefinition`
- `ExpeditionDefinition`
- `RewardTableDefinition`

## 현재 권장 시드 수량

- race: 3
- class: 4
- archetype: 8
- temporary augment: 9
- permanent augment: 3
- item base: 6
- affix: 8
