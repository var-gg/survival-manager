# 03 아키텍처

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/03_architecture/index.md`
- 관련문서:
  - `docs/index.md`
  - `docs/04_decisions/index.md`
  - `docs/04_decisions/adr-0006-combat-sim-boundary.md`
  - `docs/04_decisions/adr-0014-grid-deployment-continuous-combat.md`

## 목적

이 폴더는 기술 구조, 코딩 경계, 의존 방향, Unity 특화 제약을 정의한다.

## Unity agent harness 문서

- `unity-agent-harness-contract.md`: Unity repo에서 task를 어떻게 shape하고 닫는지에 대한 상위 운영 계약
- `unity-editor-iteration-and-asset-authoring.md`: code-only / asset batch / validator / smoke 순서와 Editor state 규칙
- `assembly-boundaries-and-persistence-ownership.md`: `SM.Meta`, `SM.Unity`, `SM.Persistence.Abstractions` 경계와 asmdef 사전 점검 규칙
- `validation-and-acceptance-oracles.md`: validator-first, acceptance matrix, evidence 기록 규칙
- `unity-mcp-tooling-contract.md`: low-level loop를 high-level capability contract로 대체하는 기준

## 핵심 구조 문서

- `technical-overview.md`: 현재 구현과 문서 체계를 연결하는 상위 개요
- `bounded-contexts.md`: `SM.*` context 책임 분리 기준
- `coding-principles.md`: AI와 사람이 공유하는 코딩 원칙
- `dependency-direction.md`: asmdef/context/layer 의존 허용·금지 규칙
- `unity-boundaries.md`: `MonoBehaviour`, `ScriptableObject`, scene 책임 규칙
- `unity-project-layout.md`: 실제 폴더와 asmdef 배치
- `unity-scene-flow.md`: Boot 중심 scene flow 규칙

## 데이터와 콘텐츠 문서

- `data-model.md`: authored definition, runtime instance, save model 분리 기준
- `content-authoring-model.md`: 콘텐츠 authoring/validation 경계
- `content-authoring-and-balance-data.md`: launch authoring schema와 Loop C governance/audit 경계
- `content-pipeline.md`: 콘텐츠 intake와 파이프라인 운영 방향
- `content-loading-contract.md`: canonical content root와 runtime/editor 계약
- `content-loading-strategy.md`: MVP content loading 계약
- `content-seed-assets.md`: sample seed asset과 Markdown catalog/live subset 기준
- `combat-content-mapping.md`: spatial combat authored data와 runtime 매핑
- `encounter-authoring-and-runtime-resolution.md`: chapter/site/encounter resolve와 debug fallback 경계
- `recruit-offer-schema.md`: recruit preview, pack metadata, pity/scout state schema
- `unit-economy-schema.md`: wallet, retrain state, duplicate conversion, dismiss footprint schema
- `skill-tag-catalog-and-compatibility-resolution.md`: stable tag catalog와 compile/validation 계약
- `localization-runtime-and-content-pipeline.md`: localization runtime, content key, UI refresh 경계

## 전투 런타임 문서

- `combat-runtime-architecture.md`: live simulation 전투 런타임 책임 분리
- `combat-state-and-event-model.md`: 상태, status stack, typed event, 결과 모델 정의
- `combat-harness-and-debug-contract.md`: battle harness, 8-lane coverage, acceptance 시나리오 기준
- `telemetry-contract.md`: runtime telemetry taxonomy와 ExplainStamp source-of-truth
- `readability-gate-contract.md`: readability threshold, aggregation, fail semantics
- `first-playable-balance-targets.md`: PureKit / SystemicSlice / RunLite deterministic suite
- `pruning-playbook.md`: content health grade, prune rule, move-out-of-v1 기준
- `status-runtime-stack-and-cleanse-rules.md`: status stack / refresh / ownership runtime 규칙
- `editor-sandbox-tooling.md`: Unity combat sandbox/editor tooling 경계
- `loadout-compiler-and-battle-snapshot.md`: build -> compile -> battle snapshot 경계
- `sim-sweep-and-balance-kpis.md`: deterministic sweep, KPI, artifact, review/fail 규칙
- `replay-persistence-and-run-audit.md`: active run / replay / ledger persistence 기준

## 기타 운영 문서

- `arena-snapshot-matchmaking-and-season-contract.md`: async arena snapshot, local matchmaking, season contract
- `persistence-strategy.md`: persistence adapter 경계
- `testing-strategy.md`: 저비용 검증 우선 테스트 전략
- `asset-intake-boundary.md`: `Assets/ThirdParty/**` intake boundary

## 운영 메모

구조 정책을 바꾸면 관련 기준 문서와 `docs/04_decisions/` ADR을 같이 갱신한다.
