# 장비 표현 V1 closure plan

- 상태: active
- 소유자: codex
- 최종수정일: 2026-05-20
- 소스오브트루스: `tasks/048_equipment_presentation_v1_closure/spec.md`

## Preflight

- Pindoc context 확인
- `pindoc://decision-equipment-presentation-v1-contract` 발행
- dirty worktree 확인
- 관련 docs / UI / test 파일 읽기
- `$docs-maintainer`, `$code-structure-guard` 기준 적용

## Phase 1 code-only

1. ArtBible registry에 equipment role 추가
2. RuntimePanelTheme에 shared item/affix/operation class 추가
3. `EquipmentPresentationPolicy` 추가
4. InventoryTab migration
5. EquipmentRefit migration
6. icon resolver delegate 명명 정리
7. FastUnit guard와 policy test 추가

## Phase 2 asset authoring

기존 `Assets/Resources/_Game/Art/Icons/Item/**` 6종을 V1 canonical item icon으로 둔다.
새 ornate frame은 만들지 않는다.
필요하면 L3 plate/badge bitmap만 별도 task로 분리한다.

## Phase 3 validation

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/test-harness-lint.ps1`
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## rollback / escape hatch

ArtBible guard가 과도하게 잡으면 role 적용 범위를 `InventoryTab`과 `EquipmentRefit`으로만 좁힌다.
presentation mapper가 기존 content와 충돌하면 fallback은 유지하고 hard fail은 후속 validator task로 분리한다.

## tool usage plan

- 파일 탐색은 `rg`와 `Get-Content`
- 수동 편집은 `apply_patch`
- Unity 검증은 `tools/unity-bridge.ps1 test-batch-fast`
- Pindoc 결정은 MCP propose 사용

## loop budget

이번 루프의 목표는 장비 표현 계약과 구현을 함께 닫는 것이다.
future crafting, provenance, rolled affix value는 deferred로 남기고 이 루프에서 열지 않는다.
