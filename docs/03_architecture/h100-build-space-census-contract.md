# H100 빌드공간 census 계약

- 상태: proposed
- 소유자: repository
- 최종수정일: 2026-07-17
- 소스오브트루스: `docs/03_architecture/h100-build-space-census-contract.md`
- 관련문서:
  - `docs/03_architecture/h100-headless-metrics-contract.md`
  - `docs/03_architecture/h100-headless-policy-contract.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/04_decisions/adr-0032-h100-build-space-census-boundary.md`

## 목적

이 문서는 12개 canonical archetype에서 4명을 고르는 편성공간과 여섯 battlefield anchor에 네 role slot을 놓는 배치공간을 전투 없이 전수 열거하는 계약을 고정한다. 또한 같은 pure assembly에 두는 evaluator-only build grammar truth graph와 BT1 컨셉 카탈로그 파생 경계를 고정한다. census와 컨셉 카탈로그는 구조적 기준선을 제공하며 승률, player-visible audit, H100 통과를 선언하지 않는다.

## 소유 경계

| 경계 | 책임 | 금지 |
| --- | --- | --- |
| `SM.Combat` | V1 시너지 breakpoint와 `TeamRuleSet` doctrine id, anchor·battlefield geometry, 실제 진형 predicate | census 파일 출력과 군집 결과 소유 |
| `SM.HeadlessCensus` | pure roster DTO, C(12,4)·P(6,4) 열거, build grammar truth graph, 시너지·역할·진형 feature, deterministic medoid, BT1 컨셉 카탈로그 파생, 구조 assertion과 산출물 | authored content, player-visible audit, session, persistence, Unity/editor API, 전투 실행 |
| `SM.Editor.Validation` | `RuntimeCombatContentLookup` canonical roster를 pure DTO로 투영하고 medoid screening을 실제 session/sim 경로로 실행 | census 구조값이나 시너지 rule id를 별도로 복제 |
| `SM.HeadlessMetrics` | screening 전투 결과의 `BattleMetricRecord` projection과 replay hash | census 열거·군집·정적 구조 판정 |

`SM.HeadlessCensus`는 `SM.Core`, `SM.Combat`만 참조하고 `noEngineReferences=true`를 유지한다. `SM.HeadlessMetrics`, `SM.HeadlessPolicies`, `SM.Content`, `SM.Meta`, `SM.Persistence.*`, `SM.Unity`, `SM.Editor` 참조는 금지한다.

## canonical 입력과 열거

`H100BuildSpaceContentAdapter`는 `RuntimeCombatContentLookup.GetCanonicalArchetypeIds()`의 순서를 사용하고 `CombatContentSnapshot.Archetypes`에서 `archetype/race/class/default anchor`를 pure `BuildArchetype`으로 투영한다. 네 역할은 V1 3×4 격자의 class 축과 일치한다.

| class | census role |
| --- | --- |
| `vanguard` | `Tank` |
| `duelist` | `Damage` |
| `ranger` | `Ranged` |
| `mystic` | `Healer` |

`BuildSpaceEnumerator`는 canonical 입력 순서를 보존해 4-combination을 열거하고, formation은 `Tank`, `Damage`, `Ranged`, `Healer` 순서의 labelled role slot을 여섯 anchor에 배정한다. 따라서 편성은 495개, 편성당 배치는 360개, Cartesian state 수는 178,200개다. 중복 역할 편성에 medoid를 적용할 때는 role 순서 뒤 canonical roster 순서를 tie-break로 사용한다.

시너지 tier signature는 race count의 `@2/@4`, class count의 `@2/@3`을 ordinal id로 기록한다. doctrine id는 census가 switch를 복제하지 않고 최소 `BattleUnitLoadout`을 `SynergyService.BuildForTeam()`에 전달해 `CombatModifierPackage.GrantedTeamRuleId`에서 읽는다. 따라서 `TeamRuleSet`의 `rule.phalanx`, `rule.bloodrush`, `rule.deathtoll`, `rule.bulwark`, `rule.execute`, `rule.killzone`, `rule.resonance`가 전투 truth와 같은 경로에서 나온다.

## Build grammar truth graph 보조 경계

`BuildGrammarTruthGraph`는 편성 495×배치 360 열거 결과와 별개인 순수 evaluator 구조다. authored snapshot을 읽는 일은 `SM.Editor.Validation` adapter가 맡고, `SM.HeadlessCensus`에는 `SM.Core`·`SM.Combat` DTO만 들어온다. graph builder는 authored recruit, reward, refit, passive, synergy 후보에서 직접 확인되는 `produces`·`amplifies`·`requires`·`pays_off`·`conflicts`·`substitutes`·`acquired_by` 관계만 만든다.

이 graph는 player-visible fact나 audit 결과를 알지 못한다. `SM.HeadlessMetrics`도 graph를 직접 참조하지 않으며 Editor adapter가 sibling DTO를 매핑한다. 이 분리는 정책 assembly로 evaluator truth가 새는 것을 막고 `BuildBoundaryGuardFastTests`의 exact asmdef allowlist를 유지한다. 세부 BT3 비교·artifact 계약은 `h100-headless-metrics-contract.md`가 소유한다.

