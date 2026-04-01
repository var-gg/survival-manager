# 작업 계획

## 메타데이터

- 작업명: Loop C Content Governance Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-01
- 의존:
  - `tasks/009_loop_a_contract_closure/status.md`
  - `tasks/010_loop_b_recruitment_economy_closure/status.md`

## Preflight

- `BudgetCard`, `ContentRarity`, counter topology, forbidden policy의 코드 잔여 gap을
  먼저 찾는다.
- compile error와 validation blocker를 먼저 0으로 만든다.
- docs start context는 `AGENTS.md -> docs/index.md -> 관련 index -> task status`만 쓴다.
- town UI와 dev UI의 노출 경계를 먼저 고정하고 문서에 그대로 반영한다.

## Phase 1 code-only

- 공통 governance enum과 `BudgetCard` source-of-truth를 `SM.Content`에 추가한다.
- runtime lookup/template/snapshot에 compact governance summary를 전달한다.
- validator pass와 audit export를 붙인다.
- town UI와 sandbox/debug UI를 분리한다.

## Phase 2 asset authoring

- archetype/skill/passive/mobility/affix/augment/synergy/status asset에
  `BudgetCard`와 Loop C metadata를 채운다.
- permanent augment floor와 fallback content를 validator가 통과하도록 보정한다.
- scenario/harness seed와 fallback data를 같은 규칙으로 맞춘다.

## Phase 3 validation

- `pwsh -File tools/unity-bridge.ps1 compile`
- content validation batch 실행과 audit artifact 확인
- `pwsh -File tools/unity-bridge.ps1 test-edit`
- `pwsh -File tools/unity-bridge.ps1 test-play`
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## rollback / escape hatch

- compile이 계속 red면 asset authoring보다 schema/validator 책임 경계부터 다시 줄인다.
- `test-edit` 결과 회수가 불안정하면 compile + validator + targeted exec evidence를 먼저
  status에 남기고, runner channel 문제를 별도 blocker로 기록한다.
- recruit economy와 loot rarity 설계를 다시 열어야 하는 요구가 나오면 Loop C 범위에서
  분리한다.

## tool usage plan

- file-first로 source-of-truth 문서와 task packet을 먼저 쓴다.
- Unity compile/validation은 `tools/unity-bridge.ps1`를 우선 사용한다.
- trivial inspect는 shell/`rg`로 끝내고, scene/prefab typed guardrail이 필요한 경우에만
  MCP를 쓴다.

## loop budget

- compile-fix 허용 횟수: 3
- refresh/read-console 반복 허용 횟수: 2
- blind asset generation 재시도 허용 횟수: 1
