# Encounter/support routing validator closure status

- 상태: completed
- 소유자: codex
- 최종수정일: 2026-05-20
- 소스오브트루스: `tasks/050_encounter_support_routing_validator_closure/spec.md`

## Current state

10-site / 40-encounter matrix와 support gate anchor 정책은 Pindoc Decision으로 확정됐다.
validator code와 repo design docs는 새 contract로 패치됐고, `content-validate`는 0 error / 0 warning으로 통과했다.
이제 장비 콘텐츠 V1 이후 남아 있던 encounter/support routing validator blocker는 없다.

## Acceptance matrix

- [x] GPT-Pro focused audit 완료
- [x] Pindoc Decision 발행
- [x] answer lane allowlist 10개로 교체
- [x] encounter family exact manifest 추가
- [x] old `2~4` family range 제거
- [x] support global allowlist 추가
- [x] support gate anchor validator 기준 변경
- [x] repo docs 갱신
- [x] `content-validate` 0 error / 0 warning 확인
- [x] fast/docs/smoke 검증 완료

## Evidence

- `pindoc://decision-launch-encounter-matrix-support-gate-anchor`
- GPT-Pro 제출 응답 회수
- `pwsh -File tools/unity-bridge.ps1 content-validate` — 0 errors / 0 warnings
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast` — 333 total / 329 passed / 0 failed / 4 skipped
- `pwsh -File tools/test-harness-lint.ps1`
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## Remaining blockers

없음.

## Deferred / debug-only

- encounter difficulty tuning
- reward drop weight tuning
- boss overlay ask tuning
- site별 presentation pass

## Loop budget consumed

- GPT-Pro 제출 1회
- Pindoc Decision 1회
- content validator 1회
- FastUnit 1회
- docs/smoke validation 1회

## Handoff notes

encounter/support routing validator closure는 완료됐다.
다음 작업은 encounter difficulty tuning, reward weight tuning, boss overlay ask tuning, site presentation pass 중 하나를 새 task로 분리해 열면 된다.
