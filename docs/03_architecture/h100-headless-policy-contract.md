# H100 no-cheat 정책 계약

- 상태: proposed
- 소유자: repository
- 최종수정일: 2026-07-17
- 소스오브트루스: `docs/03_architecture/h100-headless-policy-contract.md`
- 관련문서:
  - `docs/03_architecture/h100-headless-metrics-contract.md`
  - `docs/03_architecture/h100-build-space-census-contract.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/04_decisions/adr-0031-h100-headless-policy-boundary.md`

## 목적

이 문서는 H100 campaign runner가 실제 플레이어와 같은 현재 정보로 정책을 실행하고, 미래 RNG·적 실수치·미공개 node 접근을 코드 경계에서 차단하는 계약을 고정한다. Stage 2의 작은 표본 비교는 정책 방향 witness이며 H100 통계 게이트 통과 선언이 아니다.

## 경계와 observation whitelist

`SM.HeadlessPolicies`는 `SM.Combat`만 참조하는 `noEngineReferences=true` asmdef다. 공개 API는 `IHeadlessPolicy`, observation/decision value contract, 여섯 production 정책과 QA 전용 `CoveragePolicy`, factory, guard다. session, content lookup, authored object, persistence, editor API를 constructor나 method로 받지 않는다.

정책 observation에 허용되는 정보는 다음과 같다.

- 현재 expedition squad의 hero/archetype/race/class/role, level, 현재 공개 HP, 선호 anchor, 현재 배치 여부
- 각 영웅의 공개 skill card mechanics, flex active/passive skill id, 장착 item과 확정 affix mechanics, 선택된 passive node id
- 공개된 여섯 deployment anchor와 4-cap
- 현재 선택 chapter/site id
- 현재 선택 node 한 개의 enemy preview: archetype identity에서 알 수 있는 race/class/role/default anchor, faction, difficulty band, threat skull, 공개 boss/reward tag
- Squad Builder가 표시하는 현재 **배치 분대 기준** synergy count와 Compendium의 synergy threshold/effect catalog
- Town HUD의 gold/echo wallet과 현재 run의 temporary augment mechanics
- reward 화면에 이미 제시된 option의 표시 금액/payload id 및 item/temporary augment 공개 mechanics
- 현재 결정을 위해 runner가 파생한 non-zero seed

허용 여부는 필드 출처가 아니라 플레이어가 현재 화면에서 같은 의미를 읽을 수 있는지로 판단한다. builder는 ID와 mechanics collection을 ordinal 정렬하고, 정책에는 authored definition이나 snapshot을 전달하지 않는다.

금지 정보는 미래 node 목록, unrevealed encounter, RNG state/다음 roll, resolved enemy base stat/trait/rule package, `BattleState`, `GameSessionState`, authored definition 참조다. reward item affix는 선택 적용 뒤 생성되므로 선택 전에는 공개 mechanics가 아니며 비워 둔다. `H100PolicyObservationBuilder`는 현재 `GetSelectedExpeditionNode()`만 투영하고 enemy preview vocabulary를 확장하거나 future node를 순회하지 않는다.

## 결정 표면과 정책

현재 policy action은 deployment와 reward choice 두 축이다. campaign site는 전 사이트 클리어가 필요한 선형 진행이라 runner가 다음 미클리어 site로 이동한다. `TeamPostureType`은 실제 session surface에 있지만 Stage 2 범위에서 제외한다.

| 정책 | 결정 규칙 |
| --- | --- |
| `random-legal-v1` | observation seed로 hero/anchor 또는 reward를 합법 범위에서 결정적으로 shuffle |
| `greedy-v1` | roster 앞 4명을 Stage 1과 같은 class front/back 순서로 배치하고 첫 reward 선택 |
| `competent-doctrine-v1` | 공개 race/class count로 race@4, class@3, 하위 threshold 우선 |
| `competent-formation-v1` | front/back 균형, support 보호, class coverage 우선 |
| `competent-counter-adaptive-v1` | 현재 공개 enemy class/anchor preview에 대응하는 roster/배치 우선 |
| `competent-search-planner-v1` | 공개 상태에서 상위 roster 조합과 legal anchor permutation을 최대 4,096개 평가하는 bounded 1-ply |
| `qa-formation-coverage-v1` | 힐러·역할 완비·독트린·다섯 진형 채널용 anchor 조건을 결정적으로 표본화하는 발동 가능성 전용 QA 정책 |

