# 장비 콘텐츠 V1 자산화 closure spec

- 상태: active
- 소유자: codex
- 최종수정일: 2026-05-20
- 소스오브트루스: `pindoc://decision-equipment-content-v1-assetization-contract`
- 관련문서:
  - `docs/02_design/meta/item-and-affix-system.md`
  - `docs/02_design/meta/affix-pool-v1.md`
  - `docs/02_design/meta/equipment-family-and-crafting-depth.md`
  - `docs/02_design/meta/drop-table-rarity-bracket-and-source-matrix.md`
  - `tasks/048_equipment_presentation_v1_closure/status.md`

## Goal

스킬 목록처럼 장비도 실제 asset 수량과 live 생성 범위를 정형화한다.
item, affix, drop table, refit runtime이 같은 V1 manifest를 따르게 만들어 `개수는 있는데 실제 내용이 비어 있는` 상태를 제거한다.

## Authoritative boundary

콘텐츠 결정은 `pindoc://decision-equipment-content-v1-assetization-contract`가 소유한다.
repo 문서는 해당 결정을 코드/validator/asset authoring contract로 옮긴다.
기존 장비 표현 규칙은 `pindoc://decision-equipment-presentation-v1-contract`와 `tasks/048_equipment_presentation_v1_closure`를 따른다.

## In scope

- item catalog `42`개를 `Common 30 / Rare 9 / Epic 3`으로 고정
- item identity를 `Baseline 34 / Named 6 / Unique 2`로 고정
- affix catalog `30`개를 live `24`, reserved `6`으로 고정
- live affix mix를 `Implicit 6 / Prefix 12 / Suffix 6`으로 고정
- live family mix를 `CoreScalar 14 / ConditionalTagged 6 / BuildShaping 4`로 고정
- skirmish / elite / boss drop table에 `RewardType.Item` entry를 보장
- reward settlement가 `InventoryItemRecord`를 definition 기반 generated affix로 만든다
- `Refit` 후보는 `Prefix` / `Suffix` live affix로 제한
- content validator와 focused BatchOnly test로 회귀를 막는다

## Out of scope

- material crafting rail
- recipe crafting
- salvage loop
- item source/provenance persistence
- locked affix persistence
- rolled affix value persistence
- 장비 UI의 새 bitmap frame 제작
- rarity ladder 확장

## asmdef impact

새 asmdef는 만들지 않는다.
editor seed/validator 코드는 기존 `SM.Editor` 경계에 둔다.
runtime item generation은 기존 `SM.Unity` `GameSessionState` partial로 분리해 session reward/refit 흐름에만 붙인다.

## persistence impact

save schema는 변경하지 않는다.
`InventoryItemRecord`는 여전히 item base id와 affix id 목록만 들고, source, lock, rolled value는 저장하지 않는다.
V1 generated item은 definition id와 deterministic seed로 affix 조합을 만든다.

## validator / test oracle

- `EquipmentContentV1CatalogValidator`
- `EquipmentAssets_ExposeV1AssetizationContract`
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter EquipmentAssets_ExposeV1AssetizationContract`
- `pwsh -File tools/unity-bridge.ps1 content-validate`
- `pwsh -File tools/test-harness-lint.ps1`
- 문서 검증 스크립트

## done definition

- sample seed 재생성 후에도 V1 item/affix/drop/refit manifest가 유지된다.
- 모든 item asset이 V1 rarity, identity, `Reforge` operation, `echo` currency contract를 만족한다.
- live affix `24`개와 reserved affix `6`개가 validator로 구분된다.
- required item drop entry가 `RewardType.Item`으로 유지된다.
- runtime reward choice와 automatic drop이 item reward를 fake skill/manual item으로 만들지 않는다.
- focused BatchOnly test가 통과한다.
- 전체 `content-validate`에서 장비 콘텐츠 관련 오류가 남지 않는다.

## deferred

- material source matrix와 crafting economy
- recipe authoring schema
- salvage settlement
- source/provenance badge
- locked affix와 rolled value persistence
- reserved affix의 live 승격
