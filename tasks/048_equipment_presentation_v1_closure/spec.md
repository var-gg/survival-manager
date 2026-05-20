# 장비 표현 V1 closure spec

- 상태: active
- 소유자: codex
- 최종수정일: 2026-05-20
- 소스오브트루스: `pindoc://decision-equipment-presentation-v1-contract`
- 관련문서:
  - `docs/02_design/meta/item-and-affix-system.md`
  - `docs/02_design/meta/equipment-family-and-crafting-depth.md`
  - `docs/02_design/systems/rarity-ladder-contract.md`

## Goal

InventoryTab과 EquipmentRefit이 같은 장비 표현 계약을 쓰도록 닫는다.
장비 cell, item icon, rarity, identity, affix row, refit CTA를 ArtBible role과 공통 mapper로 고정해 패널별 표현 차이를 줄인다.

## Authoritative boundary

binding 결정은 `pindoc://decision-equipment-presentation-v1-contract`다.
repo 문서는 코드와 validator가 기대하는 launch-floor contract만 보강한다.

## In scope

- ArtBible registry에 장비 role 추가
- RuntimePanelTheme shared class 추가
- FastUnit conformance guard 확장
- `EquipmentPresentationPolicy` 추가
- InventoryTab / EquipmentRefit ViewState, Presenter, View, USS migration
- item/affix icon resolver delegate 명명 정리
- 기존 item icon과 최소 L3 plate/badge asset 상태 점검

## Out of scope

- recipe crafting UI
- material rail
- salvage result screen
- source/provenance badge
- locked state persistence
- rolled affix value persistence
- rarity별 ornate frame
- Legendary/Unique visual rarity ladder

## asmdef impact

새 asmdef는 만들지 않는다.
새 타입은 `SM.Unity` 안의 presentation policy로 둔다.
FastUnit은 `SM.Tests.FastUnit`에 유지한다.

## persistence impact

없다.
`InventoryItemRecord`는 source, lock, rolled affix value를 아직 갖지 않는다.
이번 작업은 해당 정보를 UI에서 임의 생성하지 않는다.

## validator / test oracle

- `UiArtBibleConformanceFastTests`
- `EquipmentPresentationPolicyTests`
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/test-harness-lint.ps1`
- 문서 변경 검증 스크립트

## done definition

- InventoryTab과 EquipmentRefit이 같은 presentation mapper를 사용한다.
- 두 패널이 `sm-item-*`, `sm-affix-*`, `sm-operation--refit` role class를 사용한다.
- ArtBible guard가 장비 role 누락, forbidden ornate frame, forbidden rarity class를 잡는다.
- V1 forbidden crafting/rarity surface가 UI에 생기지 않는다.
- fast tests와 문서 검증이 통과한다.

## deferred

- `Magic`/`Legendary` live authoring 정책의 hard validator 승격
- item provenance persistence
- locked state persistence
- rolled affix value persistence
- future crafting station / material economy
