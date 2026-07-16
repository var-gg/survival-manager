# 의사결정 문서 인덱스

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-16
- 소스오브트루스: `docs/04_decisions/index.md`
- 관련문서:
  - `docs/index.md`
  - `docs/00_governance/docs-governance.md`

## 목적

`04_decisions/`는 저장소와 prototype 구현에 durable하게 영향을 주는 ADR을 모아 둔다.

## ADR 목록

- `adr-0001-docs-architecture.md`: 문서 체계와 거버넌스 구조 (superseded: Pindoc 전환)
- `adr-0002-prototype-phase.md`: prototype 단계 채택
- `adr-0003-unity-project-structure.md`: Unity 프로젝트 구조 채택
- `adr-0004-content-pipeline-boundary.md`: 콘텐츠 파이프라인 경계 채택
- `adr-0005-data-driven-content.md`: 데이터 주도 콘텐츠 방향 채택
- `adr-0006-combat-sim-boundary.md`: 전투 시뮬레이션 경계 채택
- `adr-0007-thirdparty-asset-policy.md`: 서드파티 에셋 정책 채택
- `adr-0008-editor-bridge-policy.md`: 에디터 브리지 정책 채택
- `adr-0009-persistence-boundary.md`: persistence 경계 채택
- `adr-0011-mcp-adoption-policy.md`: MCP 도입 정책
- `adr-0012-code-structure-and-dependency-policy.md`: 코드 구조와 의존 정책 채택
- `adr-0013-unity-cli-hybrid-lane.md`: Unity CLI hybrid lane 채택
- `adr-0014-grid-deployment-continuous-combat.md`: grid 배치 + continuous combat 채택
- `adr-0015-build-compile-audit-pipeline.md`: build-compile-audit 파이프라인 채택
- `adr-0016-localization-boundary.md`: localization 경계와 공식 패키지 채택
- `adr-0017-docs-context-harness.md`: 문서 컨텍스트 하네스와 tombstone registry 채택
- `adr-0018-loop-c-content-governance.md`: BudgetCard/ContentRarity/8-lane/fatal forbidden policy 채택
- `adr-0019-runtime-panel-host-ui-toolkit-first.md`: RuntimePanelHost, UITK asset-first, battle shell only 채택
- `adr-0020-session-realm-authority-boundary.md`: session realm authority boundary와 offline-first port seam 채택
- `adr-0021-character-definition-identity-layer.md`: `CharacterDefinition` identity layer와 localized battle metadata 채택
- `adr-0022-narrative-architecture.md`: 내러티브 아키텍처 (definitions in Content, runtime in Meta, presentation in Unity) 채택
- `adr-0023-meta-content-adapter-boundary.md`: `SM.Meta` content adapter boundary와 pure Meta asmdef 경계 채택
- `adr-0024-narrative-human-centric-reskin.md`: 내러티브 인간 중심 reskin (superseded: Pindoc 전환)
- `adr-0025-narrative-authoring-runtime-sync.md`: 내러티브 authoring↔runtime 동기화 거버넌스 — 3계층 하이브리드 SoT(pindoc 창작 / Git canonical manifest 계약 / Unity 파생), stable line_uid·다층 drift 게이트·AI edit protocol (draft, GPT Pro 자문 반영)
- `adr-0026-dossier-persistence-schema.md`: ludonarrative 루프 P1a — `DossierEntryRecord`(전투 결과 영속 ledger)를 `SM.Persistence.Abstractions.Models`에 추가, 분류 판정은 `SM.Meta.DossierOutcomeClassifier` 순수 코어, 집계는 `SM.Unity` settlement flow. combat 순수성 보존
- `adr-0027-warrant-judgment-architecture.md`: ludonarrative 루프 P2a — 출격 전 서약(Warrant)을 전투 사실(승패·생존·turn 수)로 판정. `WarrantJudge`(`SM.Meta` 순수), 서약 id는 overlay rail(`RewardSourceId` 동렬), outcome은 `DossierEntryRecord`에 영속. combat 0 변경. separability 실측 후 GPT Pro 전략 검수로 정치적 전환(→ ADR-0028) — Swift/Intact build축 tactical warrant는 미분리로 중단
- `adr-0028-political-warrant-loop.md`: ludonarrative 루프 P2 전환 — warrant=faction political mandate. `FactionState`(profile truth, trust per faction) + `WarrantResult`(issuer/opposed faction) + trust delta(`SM.Meta` 순수). judgment rail(ADR-0027) 재사용, combat 무관. slice 1(정치 상태 + warrant→trust), slice 2(trust→다음 전투 mutation)로 루프 닫음
- `adr-0029-deterministic-fixed-point-sim.md`: 결정론적 고정소수점 sim — float→fixed 마이그레이션(approach A). ingress(콘텐츠 float 저작→진입 양자화, 리플레이 raw fixed) + egress(read-model float, `SM.Unity` 무수정) 경계, 도메인 fixed 타입(`Fixed32`/`Score64`/`Hp64`, 범용 Wide 금지), 정수 틱 권위, `StatBlock` 결정적 순서, cross-platform golden hash(backend matrix). B(soft-float)·C(하이브리드) 기각. GPT Pro 검수(2026-06-07) 반영. 상세 계획 `docs/03_architecture/deterministic-sim-and-fixed-point-migration.md`
- `adr-0030-h100-headless-metrics-boundary.md`: H100 계측을 `SM.Core` + `SM.Combat` 전용 pure `SM.HeadlessMetrics` asmdef로 분리하고, real-content/session 조립은 `SM.Editor.Validation`에 남기는 경계
- `adr-0031-h100-headless-policy-boundary.md`: H100 정책을 `SM.Combat` 단일 참조 pure `SM.HeadlessPolicies`로 분리하고 player-visible projection은 `SM.Editor.Validation`이 소유하는 no-cheat 경계 (proposed, HUB 구조 승인 대기)

## 운영 메모

- ADR 번호는 중복 없이 증가한다.
- 신규 결정은 Pindoc Decision/Analysis가 기본이다. git ADR은 코드 직결 architecture 결정에만 사용한다.
