# H100 빌드공간 census 계약

- 상태: proposed
- 소유자: repository
- 최종수정일: 2026-07-16
- 소스오브트루스: `docs/03_architecture/h100-build-space-census-contract.md`
- 관련문서:
  - `docs/03_architecture/h100-headless-metrics-contract.md`
  - `docs/03_architecture/h100-headless-policy-contract.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/04_decisions/adr-0032-h100-build-space-census-boundary.md`

## 목적

이 문서는 12개 canonical archetype에서 4명을 고르는 편성공간과 여섯 anchor에 네 role slot을 놓는 배치공간을 전투 없이 전수 열거하는 계약을 고정한다. census는 빌드공간의 구조적 누락, 자동 발동, dead zone, 상위 독트린 희소성의 기준선이며 승률이나 H100 통과를 선언하지 않는다.

## 소유 경계

| 경계 | 책임 | 금지 |
| --- | --- | --- |
| `SM.Combat` | V1 시너지 breakpoint와 `TeamRuleSet` doctrine id, anchor·battlefield geometry, 실제 진형 predicate | census 파일 출력과 군집 결과 소유 |
| `SM.HeadlessCensus` | pure roster DTO, C(12,4)·P(6,4) 열거, 시너지·역할·진형 feature, deterministic medoid, 구조 assertion과 산출물 | authored content, session, persistence, Unity/editor API, 전투 실행 |
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
```

## deferred

- 2,027,520전투 full screening은 Unity batch가 아니라 content/session 입력을 pure snapshot으로 고정한 dotnet CLI 또는 scale runner에서 수행한다.
- 열위 편성 자동 제거와 pruning ledger는 census 구조지도 위의 후속 단계다.
- build·item·passive·formation optimizer와 적대적 meta 탐색은 census에 포함하지 않는다.