## BT1 컨셉 카탈로그 파생 계약

`OwnerConceptAnchorCatalog`는 owner가 제시한 열 가지 판타지 anchor의 id·이름·짧은 의도만 소유한다. 모든 anchor는 `ratification_pending=true`인 draft이며 recipe, 현재 콘텐츠 id, motif 매핑을 정의 파일에 섞지 않는다. 실제 파생 결과는 별도 `OwnerConceptDerivation`에 기록하여 owner 의도와 evaluator 계산을 분리한다.

각 파생 컨셉의 `ConceptContract`는 다음 여덟 필드만 계약 표면으로 가진다.

- `identity_predicates`
- `progress_milestones`
- `payoff_witness`
- `allowed_substitutions`
- `flex_slots`
- `counter_affordances`
- `availability_tier` (`core` 또는 `aspirational`)
- `pivot_conditions`

`ConceptMotifEnumerator`는 authored snapshot을 직접 읽지 않는다. Editor adapter가 투영한 build grammar truth graph, 495개 편성, 360개 labelled formation을 입력으로 받아 다음 두 motif 계열을 만든다.

- threshold → doctrine → tactical payoff
- enabler → amplifier → payoff

formation은 전체 360개를 결정적으로 profile로 묶되 실제 census formation signature를 대표값으로 사용한다. 후보는 구체 content id를 제외한 `ConceptFingerprint`로 동형성을 판정하고, weighted token distance와 ordinal tie-break로 cluster medoid를 고른다. stable id는 정렬된 계약 입력의 SHA-256에서 파생하며 wall clock, GUID, 현재 culture 정렬을 사용하지 않는다.

raw-stat-only 증폭은 독립 컨셉으로 인정하지 않는다. 비수치 payoff나 전술 witness로 이어지는 graph route가 있을 때만 motif에 들어간다. `payoff_witness`는 E01 player-visible feedback vocabulary를 Editor adapter가 명시적으로 주입한 값만 허용하며, deriver가 새로운 witness 이름을 만들거나 정책 observation을 참조하지 않는다. `SM.HeadlessPolicies`에는 `ConceptCatalog`와 `ConceptContract`가 접근 불가능해야 하며 이 제약은 FastUnit boundary guard가 고정한다.

owner anchor마다 실제 motif를 연결한 recipe가 하나 이상 있거나, 구조화된 derivation gap 하나가 있어야 한다. 현재 tier seed가 owner 정의에 없으므로 `availability_tier`는 reachable threshold와 acquisition route로부터 시스템이 파생하며 owner ratification 전까지 확정된 기획 truth로 취급하지 않는다. owner anchor에 선택되지 않은 cluster medoid는 `system_derived_medoids`로 별도 보존한다.

BT1 산출물은 `Logs/h100-concept-catalog/concept_catalog_bt1.json`이다. artifact에는 owner anchor 정의와 derivation, variants, system-derived 미배정 medoid, tier 분포, raw-stat-only 제외 수, 동형 중복 제거 수를 분리해 기록한다. 탐색 중 숨겨지는 정보나 Coverage QA 관측 주입은 이 계약 범위가 아니며 후속 BT1-E04가 소유한다.

FastUnit은 pure fixture로 byte-identical determinism, raw-stat-only 제외, 동형 dedupe, witness whitelist, 정책 접근 차단을 검증한다. 실제 authored content smoke는 Editor runner가 canonical snapshot을 투영한 뒤 같은 pure deriver와 validator를 호출한다. 이 분리는 FastUnit의 resource-free/authored-object-free 계약을 유지한다.

## E05 intent track 탐색 경계

`IntentTrackEvaluator`는 E03 계약을 실제 campaign offer stream에 대조하는 evaluator-only 순수 검색 책임이다. `IntentTrackState`, `IntentTrackAgencyWindow`, `IntentTrackChoice`는 authored object, session, content lookup을 포함하지 않는 DTO이며 roster, inventory, skill/passive, passive budget, Refit 자원, 배치, formation, milestone 상태만 운반한다. Editor adapter가 실제 배치와 보상 선택지를 delta DTO로 낮추고 campaign 종료 뒤 한 번에 넘긴다.

탐색은 `identity_predicates`를 모두 만족하는 합법 경로의 존재와 가장 이른 진척/실현 시점을 구한다. contract-relevant component/effect만 state signature에 남기고 동일 signature의 열등 경로를 제거해 확정 offer열의 유한 분기를 줄인다. 이는 정밀 최적화 인증기가 아니라 BT6/BT7 지표 공급기다. 정책 assembly가 이 타입을 참조하거나 미래 offer를 observation으로 받는 변경은 금지하며 `BuildBoundaryGuardFastTests`와 E05 witness가 이를 고정한다.

## 진형 feature와 medoid

