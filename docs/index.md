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
- 중앙 deprecated registry: `00_governance/deprecated-docs-registry.md`
- source-of-truth matrix: `00_governance/source-of-truth-matrix.md`
- docs eval 초안: `00_governance/docs-evals.md`
- 구현 검수 체크리스트: `00_governance/implementation-review-checklist.md`
- 제품 인덱스: `01_product/index.md`
- 디자인 인덱스: `02_design/index.md`
- 아키텍처 인덱스: `03_architecture/index.md`
- 의사결정 인덱스: `04_decisions/index.md`
- 설정 인덱스: `05_setup/index.md`
- 운영 인덱스: `06_production/index.md`

## localization 빠른 링크

- 정책: `02_design/ui/localization-policy.md`
- 구조: `03_architecture/localization-runtime-and-content-pipeline.md`
- 결정: `04_decisions/adr-0016-localization-boundary.md`
- 운영 절차: `05_setup/localization-workflow.md`

## 현재 전투 기준 빠른 링크

- 출시 기준 허브: `02_design/systems/launch-content-scope-and-balance.md`
- launch floor archetype matrix: `02_design/systems/launch-floor-content-matrix.md`
- 로스터와 archetype: `02_design/deck/roster-archetype-launch-scope.md`
- skill taxonomy와 데미지 모델: `02_design/combat/skill-taxonomy-and-damage-model.md`
- item / passive / augment budget: `02_design/meta/item-passive-augment-budget.md`
- passive board node catalog: `02_design/meta/passive-board-node-catalog.md`
- synergy breakpoint와 soft counter: `02_design/meta/synergy-breakpoints-and-soft-counters.md`
- synergy family catalog: `02_design/meta/synergy-family-catalog.md`
- authoring과 balance data 경계: `03_architecture/content-authoring-and-balance-data.md`
- 전장과 카메라: `02_design/combat/battlefield-and-camera.md`
- 배치와 앵커: `02_design/combat/deployment-and-anchors.md`
- 실시간 체감 시뮬레이션 모델: `02_design/combat/realtime-simulation-model.md`
- 팀 전술과 유닛 규칙: `02_design/combat/team-tactics-and-unit-rules.md`
- 전투 스탯과 파워 예산: `02_design/combat/stat-system-and-power-budget.md`
- squad blueprint와 빌드 소유권: `02_design/systems/squad-blueprint-and-build-ownership.md`
- 전투 런타임 아키텍처: `03_architecture/combat-runtime-architecture.md`
- editor sandbox tooling: `03_architecture/editor-sandbox-tooling.md`
- 전투 상태와 이벤트 모델: `03_architecture/combat-state-and-event-model.md`
- loadout compiler와 battle snapshot: `03_architecture/loadout-compiler-and-battle-snapshot.md`
- sim sweep과 balance KPI: `03_architecture/sim-sweep-and-balance-kpis.md`
- replay persistence와 run audit: `03_architecture/replay-persistence-and-run-audit.md`
- ADR-0014: `04_decisions/adr-0014-grid-deployment-continuous-combat.md`
- ADR-0015: `04_decisions/adr-0015-build-compile-audit-pipeline.md`
