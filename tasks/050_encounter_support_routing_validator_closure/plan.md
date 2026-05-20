# Encounter/support routing validator closure plan

- 상태: completed
- 소유자: codex
- 최종수정일: 2026-05-20
- 소스오브트루스: `tasks/050_encounter_support_routing_validator_closure/spec.md`

## Preflight

- `content-validate` 잔여 오류 종류와 count 확인
- `$gpt-pro-submit` focused audit 제출
- Pindoc Decision 발행
- dirty worktree 확인
- `$docs-maintainer`, `$code-structure-guard` 기준 적용

## Phase 1 code-only

1. 10개 canonical answer lane allowlist 적용
2. 40 encounter exact family distribution manifest 추가
3. old `2~4` family range validator 제거
4. global support allowlist 추가
5. support gate anchor validator를 required weapon/class fields로 이동

## Phase 2 asset authoring

현재 authored assets는 10-site / 40-encounter matrix와 routed drop entry를 이미 보유한다.
`content-validate` 결과상 추가 asset patch는 필요하지 않다.
seed generator는 기존 10-site seed를 유지한다.

## Phase 3 validation

- `pwsh -File tools/unity-bridge.ps1 content-validate`
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/test-harness-lint.ps1`
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## rollback / escape hatch

`content-validate`가 새 exact manifest에서 실패하면 seed generator의 `GetCampaignSiteSeeds()`와 current encounter assets를 먼저 비교한다.
old 6-site allowlist로 rollback하지 않는다.
support policy가 extra support modifier와 충돌하면 extra support만 별도 contract로 분리하고 canonical 12 support contract는 유지한다.

## tool usage plan

- 파일 탐색은 `rg`와 `Get-Content`
- 수동 편집은 `apply_patch`
- 설계 검수는 `$gpt-pro-submit`
- Pindoc 결정은 `pindoc_artifact_propose`
- Unity 검증은 `tools/unity-bridge.ps1`

## loop budget

이번 루프는 validator/docs/contract closure다.
encounter tuning과 reward balance는 같은 루프에서 열지 않는다.