formation feature는 실제 전투 결과가 아니라 정적 eligibility proxy다.

- `frontline_count`: 점유된 front anchor 수
- `protected_slot_count`: 같은 lane의 front anchor가 점유된 back anchor 수
- `flank_rear_exposure_score`: 비어 있는 같은 row 인접 lane과 직접 front blocker가 없는 back slot을 role 가중치로 합산한 값
- `support_distance`: `Healer` slot에서 다른 세 role slot까지의 평균 battlefield 거리
- `backline_accessibility`: `Ranged`와 `Healer` slot의 direct/adjacent front 진입 가능성 평균

`BattleFormationConsequence`의 screen/interpose/flank, save moment, backline dive 같은 실제 predicate는 동적 `BattleState`가 필요하므로 census에서 재구현하지 않는다. 정적 feature는 후보를 나누는 데만 쓰고 실제 발동은 screening 또는 후속 paired rollout에서 `BattleMetricRecord`의 진형 전과 채널로 확인한다.

8개 medoid는 다섯 feature를 각 축의 min/max로 정규화한 뒤 L1 distance를 사용한다. 첫 점은 전체 distance 합이 가장 작은 실제 배치, 후속 초기점은 nearest-medoid distance가 가장 큰 실제 배치로 고른다. cluster 내부 distance 합이 가장 작은 실제 배치로 최대 32회 refinement하며 모든 tie는 placement signature ordinal 순서로 끊는다. 수동 favorite 목록은 없다.

Stage 4 placement leverage runner는 이 8개 medoid를 그대로 재사용한다. 같은 편성·적·seed에서 기본 배치와 medoid 배치를 실제 sim으로 실행해 최적 배치 승률에서 기본 배치 승률을 뺀다. `SM.HeadlessMetrics`가 medoid를 재선정하거나 360 배치 feature를 복제하지 않으며, `SM.Editor.Validation`만 `SM.HeadlessCensus` 결과와 실제 session을 조합한다.

## 산출물과 구조 assertion

기본 출력은 `Logs/h100-build-space/`이며 다음 파일을 생성한다.

- `build-space.csv`: 495편성의 archetype/race/class/synergy/doctrine/role 구조 행
- `formation-space.csv`: 360 labelled role-slot 배치와 진형 feature
- `formation-medoids.csv`: 자동 선정된 8개 medoid, cluster 크기와 distance
- `census-report.json`: 구조 요약과 automatic activation/dead zone/rarity flag
- `screening-smoke.jsonl`: medoid pipeline을 통과한 소규모 실제 전투 record
- `screening-smoke-summary.json`: build/medoid/seed 수, failure/crash/timeout, replay manifest hash

`CanonicalBuildSpaceContract`와 `BuildSpaceCensusFastTests`는 최소 아래 값을 fail closed한다.

| 구조값 | 기대값 |
| --- | ---: |
| 전체 편성 | 495 |
| 편성당 labelled 배치 | 360 |
| 전체 state | 178,200 |
| `race@2` 이상 편성 | 495 |
| `class@2` 이상 편성 | 414 |
| `class@3` 편성 | 36 |
| `race@4` 편성 | 3 |
| 정확히 같은 race 3명 | 96 |
| race 2+2 | 108 |
| class 2+2 | 54 |
| 역할 1/1/1/1 | 81 |

## 실행과 검증

기본 명령은 구조 census 뒤 8 medoid × 3개 자동 build stratum × 2 seed, 총 48전투를 실행한다. build stratum은 `race@4`, `class@3`, 상위 독트린이 없는 역할완비 편성을 먼저 고르고 부족한 수는 build index 간격으로 결정적으로 채운다.

```powershell
pwsh -File tools/h100-build-space.ps1
```

BT1 컨셉 카탈로그를 실제 canonical content에서 다시 파생하고 검증한다.

```powershell
pwsh -File tools/h100-concept-catalog.ps1
```

Stage 4에서 같은 medoid를 placement leverage에 재사용하는 integration은 별도 명령으로 확인한다.

```powershell
pwsh -File tools/h100-formation.ps1 -SeedCount 5
```

빠른 pipeline 확인은 build와 seed 수를 줄일 수 있다.

```powershell
pwsh -File tools/h100-build-space.ps1 -ScreeningBuildCount 1 -ScreeningSeedCount 1
```

코드 변경의 기본 검증은 다음 순서다.

```powershell
pwsh -File tools/unity-bridge.ps1 test-batch-fast
pwsh -File tools/test-harness-lint.ps1 -RepoRoot .
pwsh -File tools/h100-build-space.ps1
pwsh -File tools/h100-concept-catalog.ps1
```

## deferred

- 2,027,520전투 full screening은 Unity batch가 아니라 content/session 입력을 pure snapshot으로 고정한 dotnet CLI 또는 scale runner에서 수행한다.
- 열위 편성 자동 제거와 pruning ledger는 census 구조지도 위의 후속 단계다.
- build·item·passive·formation optimizer와 적대적 meta 탐색은 census에 포함하지 않는다.
