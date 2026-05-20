# Encounter/support routing validator closure implement

- 상태: active
- 소유자: codex
- 최종수정일: 2026-05-20
- 소스오브트루스: `tasks/050_encounter_support_routing_validator_closure/plan.md`

## Phase summary

GPT-Pro focused audit와 Pindoc Decision으로 10-site / 40-encounter matrix를 canonical으로 확정했다.
old 6-site / 24-encounter matrix는 validator/docs drift로 분류했다.

코드 단계에서는 `FirstPlayableAuthoringContract`에 10개 answer lane allowlist, exact family count manifest, global support allowlist, support gate contract를 추가했다.
`EncounterAuthoringCatalogValidator`는 old `2~4` range 대신 exact manifest를 검증하게 바꿨다.
`SkillCatalogValidator`는 support gate anchor를 `SupportAllowedTags`가 아니라 `RequiredWeaponTags` / `RequiredClassTags`에서 판정하도록 바꿨다.

문서 단계에서는 launch encounter matrix, campaign site matrix, drop routing, support keyword 문서를 current contract와 맞췄다.

## Deviations

추가 asset patch는 하지 않았다.
`content-validate` 결과상 current assets는 10-site matrix와 support gate fields를 이미 충족했고, 오류 원인은 validator와 stale docs였다.

## Blockers

없음.

## Diagnostics

- 시작 오류: `content-validate` 51 errors / 0 warnings
- 주요 오류: `encounter.answer_lane_assignment`, `reward.answer_lane_site_contract`, `encounter.family_distribution`, `skill.support_gate_anchor`
- 1차 코드 패치 후 결과: `content-validate` 0 errors / 0 warnings

## why this loop happened

campaign encounter content가 10-site / 40-encounter 형태로 진화했지만, 일부 validator와 repo docs가 6-site / 24-encounter 시절의 allowlist와 family distribution rule을 유지했다.
support modifier도 문서상 compatibility filter와 gate anchor가 분리되어 있었으나 validator는 include tag에서 gate anchor를 찾고 있었다.
이번 루프는 content 자체를 다시 설계하기보다 이미 생성된 current contract를 validator와 문서에 반영한 closure다.
