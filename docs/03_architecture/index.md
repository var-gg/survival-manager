# 03 아키텍처

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-21
- 소스오브트루스: `docs/03_architecture/index.md`
- 관련문서:
  - `docs/index.md`
  - `docs/04_decisions/index.md`
  - `docs/04_decisions/adr-0006-combat-sim-boundary.md`
  - `docs/04_decisions/adr-0014-grid-deployment-continuous-combat.md`

## 목적

이 폴더는 기술 구조, 코딩 경계, 의존 방향, Unity 특화 제약을 정의한다.

## 코드 구조 거버넌스 문서

- `coding-principles.md`: 파일 책임, 분리 신호, interface/abstract class 도입 기준
- `dependency-direction.md`: asmdef/context/layer 의존 허용·금지 규칙
- `unity-boundaries.md`: `MonoBehaviour`, `ScriptableObject`, scene 책임 경계
- `assembly-boundaries-and-persistence-ownership.md`: `SM.Meta` content adapter, persistence ownership, asmdef 사전 점검 규칙
- `validation-and-acceptance-oracles.md`: feature closure, acceptance matrix, evidence 기록 기준
- `testing-strategy.md`: 저비용 검증 표면 추가 순서만 다루는 보조 draft

## 코드 구조 작업 시작 컨텍스트

- 구조 변경 작업은 `AGENTS.md` -> `docs/index.md` -> `docs/03_architecture/index.md` -> `coding-principles.md` -> `dependency-direction.md` -> `docs/00_governance/implementation-review-checklist.md` 순서로 시작한다.
- `MonoBehaviour`, `ScriptableObject`, scene 책임이 걸리면 `unity-boundaries.md`를 추가로 연다.
- asmdef/persistence ownership까지 걸리면 `assembly-boundaries-and-persistence-ownership.md`를 추가로 연다.

## Editor-free closure scope

| 범위 | 현재 판정 | 라우팅 |
| --- | --- | --- |
| `SM.Core`, `SM.Combat`, `SM.Meta`, `SM.Meta.Serialization`, `SM.Persistence.Abstractions` | pure boundary로 닫힘 | `test-batch-fast`, exact asmdef allowlist, boundary guard |
| `FastUnit` test lane | `SM.Tests.FastUnit` 전용 asmdef에서 editor-free/resource-free/authored-object-free로 닫힘 | fake lookup, pure fixture, class-level category, dedicated folder/alias-wrapper guard |
| `SM.Unity`, `GameSessionState`, runtime bootstrap/content lookup | boundary adapter로 유지 | `GameSessionRuntimeBootstrapProvider` production choke point, FastUnit 밖, 필요 시 focused session 또는 BatchOnly |
| authored content, `ScriptableObject`, `Resources.Load*`, content conversion | pure closure 밖 | content validation 또는 BatchOnly; `ContentConversion`은 내부 converter guard로 관리 |
| UI/controller/scene/prefab, PlayMode | runtime/editor integration lane | focused EditMode, PlayMode smoke, manual/editor-required |
| `ManualLoopD` | 장시간 balance/telemetry lane | default fast closure 증거로 쓰지 않음; symmetric mirror 4v4 policy 추적 포함 |

아키텍처 문서에서 “editor-free closure”라고 쓸 때는 첫 두 행의 범위를 뜻한다. `SM.Unity`와 authored content/UI loop가 repo-wide pure boundary 안으로 들어왔다는 뜻으로 쓰지 않는다.

## 변경 라우팅 Quick Reference

