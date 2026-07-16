# ADR-0032 H100 빌드공간 census 순수 경계

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-16
- 소스오브트루스: `docs/04_decisions/adr-0032-h100-build-space-census-boundary.md`
- 관련문서:
  - `docs/03_architecture/h100-build-space-census-contract.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/04_decisions/adr-0030-h100-headless-metrics-boundary.md`
  - `docs/04_decisions/adr-0031-h100-headless-policy-boundary.md`

## 문맥

H100 Stage 3은 12개 canonical archetype에서 4명을 고르는 495편성과 네 labelled role slot을 여섯 anchor에 놓는 360배치를 전수 지도화해야 한다. 이 작업은 전투 결과를 측정하는 계측도, 플레이어 행동을 선택하는 정책도 아니다. 또한 authored `ScriptableObject`와 session을 순수 열거 core로 끌어오면 Stage 1~2가 정한 editor boundary를 깨뜨린다.

## 결정

- 새 `SM.HeadlessCensus` asmdef가 pure build DTO, `BuildSpaceEnumerator`, 시너지·역할·진형 feature, deterministic medoid, 구조 assertion, census artifact writer를 소유한다.
- `SM.HeadlessCensus`는 `SM.Core`, `SM.Combat`만 참조하고 `noEngineReferences=true`를 사용한다. metrics, policies, content, meta, persistence, Unity, editor는 참조하지 않는다.
- doctrine id는 `SynergyService.BuildForTeam()`이 반환한 package에서 읽고 `TeamRuleSet` mapping을 복제하지 않는다. census는 sim/save truth와 `BattleHashCorpus` golden을 바꾸지 않는다.
- `SM.Editor.Validation.H100BuildSpaceContentAdapter`가 `RuntimeCombatContentLookup`의 canonical order와 combat snapshot을 pure input으로 투영한다.
- medoid는 360 labelled role-slot 배치를 다섯 정적 formation feature로 자동 군집화한다. 정규화 L1 distance, deterministic farthest-first 초기화, cluster medoid refinement, ordinal tie-break를 고정한다.
- 실제 진형 predicate를 정적 census에 복제하지 않는다. `H100BattleCorpusRunner.RunScreening()`이 선정된 medoid와 소수 build/seed를 기존 session, `BattleResolver`, `BattleMetricProjector` 경로로 실행해 pipeline만 증명한다.
- 2,027,520 full screening, pruning, optimizer는 Stage 3 census core의 후속으로 남긴다.

## 검토한 대안

| 대안 | 판정 | 이유 |
| --- | --- | --- |
| `SM.HeadlessMetrics`에 census 추가 | 기각 | 전투·캠페인 outcome record/gate와 입력공간 열거·군집의 truth 축이 다르다. |
| `SM.HeadlessPolicies`에 census 추가 | 기각 | 행동 선택/no-cheat contract와 offline 공간지도가 섞이며 metrics 참조 금지 sibling 경계가 흐려진다. |
| `SM.Editor.Validation`에 전부 구현 | 기각 | 178,200 pure state와 medoid/구조 assertion을 dotnet scale runner에서 재사용할 수 없다. |
| 별도 pure `SM.HeadlessCensus` + Editor adapter | 채택 | 기존 authored/session 경계를 보존하면서 열거·분류·군집을 Unity 없이 재사용할 수 있다. |

## 결과와 영향

- `SM.Editor`와 `SM.Tests.FastUnit`이 `SM.HeadlessCensus`를 소비하고 역방향 참조는 없다.
- `BuildBoundaryGuardFastTests`가 no-engine, exact references(`SM.Core`, `SM.Combat`), 상위 assembly 금지를 고정한다.
- `build-space.csv`, `formation-space.csv`, `formation-medoids.csv`, `census-report.json`은 invariant/ordinal/UTF-8 no-BOM으로 결정적으로 생성된다.
- 정적 formation feature는 실제 flank, interpose, save, dive의 인과성을 주장하지 않는다. screening record도 작은 pipeline witness이며 밸런스 인증 표본이 아니다.
- full screening을 순수 CLI로 옮길 때 `SM.HeadlessCensus`의 roster/formation manifest와 `SM.HeadlessMetrics`의 outcome record를 sibling consumer에서 조립할 수 있다.

## 승인 조건

HUB 구조 리뷰에서 별도 sibling asmdef, `SM.Core`+`SM.Combat` exact dependency, role-labelled medoid feature, Editor adapter와 screening ownership을 승인한다. 승인 뒤 상태를 `active`로 바꾸고 `test-batch-fast`, `test-harness-lint`, `tools/h100-build-space.ps1` 결과를 기록한다.

**HUB 승인 (2026-07-16)**: 3번째 sibling pure asmdef(SM.HeadlessCensus, Core+Combat, no-engine) 승인 — Metrics/Policies/Census 삼분 경계. 경계 가드가 census no-engine + exact(Core,Combat) + no-reference(sibling Metrics/Policies 포함) 강제 확인. census가 Pro Q2 조합값 495/360/178,200/36/3/96/81을 자동 검증(BuildSpaceCensusFastTests). medoid 8개 결정적·360 전배치 커버. screening smoke 48레코드 결정적. 정적 census feature=eligibility(인과 주장 아님, Stage 4 담당) 명시 확인. HUB 독립 재검증 test-batch-fast 976/972/0. 골든 무변경. 상태 active.
