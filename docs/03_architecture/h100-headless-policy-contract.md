# H100 no-cheat 정책 계약

- 상태: proposed
- 소유자: repository
- 최종수정일: 2026-07-17
- 소스오브트루스: `docs/03_architecture/h100-headless-policy-contract.md`
- 관련문서:
  - `docs/03_architecture/h100-headless-metrics-contract.md`
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

모든 decision은 `Rationale`과 finite `EstimatedValue`를 반환한다. runner는 policy/kind/chapter/site/seed/value/reason을 단일 행 로그로 남긴다. `HeadlessPolicyGuard`는 observation과 action의 null, 중복, 범위, legal set, finite value를 fail closed한다.

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

## deferred

- SearchPlanner 깊은 lookahead/MCTS와 common-random counterfactual
- posture 결정축과 자세별 paired rollout
- content snapshot/campaign orchestration을 포함한 pure dotnet CLI