| 변경 유형 | 소유 경계 | 첫 테스트/검증 | 에디터 의존 경계 |
| --- | --- | --- | --- |
| 전투 규칙, damage, targeting, movement, status | `SM.Combat` | `test-batch-fast`, combat focused tests | editor-free |
| 공통 id/stat/result/content schema enum | `SM.Core`, `SM.Core.Content` | `test-batch-fast` | editor-free |
| reward, passive, loot, expedition progression rule | `SM.Meta` pure model/service | `test-batch-fast`, `MetaRewardPickTests` | editor-free unless session/UI application is in scope |
| story/dialogue/runtime narrative decision | `SM.Meta` story/spec model | `test-batch-fast`, `StoryDirectorServiceTests` | editor-free when authored definition is not touched |
| authored `ScriptableObject` definition/schema | `SM.Content` | `content-validate`, BatchOnly focused tests | editor-required/content lane |
| authored definition to runtime snapshot/spec conversion | `SM.Unity.ContentConversion`, runtime bootstrap | BatchOnly focused tests, `test-harness-lint` | editor-light; converter는 내부 `SM.Unity` 경계, `Resources.Load*` stays at choke point |
| session orchestration and UI-facing facade | `SM.Unity.Session`, `GameSessionState` public facade | `test-batch-fast`, `GameSessionStateTests` when production path is touched | editor-light; public facade migration is separate task |
| UI controller, presenter, view binding | `SM.Unity` presentation boundary | focused EditMode, PlayMode smoke when scene/prefab behavior matters | editor-light to editor-required |
| save contract and serialization | `SM.Persistence.Abstractions`, `SM.Persistence.Json` | persistence focused tests, `test-batch-fast` | editor-free unless repository adapter integration is in scope |
| docs, validation harness, guard scripts | `docs/**`, `tasks/**`, `tools/**`, `Assets/Tests/**` | docs policy/check/smoke, `test-harness-lint`, focused guard | editor-free unless Unity batch guard is being verified |

라우팅 표는 첫 도착지를 고르는 기준이다. 변경이 두 경계를 동시에 건드리면 더 낮은 pure rule부터 닫고, session/content/UI 연결은 별도 단계로 올린다.

## Unity agent harness 문서

- `unity-agent-harness-contract.md`: Unity repo에서 task를 어떻게 shape하고 닫는지에 대한 상위 운영 계약
- `unity-editor-iteration-and-asset-authoring.md`: code-only / asset batch / validator / smoke 순서와 Editor state 규칙
- `assembly-boundaries-and-persistence-ownership.md`: `SM.Meta`, `SM.Unity`, `SM.Persistence.Abstractions` 경계와 authored content adapter 규칙
- `validation-and-acceptance-oracles.md`: feature closure, acceptance matrix, evidence 기록 기준
- `unity-mcp-tooling-contract.md`: low-level loop를 high-level capability contract로 대체하는 기준

## 핵심 구조 문서

- `technical-overview.md`: 현재 구현과 문서 체계를 연결하는 상위 개요
- `bounded-contexts.md`: `SM.*` context 책임 분리 기준
- `coding-principles.md`: AI와 사람이 공유하는 코딩 원칙
- `dependency-direction.md`: asmdef/context/layer 의존 허용·금지 규칙
- `unity-boundaries.md`: `MonoBehaviour`, `ScriptableObject`, scene 책임 규칙
- `unity-project-layout.md`: 실제 폴더와 asmdef 배치
- `unity-scene-flow.md`: Boot 중심 scene flow 규칙
- `ui-runtime-architecture.md`: `RuntimePanelHost`, UITK asset-first packaging, battle shell 경계

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
- `town-character-sheet-contract.md`: Town readonly character sheet source/view-state contract
- `character-axis-and-localized-battle-metadata.md`: `CharacterDefinition`, localized inspector, battle metadata 흐름
- `loadout-compiler-and-battle-snapshot.md`: build -> compile -> battle snapshot 경계
- `sim-sweep-and-balance-kpis.md`: deterministic sweep, KPI, artifact, review/fail 규칙
- `replay-persistence-and-run-audit.md`: active run / replay / ledger persistence 기준
- `battle-actor-wrapper-and-asset-intake-seam.md`: battle wrapper prefab, socket surface, vendor intake seam

## 내러티브 아키텍처 문서

- `narrative-code-architecture.md`: story/dialogue/event definition, runtime state, save model, `StorySceneFlowBridge` / `StoryPresentationRunner` playback adapter

## 기타 운영 문서

- `arena-snapshot-matchmaking-and-season-contract.md`: async arena snapshot, local matchmaking, season contract
- `persistence-strategy.md`: persistence adapter 경계
- `session-realm-authority-and-offline-online-ports.md`: session realm, authority seam, offline-first port 구조
- `testing-strategy.md`: 저비용 검증 표면 추가 순서만 다루는 보조 draft
- `asset-intake-boundary.md`: `Assets/ThirdParty/**` intake boundary
- `battle-actor-wrapper-and-asset-intake-seam.md`: battle actor wrapper seam과 sandbox/validator/bootstrapping 규칙

## 운영 메모

구조 정책을 바꾸면 관련 기준 문서와 `docs/04_decisions/` ADR을 같이 갱신한다.