네 유능 정책의 canonical ID는 `H100GateEvaluator`가 `competent` cohort로 집계할 수 있도록 `competent-` 접두사를 고정한다. `qa-formation-coverage-v1`은 production 정책 목록에서 제외하며 유능 플레이나 밸런스 가치를 주장하지 않는다. 짧은 별칭은 factory 입력에서만 허용하고 metric에는 canonical ID를 기록한다.

모든 decision은 `Rationale`, finite `EstimatedValue`, 하나 이상의 `EvidenceFactIds`를 반환한다. observation에는 `SM.Editor.Validation` projector가 만든 signal key→fact id index만 additive로 들어가며 fact schema나 ledger 구현은 정책 assembly에 노출되지 않는다. runner는 policy/kind/chapter/site/seed/value/reason을 단일 행 로그로 남기고 별도 fact ledger에 action과 evidence link를 기록한다. `HeadlessPolicyGuard`는 observation과 action의 null, 중복, 범위, legal set, finite value, 빈·중복 evidence id를 fail closed한다. fact 존재와 결정 시점은 `SM.HeadlessMetrics.PlayerVisibleFactLedgerAuditor`를 호출하는 Editor 조립층이 검증한다.

정책별 최소 evidence 신호는 실제 선택·가치 계산 경로와 다음처럼 대응한다.

| 정책 | deployment evidence | reward evidence |
| --- | --- | --- |
| `random-legal-v1` | seed, chapter/site context, roster, legal deployment surface, enemy preview | seed, chapter/site context, 현재 reward surface |
| `greedy-v1` | roster order/role, legal deployment surface, enemy preview | 현재 reward surface |
| `competent-doctrine-v1` | roster race/class/readiness, legal deployment surface, enemy preview | reward surface, deployed roster identity |
| `competent-formation-v1` | roster role/anchor/readiness, legal deployment surface, enemy preview | reward surface의 protection/healing payload |
| `competent-counter-adaptive-v1` | roster, legal deployment surface, 현재 enemy preview | reward surface의 counter payload |
| `competent-search-planner-v1` | roster, legal deployment surface, 현재 enemy preview | reward surface, deployed roster identity |
| `qa-formation-coverage-v1` | seed, roster role/class, legal deployment surface, enemy preview | reward surface, deployed roster identity |

`random-legal-v1`의 무작위 선택도 외부 RNG state가 아니라 player-visible observation에 고정된 decision seed fact를 인용한다. 모든 reward option이 없는 결정도 빈 reward surface fact를 근거로 `option=-1`을 반환한다. 정책이 읽지 않은 wallet, item mechanics, synergy catalog 전체를 편의상 모두 인용하지 않는다.

## 컨셉 의도 정책과 주입 경계

`ConceptCommitPolicy`는 기존 여섯 production 정책 cohort와 `qa-formation-coverage-v1` factory 표면을 바꾸지 않는 별도 BT1 정책이다. `IHeadlessPolicy`의 배치·보상 시그니처를 그대로 구현하되, 한 campaign 동안 `IntentState`를 policy instance 내부에 보관하고 모든 결정에 `keep`, `advance`, `substitute`, `counter-adapt`, `pivot`, `abandon` 중 하나의 이유를 남긴다. 상태는 static global이 아니며 campaign마다 새 policy instance를 만든다.

정책 assembly에는 evaluator 계약 대신 `HeadlessConceptIntent`만 존재한다. 이 DTO는 identity predicate, progress milestone, payoff witness ID, substitution, flex slot, counter affordance, availability tier, pivot condition 같은 정렬된 문자열만 운반한다. `SM.Editor.Validation.H100ConceptIntentProjector`가 E03 `ConceptContract` 하나를 이 DTO로 투영하며 `SM.HeadlessPolicies`는 `SM.HeadlessCensus`를 참조하지 않는다.

