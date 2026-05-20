# Encounter/support routing validator closure spec

- 상태: active
- 소유자: codex
- 최종수정일: 2026-05-20
- 소스오브트루스: `pindoc://decision-launch-encounter-matrix-support-gate-anchor`
- 관련문서:
  - `docs/02_design/systems/launch-encounter-variety-and-answer-lane-matrix.md`
  - `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`
  - `docs/02_design/meta/drop-table-rarity-bracket-and-source-matrix.md`
  - `docs/02_design/combat/skill-keywords-support-modifiers-and-weapon-restrictions.md`
  - `tasks/049_equipment_content_assetization_v1_closure/status.md`

## Goal

장비 콘텐츠 V1 closure 이후 남은 `content-validate` 오류 51개를 encounter/support routing 범위에서 닫는다.
10-site / 40-encounter campaign matrix, answer lane allowlist, family distribution manifest, support modifier gate anchor 정책을 하나의 contract로 맞춘다.

## Authoritative boundary

정책 결정은 `pindoc://decision-launch-encounter-matrix-support-gate-anchor`가 소유한다.
repo 문서는 validator와 seed/content asset이 기대하는 현재 구현 contract만 반영한다.

## In scope

- `AllowedAnswerLaneIds`를 10-site canonical lane으로 교체
- encounter family distribution을 40 encounter exact manifest로 검증
- support gate anchor 판정을 `RequiredWeaponTags` / `RequiredClassTags` 기준으로 이동
- global support allowlist를 `support_brutal`, `support_swift`, `support_echo`, `support_lingering`으로 제한
- stale 6-site / 24-encounter docs를 10-site / 40-encounter contract로 갱신
- `content-validate` 0 error / 0 warning 확인

## Out of scope

- encounter roster 재설계
- enemy squad composition tuning
- reward economy 밸런스 재조정
- battle AI/targeting 변경
- P09/extra actor lore 또는 narrative beat 재작성

## asmdef impact

새 asmdef는 만들지 않는다.
변경은 기존 `SM.Editor` validator와 seed/content contract 문서에 한정한다.

## persistence impact

없다.
save schema, runtime profile, combat snapshot persistence를 변경하지 않는다.

## validator / test oracle

- `pwsh -File tools/unity-bridge.ps1 content-validate`
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/test-harness-lint.ps1`
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## done definition

- `content-validate`가 0 error / 0 warning이다.
- `encounter.answer_lane_assignment`, `reward.answer_lane_site_contract`, `encounter.family_distribution`, `skill.support_gate_anchor` 오류가 남지 않는다.
- old lane id가 active validator와 canonical docs에서 제거된다.
- exact family count manifest 합계가 40이다.
- support gate anchor는 `SupportAllowedTags`가 아니라 required weapon/class fields에서 검증된다.

## deferred

- encounter difficulty tuning
- reward drop weight tuning
- site별 UI/UX presentation pass
- boss overlay ask tuning
