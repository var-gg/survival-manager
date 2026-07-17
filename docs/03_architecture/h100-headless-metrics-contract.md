# H100 헤드리스 계측 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-17
- 소스오브트루스: `docs/03_architecture/h100-headless-metrics-contract.md`
- 관련문서:
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`
  - `docs/03_architecture/sim-sweep-and-balance-kpis.md`
  - `docs/03_architecture/telemetry-contract.md`
  - `docs/04_decisions/adr-0030-h100-headless-metrics-boundary.md`
  - `docs/03_architecture/h100-headless-policy-contract.md`
  - `docs/04_decisions/adr-0031-h100-headless-policy-boundary.md`
  - `docs/03_architecture/h100-build-space-census-contract.md`
  - `docs/04_decisions/adr-0032-h100-build-space-census-boundary.md`

## 목적

이 문서는 H100 헤드리스 release-candidate 판정의 원시 레코드, 재현 해시, 결정적 산출물, 게이트 평가 경계를 고정한다. H100은 열 개 게이트를 모두 통과해야 하는 AND-gate이며, 이 계측 단계는 현재 게임이 게이트를 통과했다고 선언하는 단계가 아니다.

## 소유 경계

| 경계 | 책임 | 금지 |
| --- | --- | --- |
| `SM.Combat` | 전투 상태, 결과, 활동 telemetry, canonical state hash의 authoritative truth | H100 보고서나 파일 출력 소유 |
| `SM.HeadlessMetrics` | 전투·캠페인 레코드, 정보 표면 audit DTO·판정, 순수 projection, replay hash 조합, 결정적 JSONL/CSV, 게이트 평가 | `SM.Unity`, authored content, session, persistence, editor API와 sibling `SM.HeadlessCensus` 참조 |
| `SM.HeadlessPolicies` | player-visible observation/decision과 6개 production + 1개 QA deterministic 정책 | session/content/persistence/editor 참조, future RNG·미공개 node·resolved enemy stat 입력 |
| `SM.Editor.Validation` | 실제 `RuntimeCombatContentLookup`과 `GameSessionState`를 조립해 전투·캠페인을 실행 | 계측 스키마나 판정 규칙을 별도로 복제 |

`SM.HeadlessMetrics` asmdef는 `SM.Core`, `SM.Combat`만 참조하고 `noEngineReferences=true`를 유지한다. `SM.Content`, `SM.Meta`, `SM.Persistence.*`, `SM.Unity`, `SM.Editor` 참조는 금지한다. 실제 콘텐츠와 캠페인 세션은 Unity 경계이므로 `SM.Editor.Validation` runner가 실행하고, 순수 계측 레코드로 투영한 뒤에만 `SM.HeadlessMetrics`로 넘긴다.

## 원시 레코드 계약

`BattleMetricRecord`는 아래 원시 관측값을 보존한다.

- run, campaign, battle, replay group, scenario, policy 식별자와 seed
- ally/enemy composition·formation 식별자와 package/effect source id별 build component 수
- step 수, duration, winner side, timeout, stomp, first-death side
- 생존 HP 합, 최종 HP 차이, 선택적 정규화 전력차
- 측면, 후방, 차단, 구출, 후열 격파의 진형 전과 5채널
- synergy, combo, augment, doctrine rule id별 발현 수
- eligible, fired, causal rule id와 salient event 수
- crash, softlock, 비유한 수, 불법 음수, 비종결 플래그
- canonical state hash, activity replay hash, 결합 replay hash

`CampaignMetricRecord`는 캠페인 종료·truncation 상태, 전투·승패·timeout·stomp 수, 정책과 결정 관측 가능 여부, macro/build family 분포, 무결성 카운터, replay manifest hash를 보존한다. 설정된 site safety에 도달한 축소 실행은 softlock으로 오인하지 않고 `Truncated=true`로 기록하며 end-to-end campaign 표본에서 제외한다. paired counterfactual이나 blind holdout처럼 현재 runner가 측정하지 않는 필드는 임의 값으로 통과시키지 않고 `DecisionMetricsAvailable=false` 또는 관측치 부재로 남긴다.

새 지표를 추가할 때 authoritative sim/save truth를 바꾸지 않는다. 레코드는 기존 `BattleState`, `BattleResult`, `BattleActivityTelemetrySnapshot`, `CombatBeat`를 읽는 additive projection이어야 한다. `BattleHashCorpus` golden을 계측 편의를 위해 다시 기록하지 않는다.

## Player-visible fact ledger와 BT2 provenance

`PlayerVisibleFactRecord`는 정책 결정 전에 관측된 UI 의미를 `fact_id`, `observed_at`, `ui_source`, `subject`, `verb`, `target`, `condition`, `stack_or_threshold`, `acquisition_hint`, `source_text`, `content_hash`로 기록한다. `observed_at`은 wallclock이 아니라 `campaign_index`, `site_index`, `decision_index`로 구성한다. `fact_id`와 `content_hash`는 run id나 관측 시각을 제외한 의미 필드의 길이-prefix UTF-8 SHA-256에서 파생하므로 같은 observation 내용은 어느 재실행에서도 같은 fact id 집합을 만든다.

허용 `ui_source`는 실제 또는 예정된 player-facing 의미를 고정한다.

- `run_seed_display`, `campaign_map`, `squad_builder_formation`, `town_roster`
- `roster_sheet_skill`, `roster_sheet_item`, `roster_sheet_passive`
- `town_hud_wallet`, `run_augment_panel`
- `squad_builder_synergy`, `compendium_synergy`
- `encounter_preview`, `reward_card`

`H100PlayerVisibleFactProjector`는 `HeadlessPolicyObservation`의 roster 기본 상태, skill/status mechanics, flex skill, item/affix/granted skill mechanics, passive node, wallet, temporary augment, synergy count/catalog, 현재 enemy preview, reward option/mechanics를 빠짐없이 fact로 투영한다. 이 adapter는 `SM.Editor.Validation`에 있고 `SM.HeadlessMetrics`와 `SM.HeadlessPolicies`를 함께 보는 유일한 조립층이다. fact record·hash·audit·writer는 `SM.HeadlessMetrics`에 남고 두 pure sibling asmdef 사이의 참조는 추가하지 않는다.

`HeadlessDeploymentDecision`과 `HeadlessRewardDecision`은 additive `EvidenceFactIds`를 가진다. 정책은 observation에 조립된 문자열 fact-id index에서 실제 사용한 신호만 선택한다. `PlayerVisibleDecisionRecord`는 action, rationale, finite estimated value, `EvidenceRef` 목록을 fact와 같은 timeline에 기록한다. 빈 evidence, 존재하지 않는 fact, 결정 이후에만 존재하는 fact는 즉시 `PlayerVisibleProvenanceException`으로 실패한다. 정책 자체의 빈·중복 fact id도 `HeadlessPolicyGuard`가 fail closed한다.

`PlayerVisibleFactLedgerAuditor`가 BT2의 네 지표를 공급한다.

- `post_decision_information_reference_count`: `observed_at <= decided_at` join을 만족하지 못한 참조 수
- `non_ui_semantic_internal_field_reference_count`: 허용 UI source 밖의 의미 또는 내부 전용 vocabulary 수
- `oracle_or_truth_leak_count`: fact/decision의 evaluator-only 참조 수
- `unsupported_certain_claim_count`: prior evidence로 해석되지 않는 certain decision 수

캠페인 runner는 `player_visible_fact_ledger.jsonl`을 UTF-8 no-BOM과 stable timeline/fact id 순서로 기록한다. BT2는 E01 공급이 완료되어 `evaluable_now=true`이며 네 지표가 모두 관측된 0일 때만 PASS한다. BT3도 아래 E02 공급이 완료되어 실측 평가하며, 그 밖의 BT 게이트의 `not_yet_evaluable` 상태와 H100-RC1 산출물은 그대로 보존한다.

## Build grammar truth graph와 BT3 정보 표면 audit

`BuildGrammarTruthGraph`는 `SM.HeadlessCensus`가 소유하는 evaluator-only 순수 구조다. `SM.Editor.Validation`의 `H100BuildGrammarTruthProjector`가 실제 `CombatContentSnapshot`을 `BuildGrammarTruthSource`로 낮추고, 순수 builder는 `produces`, `amplifies`, `requires`, `pays_off`, `conflicts`, `substitutes`, `acquired_by` 관계만 ordinal 순서로 파생한다. edge id는 정렬된 관계열의 index에서 만들어지며 wallclock과 GUID를 쓰지 않는다.

actionable 판정은 현재 선택이나 정상 플레이의 선택 표면에 직접 연결되는 authored 후보로 제한한다.

| subject | authoritative 입력 | 획득·선택 표면 |
| --- | --- | --- |
| recruit archetype·candidate skill | `CombatArchetypeTemplate`의 recruitability, active/passive pool, skill spec | recruit |
| reward item·granted skill | item package/catalog와 granted-skill catalog | reward card |
| equipment affix | affix required/excluded tag, modifier/rule package | refit |
| augment | augment family, exclusion tag, modifier/rule/trigger package | reward card |
| passive node | prerequisite node, exclusion group, package, granted skill | level node |
| synergy tier | `TeamSynergyTierRule`과 `SynergyService.BuildForTeam`이 낸 canonical team rule | squad composition |

같은 slot·role·family처럼 선택 대체성이 명시된 경우만 `substitutes`를 만든다. passive board 전체, 계산 중간값, derived combat state, 승률·oracle 값은 edge로 만들지 않는다. 특히 synergy payoff는 별도 switch로 복제하지 않고 기존 `SynergyService` 결과를 사용한다.

player-visible 쪽은 별도 UI 설명을 발명하지 않는다. `H100BuildGrammarCatalogObservationBuilder`가 실콘텐츠 catalog를 E01 observation으로 조립하고, `H100PlayerVisibleFactProjector`가 실제로 생성한 fact만 `H100BuildGrammarVisibleSurfaceProjector`가 audit DTO로 매핑한다. reward card·synergy compendium처럼 선택 전에 도달하는 fact만 `available_before_choice=true`이며 roster의 현재 장착·현재 passive처럼 선택 이후 상태인 fact는 pre-choice 증거로 세지 않는다. v1 discoverability는 E01 fact projection으로 정상 플레이 표면에 도달 가능한지를 근사하며 catalog별 unlock timing은 후속이다.

`InformationSurfaceAuditor`와 결과는 `SM.HeadlessMetrics`에 있고 `SM.HeadlessCensus`를 참조하지 않는다. 두 sibling DTO의 매핑은 둘을 이미 참조하는 `SM.Editor.Validation`에서만 수행한다. BT3 hard metric은 다음 네 가지다.

- `actionable_offer_missing_semantics`: actionable subject별로 pre-choice 의미가 하나 이상 빠진 offer 수
- `undefined_visible_token`: 가시 status/tag/team-rule id 중 도달 가능한 정의가 없는 token 수
- `hidden_prerequisite`: `requires` edge가 선택 전 표면에 없는 수
- `description_behavior_mismatch_count`: 같은 관계의 visible 구조값과 runtime truth 구조값이 다른 수

`interaction_feedback_coverage`는 hard threshold와 분리한 보조 지표이며, feedback이 필요한 edge 중 기존 combat telemetry·beat witness가 연결된 비율이다. 목표는 `>= 0.90`이다. gap은 자동 수정하지 않고 `kind`, `subject_id`, `missing`, `owner_content_candidate` 네 필드만 기록한다. 실콘텐츠 FAIL은 runner 실패가 아니라 후속 content/UI owner 입력이다.

독립 E02 실행은 `Logs/h100-surface-audit/information_surface_audit.json`을 만들고, 통합 H100 실행은 같은 파일을 `Logs/h100-metrics/`에 함께 기록한다. artifact는 invariant snake_case, ordinal 정렬, UTF-8 no-BOM을 사용하며 wallclock과 GUID를 포함하지 않는다.

## 진형 Stage 4 계측

`FormationEligibilityTracker`는 재실행 중 `BattleFormationConsequence`와 실제 HP/의도 상태를 읽어 측면·후방·차단·구출·후열 격파의 eligibility를 누적한다. fired는 `BattleMetricRecord`의 typed counter에서 읽으며, 측면 수는 후방을 포함한 `FlankStrikeCount`에서 `RearStrikeCount`를 빼서 중복을 제거한다. legible은 채널별 typed 설명이 존재할 때만 true다.

`FormationCausalEvaluator` v1은 같은 seed·편성·적의 기본 배치와 census medoid 배치 전체 재실행을 비교한다. 채널 발동 유무가 달라지고 승패가 바뀌거나 정규화 최종 전력차가 0.10 이상 움직이면 event-bearing 실행을 causal로 기록한다. 이는 subsystem tagged RNG를 고정한 정밀 ablation이 아니라 `same-seed-full-rerun-placement-ablation-v1` best-effort 판정이다.

Stage 4 출력은 `Logs/h100-formation/` 아래 네 파일이다.

- `formation-events.jsonl`: eligible/fired/causal/legible와 QA probe 식별자
- `placement-leverage.jsonl`: 같은 편성·적·seed에서 8개 census medoid와 기본 배치의 승률 차이
- `healer-marginal-value.jsonl`: 힐러 포함/교체 pair의 승률·최종 전력차 marginal value와 Competent 선택 정렬
- `formation-report.json`: Coverage 통과, Competent prevalence/impact/legibility, placement, healer, Q5 및 Stage 5 밸런스 신호

Coverage가 다섯 채널을 모두 발동했지만 Competent Q5가 실패하면 `needs_stage_five_balance=true`로 기록한다. 힐러는 빈도를 고정하지 않고 marginal value가 양수인 상태에서 Competent가 선택했는지만 검사한다.

## Sunken Stage 5 방향 진단

`SunkenSolvabilityEvaluator`는 `site_sunken_bastion` 진입 직전의 캠페인 상태와 paired site replay 결과만 읽는 순수 판정기다. golden, 전투 수치, encounter authoring, save schema는 바꾸지 않는다. `SM.Editor.Validation` runner가 기존 6개 production policy로 실제 캠페인을 진행하면서 target site 진입 직전 roster·보유 archetype·배치·직전 reward를 캡처하고, `SM.HeadlessMetrics`에는 Unity/session 참조가 없는 record만 넘긴다.

same-state oracle은 캡처 프로필을 후보마다 메모리에서 복원한 뒤 현재 보유 hero로 만들 수 있는 합법 4인 편성과 배치를 같은 site battle seed 수열로 재실행한다. 편성 공간은 Stage 3의 495개 `BuildCombination`을 보유 roster로 필터링하고, 배치는 Stage 3의 8개 자동 medoid를 재사용한다. 따라서 기본 진단은 보유 편성을 전수 검사하되 360개 전체 배치 대신 8개 medoid를 쓰는 right-size 방향 판별이며, 전체 배치 인증으로 해석하지 않는다.

one-site lookback oracle은 직전 site의 reward option과 그 직후 가능한 recruit 한 번을 분기하고, 각 분기에서 counter-family별 top-K 편성과 같은 medoid를 재실행한다. 출력 지표와 Pro 판정 칸은 다음과 같다.

- `same_state_oracle_win_rate`: arrival 표본 중 같은 상태의 후보 하나라도 site를 완주한 비율
- `selection_regret`: same-state oracle 승률에서 실제 policy 선택 승률을 뺀 값
- `availability_gap`: recruit 추가 분기를 포함한 oracle과 same-state oracle의 차이
- `one_site_lookback_oracle`: same-state 또는 직전 reward/recruit 분기에서 완주 가능한 표본 비율
- `best_counter_family`: 표본별 최선 결과를 기준으로 가장 안정적인 counter family
- 판정: same-state 60% 미만은 encounter wall, 60% 이상 75% 미만은 혼합, 75% 이상이면서 regret 20%p 이상은 policy 문제다. same-state가 60% 미만이어도 lookback이 75% 이상이면 horizon 문제로 우선 분류한다. oracle 승리가 하나의 편성·배치에 수렴하고 실제 policy가 그 해답을 놓치면 puzzle lock으로 분류한다.

기본 출력 위치는 `Logs/h100-sunken-diagnosis/`이며 `arrival-snapshots.jsonl`, `oracle-candidates.jsonl`, `sunken-diagnosis.json`을 stable order와 UTF-8 no-BOM으로 기록한다. 실제 policy 선택은 한 번 더 재생해 seed 열과 replay manifest가 일치해야 하며, build/compose/예외 후보가 하나라도 있으면 runner는 실패한다.

## 재현 해시

`ReplayHash`는 새 전투 상태 정규화를 만들지 않는다. 기존 `BattleStateCanonicalHash.Compute(finalState)` 결과와 기존 activity telemetry replay hash를 길이-prefix된 UTF-8 바이트로 결합해 `H100ReplayHashV1`을 계산한다.

동일 sim version에서 같은 콘텐츠, seed, 입력과 실행 한도를 사용한 replay group의 모든 `ReplayHash`가 같아야 한다. 불일치는 무결성 게이트 실패다. 이 해시는 cross-platform 결정론을 새로 보증하지 않으며, canonical hash의 현재 보증 범위를 그대로 상속한다.

## 결정적 산출물

기본 출력 위치는 `Logs/h100-metrics/`이고 다음 파일을 생성한다.

- `battle-metrics.jsonl`
- `campaign-metrics.jsonl`
- `gate-report.json`
- `run-manifest.json`
- `player_visible_fact_ledger.jsonl`
- `information_surface_audit.json`
- `h100-bt1-gate-report.json`
- 선택적으로 `battle-metrics.csv`, `campaign-metrics.csv`

직렬화는 invariant culture, snake_case, 고정 property 순서를 사용한다. 레코드는 stable id 순서로, rule/count 컬렉션은 ordinal id 순서로 정렬한다. UTF-8 BOM과 실행 시각을 데이터 파일에 넣지 않는다. manifest hash는 정렬된 replay hash 열에서 계산한다.

## 게이트 평가

`h100-gates-v1.json`은 H100 Q1의 열 개 게이트와 v1 권고 임계치를 보존한다. 기준선 측정 뒤 인증 holdout을 열기 전에 한 번만 조정할 수 있고, 인증 데이터를 확인한 뒤 임계치를 변경하지 않는다.

`H100GateEvaluator`는 레코드에서 계산 가능한 관측치를 집계하고, blind review나 외부 severity처럼 별도 절차가 소유한 관측치는 explicit external observation으로만 받는다. 필요한 metric이 없으면 `metric unavailable`로 fail closed한다. 작은 smoke corpus나 Stage 1 runner 산출물이 전체 H100 통과를 주장해서는 안 된다.

## H100-BT1 게이트 스펙

`h100-gates-bt1-v1.json`은 AI 베타테스터 경험 루프의 완료 정의다. 기존 `h100-gates-v1.json`과 `gate-report.json`은 H100-RC1 동결 기준으로 byte 불변 유지하고, 새 스펙과 `h100-bt1-gate-report.json`은 별도 로드·평가·출력 경로를 사용한다. BT1 스펙의 열 개 게이트는 모두 hard AND-gate다.

| 게이트 | 완료 정의 | 역할 | 현재 평가 | 공급 envelope |
| --- | --- | --- | --- | --- |
| BT1 | 결정성·리플레이 무결성 | hard | `not_yet_evaluable` | E07 |
| BT2 | player-visible provenance | hard | 네 provenance metric 실측 평가 | E01 |
| BT3 | 정보 표면 완결성 | hard | 네 surface audit metric 실측 평가 | E02 |
| BT4 | 빌드 문법 유추 가능성 | hard | `not_yet_evaluable` | E02, E03, E07 |
| BT5 | 욕구 형성·커밋 | hard | `not_yet_evaluable` | E04, E07 |
| BT6 | 트랙 개방성·에이전시 연속성 | hard | E05 track oracle 실측 평가 | E03, E05 |
| BT7 | 의도 실현·payoff runway | hard | E05 conditional realization 실측 평가 | E03, E05 |
| BT8 | 적응형 도달성 | hard | `not_yet_evaluable` | E01, E06 |
| BT9 | 함정 옵션·버그급 지배성 부재 | hard | `not_yet_evaluable` | E08, E09 |
| BT10 | 베타테스터 재미·재시도 two-key | hard | `not_yet_evaluable` | E04, E05, E07 |

아직 `evaluable_now=false`인 게이트는 임계치가 미정이라는 뜻이 아니다. 임계치는 스펙에 동결되어 있지만 해당 envelope가 아직 완전한 metric supplier를 제공하지 않았다는 뜻이다. 일반 모드에서는 `not_yet_evaluable`과 `pass=null`을 출력해 조기 PASS/FAIL을 주장하지 않는다. 최종 RC strict 모드에서는 이를 FAIL로 취급한다.

role-aware 평가 의미는 다음과 같다.

- hard metric 누락은 `status=fail`, `pass=false`로 fail closed한다.
- diagnostic metric 누락은 `status=missing`, `pass=null`로 반드시 출력하며 전체 hard 판정을 막지 않는다.
- 관측된 diagnostic 값과 PASS/FAIL은 삭제하지 않고 report에 보존하되 릴리스 블록에는 사용하지 않는다.
- `owner_approval`은 BT10의 별도 boolean 임계치다. 기계 지표가 이를 대신할 수 없다.

### BT1-E05 intent track oracle

`SM.HeadlessCensus.IntentTrackEvaluator`는 정책에 노출되지 않는 evaluator-only 순수 탐색기다. 입력은 campaign 종료 뒤 `SM.Editor.Validation`이 실제 session에서 수집한 초기 roster/inventory 상태와 확정된 offer window DTO다. 탐색 목표는 승률 최대화가 아니라 E03 `ConceptContract.identity_predicates` 도달이며, 계약 관련 semantic만 상태 signature에 남기고 동일 상태당 최선 경로 하나를 memoize한다. `SM.HeadlessPolicies`는 `SM.HeadlessCensus`를 참조하지 않으며 미래 offer와 oracle 결과를 decision 시점에 읽을 수 없다.

agency window는 플레이어 선택이 실제로 발생하는 한 지점이다. 현재 campaign 표면에서는 도달한 사이트마다 배치 선택 1회와 보상 선택 1회가 각각 한 window다. 전투 node 자체는 자동 진행이므로 window가 아니다. v1 lever는 `deployment`, `reward`이고 탐색 DTO와 CLI는 `recruit`, `level_node`, `refit` 식별자를 파라미터로 수용하지만, E07이 실제 선택점을 열기 전에는 해당 window를 생성하지 않는다. 닫힌 lever가 필요한 variant는 `lever_pending`으로 분리하여 future-lever 기대치로 기록하고, `agency_gap`이나 v1 track 성공에 포함하지 않는다.

run별 핵심 값은 `TrackAvailable`, `FirstProgressTime`, `OracleRealizationTime`, `MaxAgencyDrought`, `Starved`, `PolicyCaptureRate = P(realized | TrackAvailable)`, `FalseHopeRate`, `PayoffRunway`, `IdentityRetentionAfterCounter`다. drought는 진척 또는 명시된 유효 대체가 하나도 제시되지 않은 연속 window 수이며 정확히 4개부터 starvation으로 판정한다. track 자체가 horizon 안에 없을 때도 starved다. capture 분모에는 `TrackAvailable=true`인 run만 들어간다.

run의 실패 원인은 상호배타적으로 내린다. v1 경로는 없지만 닫힌 future lever가 필요한 variant가 있으면 `lever_pending`, 그런 설명도 없으면 `agency_gap`이다. v1 경로가 있고 정책이 놓쳤으며 관련 E02 subject 위반이 있으면 `surface_gap`, 관련 위반이 없으면 `policy_gap`, 정책이 identity를 실현했지만 이후 전투 telemetry/beat에서 계약 payoff가 없으면 `combat_gap`이다. 실현과 payoff까지 있으면 `none`이다.

비율 하한은 `z=1.6448536269514722`인 one-sided 95% Wilson score lower bound를 쓴다. 기본 owner 표본은 10 anchor×16 seed이며 각 run에서 E03 owner variant 87개 전체를 anchor별 OR로 평가한다. 첫 stable variant는 coverage policy intent로만 유지하고 oracle 개방성 정의에는 사용하지 않는다. system medoid는 `isomorphic_recipe_count` 내림차순과 stable variant id로 고른 대표 N개만 v1에 포함한다. 출력 `intent_track_report.json` schema v2는 run·anchor·variant별 술어 판정, `v1_track`/`lever_pending`/`true_unavailable` 분해, predicate coverage, tier, gap 분포, BT6/BT7 공급 metric과 현재 PASS/FAIL을 invariant snake_case·ordinal order·UTF-8 no-BOM으로 기록하고 wallclock/GUID를 포함하지 않는다.

### H100-RC1 migration map

| legacy gate id | BT1 역할 | 승계 BT 게이트 |
| --- | --- | --- |
| `integrity_reproducibility` | hard | BT1, BT9 |
| `campaign_completion` | diagnostic | BT6, BT8 |
| `build_ecology` | diagnostic | BT4, BT9 |
| `effective_build_diversity` | diagnostic | BT4, BT6, BT7 |
| `decision_depth` | diagnostic | BT4, BT5, BT6 |
| `formation_significance` | diagnostic | BT3, BT7, BT9 |
| `spectator_arc` | diagnostic | BT10 |
| `depth_causality` | diagnostic | BT3, BT7, BT9 |
| `blind_fun_approval` | diagnostic | BT10 |
| `final_reproduction` | diagnostic | BT1, BT10 |

기존 `integrity_reproducibility`만 legacy hard 판정을 유지한다. 나머지 RC1 임계치와 프록시는 경험 실패 분해를 위한 diagnostic으로 보존한다. 결정성, provenance, 도달성, 기계적 진실성의 hard 축은 각각 BT1, BT2, BT6·BT8, BT3·BT9로 승계한다.

## 실행과 검증

축소 witness는 다음 명령으로 실행한다. `-Policy` 기본값은 Stage 1 행동을 보존하는 `greedy-v1`이다.

```powershell
pwsh -File tools/h100-metrics.ps1 -Policy greedy-v1 -BattleCount 4 -CampaignCount 1 -ReplayCopies 2
```

E02 정보 표면만 실콘텐츠에서 빠르게 재측정할 때는 다음 명령을 쓴다. metric FAIL과 gap 존재는 출력하되 콘텐츠를 자동 수정하거나 명령을 실패시키지 않는다.

```powershell
pwsh -File tools/h100-surface-audit.ps1
```

E05 기본 실측은 owner 10×16 seed와 system medoid 대표 8개를 같은 coverage campaign 경로에서 실행한다. BT6/BT7가 임계치에 못 미치면 wrapper는 측정 실패를 숨기지 않고 `status=fail`을 출력하되, artifact 생성 자체가 정상인 한 프로세스 오류로 바꾸지 않는다.

```powershell
pwsh -File tools/h100-intent-track.ps1
```

64-seed RC 경로는 `-SeedCount 64`를 명시한다. 반복 결정성은 같은 인자로 별도 output directory에 두 번 실행한 `intent_track_report.json`의 byte hash 일치로 검증한다.

대량 실행은 같은 명령에서 수를 명시한다. `BattleCount`는 같은 입력을 반복하는 replay group 수이며 실제 battle record 수는 replay copy 수만큼 증가한다.

```powershell
pwsh -File tools/h100-metrics.ps1 -BattleCount 10000 -CampaignCount 10000 -ReplayCopies 2 -CampaignSiteSafety 32
```

wrapper는 Unity batch execute-method로 실제 content/session/simulator 경로를 실행한 뒤 필수 산출물 존재와 `replay_hash_match_rate == 1.0`을 확인한다. 전체 게이트는 아직 측정되지 않은 외부·paired·blind 지표 때문에 실패할 수 있으며, 이는 runner 실패와 구분한다.

코드 변경의 기본 검증 순서는 다음과 같다.

```powershell
pwsh -File tools/unity-bridge.ps1 test-batch-fast
pwsh -File tools/test-harness-lint.ps1 -RepoRoot .
pwsh -File tools/h100-surface-audit.ps1
pwsh -File tools/h100-metrics.ps1 -BattleCount 4 -CampaignCount 1 -ReplayCopies 2
```

실제 콘텐츠 runner는 Unity/editor 경계를 밟으므로 pure `FastUnit`만으로 실행 증거를 대체하지 않는다.

진형 Stage 4의 축소 paired runner는 다음 명령으로 검증한다.

```powershell
pwsh -File tools/h100-formation.ps1 -SeedCount 5 -CompetentPolicy competent-formation-v1
```

Sunken Stage 5의 방향 판별 runner는 다음 명령으로 검증한다. 기본값은 policy당 seed 1개, 보유 편성 전수, 8개 medoid, lookback counter-family top-12다.

```powershell
pwsh -File tools/h100-sunken-diagnosis.ps1
```

## 현재 한계와 후속

- test-only `IPlaythroughDecisionPolicy`는 production이 참조하지 않는다. production-safe port는 ADR-0031의 `SM.HeadlessPolicies` + `SM.Editor.Validation` projection adapter로 분리됐으며 상세 observation/action 계약은 `h100-headless-policy-contract.md`가 소유한다.
- tagged/subsystem RNG stream이 없어 공용 RNG를 보존하는 counterfactual ablation을 아직 보증하지 않는다.
- posture는 현재 정책 action 축이 아니며 자세별 paired rollout은 후속 단계다.
- `SM.HeadlessMetrics` 자체는 pure .NET으로 빌드 가능하지만 실제 콘텐츠/session composition은 Unity adapter에 남아 있다. 2M+ 전투용 pure dotnet CLI는 content snapshot과 campaign orchestration port를 분리한 뒤 추가한다.
- Sunken Stage 5 기본 실행은 방향 판별용 small-N/medoid 표본이다. 다중 seed·360개 전체 배치·대규모 holdout 인증은 별도 실행 예산으로 남긴다.
- 정보 표면 v1은 E01 fact reachability를 정상 플레이 discoverability의 보수적 proxy로 사용한다. catalog unlock 시점과 실제 run별 offer 빈도는 E03 이후의 별도 표본이 필요하다.
