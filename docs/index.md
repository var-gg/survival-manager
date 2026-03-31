# 문서 체계 인덱스

이 디렉터리는 `survival-manager`의 공식 문서 체계를 담는다.
현재 저장소 phase는 `prototype`이며, 문서는 실제 구현 상태와 운영 기준을 반영해야 한다.

## 문서 계층

- `00_governance/`: 문서 운영 원칙, 용어, 명명 규칙
- `01_product/`: 제품 목표, 범위, 사용자 가치
- `02_design/`: 게임/UX/시스템 디자인 문서
- `03_architecture/`: 기술 구조와 경계 정의
- `04_decisions/`: ADR과 주요 기술/운영 결정
- `05_setup/`: 개발 환경 및 초기 설정 절차
- `06_production/`: 플레이테스트/운영 기준 문서

## 운영 원칙

- 문서는 한 문서 한 목적을 따른다.
- 폴더 단위 인덱스로 탐색성을 유지한다.
- 구조 변경 시 관련 인덱스와 링크를 함께 갱신한다.
- 지속 문서는 한국어로 유지한다.
- 파일명, 코드, API 식별자는 영어를 유지한다.
- 기본 시작 컨텍스트는 `AGENTS.md` -> `docs/index.md` -> 관련 폴더 `index.md` -> 현재 task 상태 문서 순서를 따른다.
- active index는 active/draft 문서만 노출하고 deprecated 문서는 중앙 registry로 관리한다.

## 시작점

- 문서 거버넌스: `00_governance/docs-governance.md`
- 문서 하네스와 lifecycle: `00_governance/docs-harness.md`
- source-of-truth matrix: `00_governance/source-of-truth-matrix.md`
- docs eval 초안: `00_governance/docs-evals.md`
- task 실행 패턴: `00_governance/task-execution-pattern.md`
- 구현 검수 체크리스트: `00_governance/implementation-review-checklist.md`
- 제품 인덱스: `01_product/index.md`
- 디자인 인덱스: `02_design/index.md`
- 아키텍처 인덱스: `03_architecture/index.md`
- 의사결정 인덱스: `04_decisions/index.md`
- 설정 인덱스: `05_setup/index.md`
- 운영 인덱스: `06_production/index.md`

## Unity 에이전트 하네스 빠른 링크

- 하네스 상위 계약: `03_architecture/unity-agent-harness-contract.md`
- Editor iteration / asset authoring 규칙: `03_architecture/unity-editor-iteration-and-asset-authoring.md`
- asmdef / persistence ownership 규칙: `03_architecture/assembly-boundaries-and-persistence-ownership.md`
- validator / acceptance oracle 규칙: `03_architecture/validation-and-acceptance-oracles.md`
- Unity MCP capability 계약: `03_architecture/unity-mcp-tooling-contract.md`
- 현재 retrofit parent task: `../tasks/004_launch_floor_catalog_closure/status.md`

## localization 빠른 링크

- 정책: `02_design/ui/localization-policy.md`
- 구조: `03_architecture/localization-runtime-and-content-pipeline.md`
- 결정: `04_decisions/adr-0016-localization-boundary.md`
- 운영 절차: `05_setup/localization-workflow.md`

## 현재 전투 기준 빠른 링크

- 출시 기준 허브: `02_design/systems/launch-content-scope-and-balance.md`
- launch floor archetype matrix: `02_design/systems/launch-floor-content-matrix.md`
- roster와 archetype: `02_design/deck/roster-archetype-launch-scope.md`
- chapter/site 진행: `02_design/meta/campaign-chapter-and-expedition-sites.md`
- 조우 카탈로그: `02_design/combat/encounter-catalog-and-scaling.md`
- 상태이상 taxonomy: `02_design/combat/status-effects-cc-and-cleanse-taxonomy.md`
- 스킬 키워드와 support restriction: `02_design/combat/skill-keywords-support-modifiers-and-weapon-restrictions.md`
- 드롭/source matrix: `02_design/meta/drop-table-rarity-bracket-and-source-matrix.md`
- 장비/제작 깊이: `02_design/meta/equipment-family-and-crafting-depth.md`
- PVP arena ruleset: `02_design/meta/pvp-ruleset-and-arena-loop.md`
- skill taxonomy와 데미지 모델: `02_design/combat/skill-taxonomy-and-damage-model.md`
- battle presentation contract: `02_design/combat/battle-presentation-contract.md`
- combat spatial contract: `02_design/combat/combat-spatial-contract.md`
- combat behavior contract: `02_design/combat/combat-behavior-contract.md`
- mobility contract: `02_design/combat/mobility-contract.md`
- combat mechanics glossary: `02_design/combat/combat-mechanics-glossary.md`
- squad blueprint와 빌드 소유권: `02_design/systems/squad-blueprint-and-build-ownership.md`
- 전투 런타임 아키텍처: `03_architecture/combat-runtime-architecture.md`
- battle harness/debug contract: `03_architecture/combat-harness-and-debug-contract.md`
- 조우 resolve 구조: `03_architecture/encounter-authoring-and-runtime-resolution.md`
- status runtime stack: `03_architecture/status-runtime-stack-and-cleanse-rules.md`
- 드롭/ledger pipeline: `03_architecture/drop-resolution-and-ledger-pipeline.md`
- skill tag catalog: `03_architecture/skill-tag-catalog-and-compatibility-resolution.md`
- arena snapshot 계약: `03_architecture/arena-snapshot-matchmaking-and-season-contract.md`
- replay persistence와 run audit: `03_architecture/replay-persistence-and-run-audit.md`
- ADR-0014: `04_decisions/adr-0014-grid-deployment-continuous-combat.md`
- ADR-0015: `04_decisions/adr-0015-build-compile-audit-pipeline.md`
