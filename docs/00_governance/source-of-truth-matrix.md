# Source-of-truth matrix

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-08
- 소스오브트루스: `docs/00_governance/source-of-truth-matrix.md`
- 관련문서:
  - `docs/00_governance/docs-harness.md`
  - `docs/00_governance/docs-governance.md`
  - `docs/03_architecture/index.md`
  - `docs/03_architecture/coding-principles.md`
  - `docs/00_governance/implementation-review-checklist.md`
  - `docs/05_setup/index.md`
  - `docs/06_production/index.md`

## 목적

이 문서는 같은 주제를 여러 문서가 역할 분담해서 다룰 때, 어떤 문서를 어떤 맥락에서 우선 읽어야 하는지 기록한다.
역할 분리가 분명하면 무조건 삭제하지 않고, 읽기 순서와 우선순위를 명확히 한다.

## 작성 규칙

- 한 행은 하나의 문서군만 다룬다.
- `정책 / 결정`에는 ADR 또는 거버넌스 기준 문서를 둔다.
- `setup / 운영`에는 절차 문서를 둔다.
- `live state`에는 현재 rollout 상태나 handoff 문서를 둔다.
- `routing asset`에는 prompt/skill 같은 에이전트 전용 자산을 둔다.
- 역할이 겹치기 시작하면 기존 문서를 더 늘리기 전에 matrix를 먼저 갱신한다.

## 문서군 표

| 문서군 | 정책 / 결정 | setup / 운영 | live state | routing asset | 비고 |
| --- | --- | --- | --- | --- | --- |
| docs harness | `AGENTS.md`; `docs/00_governance/docs-governance.md`; `docs/04_decisions/adr-0017-docs-context-harness.md` | `docs/00_governance/docs-harness.md`; `docs/00_governance/docs-evals.md` | `tasks/003_docs_harness_hardening/status.md` | `.agents/skills/docs-maintainer/SKILL.md` | 기본 시작 컨텍스트는 AGENTS -> docs index -> 폴더 index -> task status 순서를 따른다. |
| code structure governance | `docs/03_architecture/coding-principles.md`; `docs/03_architecture/dependency-direction.md`; `docs/04_decisions/adr-0012-code-structure-and-dependency-policy.md` | `docs/00_governance/implementation-review-checklist.md`; `docs/03_architecture/unity-boundaries.md`; `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md` | `tasks/014_code_structure_hot_path_routing/status.md` | `.agents/skills/code-structure-guard/SKILL.md` | 새 asmdef, 큰 파일 확장, validator/runtime/persistence 경계 변경 전에는 이 행을 먼저 확인한다. |
| vertical slice live state | `docs/01_product/mvp-vertical-slice.md`; `docs/04_decisions/adr-0014-grid-deployment-continuous-combat.md` | `docs/06_production/index.md`; `docs/06_production/mvp-playtest-checklist.md`; `docs/06_production/pre-art-release-floor.md` | `tasks/001_mvp_vertical_slice/status.md` | 없음 | 현재 플레이어블 경계, 리스크, 다음 우선순위는 task status를 우선하고, paid asset pass 직전 floor/packet 규칙은 pre-art release floor를 따른다. |
| Unity tooling lane | `docs/04_decisions/adr-0008-editor-bridge-policy.md`; `docs/04_decisions/adr-0011-mcp-adoption-policy.md`; `docs/04_decisions/adr-0013-unity-cli-hybrid-lane.md` | `docs/05_setup/unity-mcp.md`; `docs/05_setup/unity-cli.md`; `docs/05_setup/unity-long-running-workloads.md`; `docs/05_setup/codex-mcp-setup.md`; `docs/05_setup/community-mcp-setup.md`; `docs/05_setup/mcp-safety-rules.md` | `tasks/2026-03-unity-cli-local-lane/status.md`; `tasks/013_unity_long_running_workload_lane/status.md` | `prompts/unity-cli-hybrid-ops.md` | policy는 ADR, 설치/운영은 setup docs, 장시간 workload routing과 현재 rollout 상태는 task status, routing snippet은 prompt가 맡는다. |
| Unity agent harness | `AGENTS.md`; `docs/03_architecture/unity-agent-harness-contract.md`; `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md` | `docs/03_architecture/unity-editor-iteration-and-asset-authoring.md`; `docs/03_architecture/validation-and-acceptance-oracles.md`; `docs/03_architecture/unity-mcp-tooling-contract.md`; `docs/00_governance/task-execution-pattern.md` | `tasks/004_launch_floor_catalog_closure/status.md` | `.agents/skills/docs-maintainer/SKILL.md` | task shaping, phase gate, validator-first, loop budget, tooling contract를 이 행으로 묶는다. |