`HeadlessConceptIntent`의 constructor injection은 session, content lookup, RNG service 주입이 아니라 순수 불변 데이터 주입이다. 따라서 ADR-0031의 constructor 금지는 그대로 유지하며 이 DTO 주입은 금지 대상이 아니다. `GameSessionState`, authored content, catalog, truth graph, 미래 offer provider는 constructor와 decision method 양쪽에서 계속 금지한다.

두 실행 lane은 다음처럼 분리한다.

| lane | 정책 입력 | 입증 범위 |
| --- | --- | --- |
| coverage | evaluator adapter가 E03 catalog에서 계약 한 개만 `HeadlessConceptIntent`로 투영해 주입 | 부여된 의도를 유지·진전·전환하는 agency/realization 경로. catalog 전체나 다른 계약은 정책에 비공개 |
| discovery | constructor 주입 없음. 현재 배치 synergy count와 공개 threshold 중 roster로 도달 가능한 가장 가까운 다음 tier를 먼저 고르고, 없으면 공개 skill status motif, 마지막으로 roster tag motif를 안정 ID 순서로 선택 | catalog 없이 가시 fact에서 의도를 자체 형성하는 경로. catalog 매핑은 run 종료 후 evaluator 책임 |

배치 selector는 가중합으로 컨셉·생존·대체를 섞지 않는다. 전멸 위험이면 counter safety, identity progress, milestone, substitution 순서로 비교하고, 평시에는 identity progress, milestone, substitution, 현재 배치 보존 순서로 비교한 뒤 hero/placement stable signature로 tie-break한다. 보상도 counter match가 필요한 경우를 먼저 분리한 뒤 공개 item/augment mechanics의 컨셉 동사·태그 일치, substitution, option index 순서로 고른다. 기존 `EstimatedValue`는 진단값일 뿐 컨셉 selector의 선택 기준이 아니다.

| 이유 | 판정 규칙 |
| --- | --- |
| `keep` | 현재 선택이 직접 진전하지 않지만 가시 track을 포기할 근거가 아직 없음 |
| `advance` | identity progress가 증가하거나 새 progress milestone을 완료하는 선택 |
| `substitute` | primary identity unit/mechanics가 현재 legal set에 없고 명시된 substitution이 존재하는 선택 |
| `counter-adapt` | 공개 threat skull 또는 공개 HP가 전멸 위험을 나타내어 identity를 최대한 보존한 counter 선택 |
| `pivot` | 진전 없는 결정이 두 번째 이어져 선언된 pivot condition을 실행하는 선택 |
| `abandon` | pivot 뒤에도 진전·유효 대체가 없어 현재 intent를 종료하는 선택 |

## hypothesis, commit_t, intent trace

`BuildHypothesis`는 action 선택 전에 생성한다. `ClaimedEdge`, E01 fact ID인 `EvidenceRefs`, `Confidence`, `OpenQuestion`, `ExpectedPayoff`, `NextAcquisitionPlan`, `FalsificationSignal`, 선언 decision index와 payoff 관측 decision index를 구조화해 보존한다. E04 정책 표면은 payoff를 관측하지 않으므로 관측 index는 `-1`이며, 후속 payoff collector가 생겨도 선언 index보다 앞선 payoff를 참조하면 commit 판정이 실패해야 한다.

`commit_t`는 다음 다섯 조건을 모두 충족한 첫 decision index다.

- 서로 다른 prior evidence fact가 2개 이상이다.
- 기대 payoff가 비어 있지 않다.
- 다음 acquisition plan이 비어 있지 않다.
- 실제 action이 새 milestone을 진전시키거나 희소 자원을 intent에 투자한다.
- 실패 시 pivot condition이 비어 있지 않다.

추가로 hypothesis 선언이 payoff 관측보다 앞서야 한다. `IntentCommitEvaluator`는 각 조건을 독립 boolean으로 내보내고 AND로만 판정하며, 한 번 기록한 `CommitDecisionIndex`를 후속 선택이 덮어쓰지 않는다.

