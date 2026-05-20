# 장비 콘텐츠 V1 자산화 closure implement

- 상태: active
- 소유자: codex
- 최종수정일: 2026-05-20
- 소스오브트루스: `tasks/049_equipment_content_assetization_v1_closure/plan.md`

## Phase summary

GPT-Pro 제출과 Pindoc Decision으로 item/affix/crafting 범위를 먼저 닫았다.
최종 V1 범위는 item `42`, affix `30`, live affix `24`, reserved affix `6`, fixed `15 Echo` single-affix `Refit`이다.

코드 단계에서는 `EquipmentContentV1Contract`, `EquipmentContentV1Assetizer`, `EquipmentContentV1CatalogValidator`를 추가해 seed, validator, asset authoring이 같은 manifest를 보게 했다.
runtime 단계에서는 item reward settlement가 `RewardType.Item`을 generated inventory item으로 만들고, refit 후보가 live `Prefix` / `Suffix` affix 안에서만 나오도록 보정했다.

asset 단계에서는 기존 item/affix/drop table asset을 V1 manifest에 맞춰 패치했다.
reserved affix는 asset으로 유지하지만 live roll에서는 제외했다.

## Deviations

초기 계획의 material source와 crafting rail은 열지 않았다.
Pindoc 결정상 V1은 제작 시스템 전체가 아니라 `Refit` 보정 축만 닫는 범위였기 때문이다.

drop table 전체 `content-validate`는 아직 실패하지만, 실패 원인은 장비 콘텐츠가 아니라 encounter answer lane, support gate anchor, reward answer-lane site contract, encounter family distribution 쪽이다.
장비 관련 item/affix/drop/refit 오류는 제거됐다.

## Blockers

장비 V1 closure 자체의 blocking issue는 없다.
다만 전체 content validator green을 release gate로 삼으려면 encounter/support routing 잔여 task를 별도로 닫아야 한다.

## Diagnostics

- `EquipmentContentV1Assetizer.Apply` 실행 성공
- `test-batch-fast` 통과
- focused `EquipmentAssets_ExposeV1AssetizationContract` 통과
- `content-validate` 장비 관련 오류 없음
- `content-validate` 잔여 오류 `51`: `encounter.answer_lane_assignment 28`, `skill.support_gate_anchor 12`, `reward.answer_lane_site_contract 7`, `encounter.family_distribution 4`

## why this loop happened

장비는 UI 표현 규칙이 먼저 닫혔지만 실제 item/affix/drop asset은 placeholder 성격이 남아 있었다.
그 결과 장비 UI가 예뻐져도 목록 안의 의미 단위가 덜 닫힌 것처럼 보였고, crafting/refit 범위도 material rail과 섞여 보였다.
이번 루프는 표현 문제가 아니라 콘텐츠 자산화 문제를 닫기 위해 item/affix/drop/refit manifest를 하나로 묶었다.
