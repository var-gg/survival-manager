# 장비 콘텐츠 V1 자산화 closure status

- 상태: completed
- 소유자: codex
- 최종수정일: 2026-05-20
- 소스오브트루스: `tasks/049_equipment_content_assetization_v1_closure/spec.md`

## Current state

장비 콘텐츠 V1 자산화는 구현됐다.
item `42`, affix `30`, live affix `24`, reserved affix `6`, skirmish/elite/boss item drop, fixed `15 Echo` refit이 같은 manifest와 validator를 따른다.
runtime reward item 생성도 fake/manual item 경로가 아니라 item definition 기반 generated inventory item을 사용한다.

## Acceptance matrix

- [x] GPT-Pro 검수 완료
- [x] Pindoc Decision 발행
- [x] V1 item/affix/drop manifest 추가
- [x] assetizer 추가 및 sample seed 재적용 경로 연결
- [x] item asset 42개 rarity/identity 정리
- [x] affix asset 30개 live/reserved 정리
- [x] drop table required item entry를 `RewardType.Item`으로 고정
- [x] generated inventory item affix builder 추가
- [x] refit 후보를 live `Prefix` / `Suffix`로 제한
- [x] focused BatchOnly test 추가
- [x] repo design docs 갱신

## Evidence

- `pindoc://decision-equipment-content-v1-assetization-contract`
- GPT-Pro 제출 응답 회수
- `pwsh -File tools/unity-execute-method.ps1 -Method 'SM.Editor.SeedData.EquipmentContentV1Assetizer.Apply' -LogFile 'Logs/equipment-content-v1-assetizer.log' -PhaseName 'equipment content v1 assetization'`
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast` — 333 total / 329 passed / 0 failed / 4 skipped
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter EquipmentAssets_ExposeV1AssetizationContract` — 1 total / 1 passed / 0 failed
- `pwsh -File tools/unity-bridge.ps1 content-validate` — 장비 콘텐츠 관련 오류 없음, 비장비 잔여 오류 51개
- `pwsh -File tools/test-harness-lint.ps1`
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## Remaining blockers

장비 콘텐츠 V1 범위의 blocker는 없다.
전체 `content-validate` green을 막는 잔여 오류는 별도 도메인이다.

- `encounter.answer_lane_assignment`: 28
- `skill.support_gate_anchor`: 12
- `reward.answer_lane_site_contract`: 7
- `encounter.family_distribution`: 4

## Deferred / debug-only

- material crafting rail
- recipe crafting
- salvage loop
- source/provenance persistence
- locked affix persistence
- rolled affix value persistence
- reserved affix live 승격
- 장비 bitmap frame 추가 제작

## Loop budget consumed

- GPT-Pro 제출 1회
- Pindoc Decision 1회
- Unity assetizer execute loop 2회
- FastUnit 1회
- focused BatchOnly 1회
- content validator 1회
- docs validation 1회

## Handoff notes

다음 작업은 encounter/support routing validator 잔여 51개를 별도 task로 닫는 것이다.
장비 쪽을 더 열 때는 reserved affix 승격, material crafting, recipe, source/lock/rolled value persistence 중 하나를 골라 새 task로 분리한다.