정책 assembly의 `HeadlessIntentDecision`은 policy-side snapshot이다. `SM.Editor.Validation.H100IntentTraceCollector`가 이를 `SM.HeadlessMetrics.IntentTraceRecord`로 투영하고 `IntentTraceArtifactWriter`가 `intent_trace.jsonl`을 wallclock, GUID, BOM 없이 timeline/stable ID 순서로 기록한다. 각 행에는 이유, action, milestone/희소 자원 진전, hypothesis, intent state, commit 조건별 결과와 `is_commit`이 들어간다. fact ledger decision과 intent trace 행 수가 다르거나 provenance audit가 0이 아니면 runner는 fail closed한다.

## 실행과 검증

정책 한 개의 campaign metric은 다음처럼 실행한다. 기본 정책은 `greedy-v1`이다.

```powershell
pwsh -File tools/h100-metrics.ps1 -Policy competent-search-planner-v1 -BattleCount 4 -CampaignCount 1 -ReplayCopies 2
```

여섯 정책 smoke는 각 정책마다 output directory를 분리한다.

```powershell
$policies = 'random-legal-v1','greedy-v1','competent-doctrine-v1','competent-formation-v1','competent-counter-adaptive-v1','competent-search-planner-v1'
foreach ($policy in $policies) {
  pwsh -File tools/h100-metrics.ps1 -Policy $policy -BattleCount 1 -CampaignCount 1 -ReplayCopies 2 -OutputDirectory "Logs/h100-$policy"
}
```

같은 seed set의 실제 결과 방향 witness는 다음 명령이다.

```powershell
pwsh -File tools/h100-policy-witness.ps1 -CampaignCount 8 -CampaignSiteSafety 32
```

`policy-witness.json`의 `improved`는 SearchPlanner completion rate가 Greedy보다 높거나, completion이 같더라도 battle win rate가 높을 때만 true다. 통계적 유의성, Wilson interval, 1,000-seed holdout은 후속 campaign agency stage에서 닫는다.

진형 Stage 4는 Coverage와 Competent를 분리해 실행한다. Coverage runner의 통제된 개전 접촉은 공개된 전투 상태를 구성한 뒤 production combat resolver를 통과하며, 산출물의 `coverage_probe_channel_id`로 명시된다. 이 표본은 발동 가능성만 증명하고 Competent의 자연 발생 prevalence/impact 집계에는 포함하지 않는다.

```powershell
pwsh -File tools/h100-formation.ps1 -SeedCount 5 -CompetentPolicy competent-formation-v1
```

BT1-E04 intent trace smoke는 동일한 real campaign/session 경로에서 coverage 단일 계약과 catalog-hidden discovery를 각각 실행한다.

```powershell
pwsh -File tools/h100-intent-trace.ps1 -SeedCount 8 -Lanes both -CoverageAnchorId anchor_iron_line
```

각 lane의 `intent_trace_summary.json`에서 `missing_trace_count=0`, `hidden_fact_use_count=0`, `campaigns_with_commit=8`을 요구한다. 같은 seed와 intent의 policy decision 및 JSONL은 byte-identical이어야 한다. 현재 action surface는 deployment와 reward 두 종류뿐이며 영입, node, Refit decision point 개방은 E07 범위다.

BT1-E05는 coverage lane을 E03 owner anchor별로 다시 실행하지만 정책 계약을 넓히지 않는다. 정책의 coverage intent는 첫 stable variant 하나로 고정하되, campaign 종료 후 oracle은 같은 offer stream에 anchor의 모든 E03 variant를 대조해 OR 개방성을 계산한다. `H100CampaignCorpusRunner`의 optional observer가 결정 전 배치·보상 표면과 전투 후 payoff만 복제하고, campaign 종료 뒤 Editor adapter가 순수 `IntentTrackEvaluator`에 DTO를 전달한다. 정책은 자기 `HeadlessConceptIntent`, 현재 player-visible observation, 누적 `IntentState`만 보며 oracle search result, 다른 선택지의 미래 결과, 이후 offer stream은 읽지 않는다. 기본 실측 진입점은 `pwsh -File tools/h100-intent-track.ps1`이다.

## deferred

- SearchPlanner 깊은 lookahead/MCTS와 common-random counterfactual
- posture 결정축과 자세별 paired rollout
- 영입, node, Refit decision point와 beta-runner checkpoint 연결(E07)
- content snapshot/campaign orchestration을 포함한 pure dotnet CLI
