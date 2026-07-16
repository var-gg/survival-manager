# ADR-0031 H100 no-cheat 정책 순수 경계

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-17
- 소스오브트루스: `docs/04_decisions/adr-0031-h100-headless-policy-boundary.md`
- 관련문서:
  - `docs/03_architecture/h100-headless-policy-contract.md`
  - `docs/03_architecture/h100-headless-metrics-contract.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/04_decisions/adr-0030-h100-headless-metrics-boundary.md`

## 문맥

H100 Stage 1 runner는 `H100SessionDriver.ApplyScriptedDeployment`에 roster 순서 기반 front/back 배치와 reward index 0을 하드코딩했다. 테스트 assembly에는 `IPlaythroughDecisionPolicy`가 있었지만 production assembly가 test assembly를 참조할 수 없고, 해당 타입은 `GameSessionState`와 `RewardChoiceKind`에 직접 결합돼 정책 비교용 순수 경계로 사용할 수 없었다.

Stage 2는 같은 player-visible 정보에서 여섯 정책을 교체 실행하고 Search/Planner와 Greedy의 결과 차이를 비교해야 한다. 정책이 `GameSessionState`, authored content, resolved encounter 또는 RNG service를 직접 받으면 미래 RNG, 적 실수치, 미공개 node 접근을 구조적으로 막을 수 없다. 한편 정책을 `SM.HeadlessMetrics`에 넣으면 계측 record/gate와 행동 선택 책임이 섞인다.

## 결정

- 새 `SM.HeadlessPolicies` asmdef가 player-visible observation/decision DTO, `IHeadlessPolicy`, 여섯 정책, 결정적 heuristic/search, `HeadlessPolicyGuard`를 소유한다.
- `SM.HeadlessPolicies`는 `SM.Combat`만 참조하고 `noEngineReferences=true`를 사용한다. `SM.Content`, `SM.HeadlessMetrics`, `SM.Meta`, persistence, `SM.Unity`, `SM.Editor`는 참조하지 않는다.
- `IHeadlessPolicy`는 실제 대체 구현 여섯 개가 같은 runner에서 교체 실행되므로 interface 도입 기준을 충족한다. 정책 constructor에는 session/content/RNG service를 주입하지 않는다.
- observation의 허용 기준은 **UI-parity**다. 현재 화면에서 플레이어가 읽을 수 있는 자기 영웅의 skill card와 flex skill, 장착 item/affix mechanics, 선택 passive node, wallet, 보유 temporary augment, 배치 분대 synergy count와 공개 synergy tier 효과, 이미 제시된 reward payload mechanics를 빠짐없이 담되 화면 밖 정보는 담지 않는다.
- reward item의 실제 affix는 선택 적용 뒤 결정적으로 생성되므로 제시 시점 observation에는 넣지 않는다. 이미 소유한 장비의 확정 affix만 투영한다.
- 현재 chapter/site, 공개 anchor, 현재 공개 enemy preview, 결정 seed는 기존대로 유지한다. 미래 node, RNG state/다음 roll, resolved enemy stat, encounter 내부 rule package는 담지 않으며 enemy preview vocabulary도 확장하지 않는다.
- `SM.Editor.Validation.H100PolicyObservationBuilder`가 `GameSessionState`와 content snapshot을 현재 player-facing preview와 같은 정보로 투영하는 유일한 adapter다. 정책에는 이 DTO만 전달한다.
- builder는 모든 ID·mechanics collection을 ordinal 기준으로 정렬하고, synergy count는 Squad Builder UI와 같은 **현재 배치 영웅** 기준으로 계산한다.
- `HeadlessPolicyGuard`가 observation 형태와 legal action을 fail closed한다. 신규 mechanics는 관측 전용이라 guard의 action 검증 규칙은 바꾸지 않는다. `BuildBoundaryGuardFastTests`가 asmdef exact reference를 고정하고 FastUnit이 observation vocabulary와 여섯 정책의 결정론을 검증한다.
- `H100MetricsRunSettings.PolicyId`와 `SM_H100_POLICY`가 정책을 선택한다. 기본값은 `greedy-v1`이며 Stage 1 roster-order/front-back/reward-first 행동을 보존한다.
- campaign profile identity에서 policy id를 제외해 같은 seed 비교가 reward/session context까지 같은 입력을 사용하게 한다.
- posture는 Stage 2에 포함하지 않는다. 현재 campaign site 진행은 전 사이트 클리어가 필요한 선형 기계이므로 policy action이 아니라 runner orchestration으로 유지한다.

## 검토한 대안

| 대안 | 판정 | 이유 |
| --- | --- | --- |
| 정책을 `SM.HeadlessMetrics`에 추가 | 기각 | 계측 schema/gate와 행동 선택 책임이 섞이고 두 assembly의 재사용 축이 다르다. |
| 정책을 `SM.Meta`에 추가 | 기각 | H100 평가 정책이 gameplay rule truth로 승격되고 runner 전용 portfolio가 domain API가 된다. |
| `SM.Editor.Validation`에 여섯 정책을 직접 구현 | 기각 | Unity/editor 없는 정책 결정론·no-cheat 경계 검증과 후속 CLI 재사용이 막힌다. |
| test-only `IPlaythroughDecisionPolicy`를 참조 | 기각 | production-to-test 의존이며 session/Unity 타입이 정책 입력으로 누출된다. |
| 별도 pure `SM.HeadlessPolicies` + Editor projection adapter | 채택 | 정책 다형성, no-cheat compile boundary, 실제 session composition을 동시에 보존한다. |

## 결과와 영향

- `SM.Editor`와 `SM.Tests.FastUnit`이 `SM.HeadlessPolicies`를 소비하고 역방향 참조는 없다.
- additive DTO 확장으로 `IHeadlessPolicy` 시그니처와 기존 여섯 정책 구현은 바뀌지 않는다. 정책은 확장된 observation에서도 같은 legal decision surface만 반환한다.
- build-intent 정책은 자기 roster의 공개 skill/item/passive, 현재 경제와 augment, 배치 synergy breakpoint, reward mechanics를 조합해 빌드 진전을 판단할 수 있다.
- `RandomLegalPolicy`도 observation seed에서 자체 deterministic stream을 매번 재구성한다. 같은 seed와 observation은 호출 순서와 무관하게 같은 결정을 만든다.
- `SearchPlannerPolicy` v1은 공개 상태의 roster 조합과 legal anchor permutation을 최대 4,096개 평가하는 bounded 1-ply다. 깊은 lookahead/MCTS는 후속이다.
- `H100PolicyWitnessRunner`는 같은 seed/profile identity로 Greedy와 SearchPlanner를 실행하고 completion 또는 battle win rate가 개선되지 않으면 실패한다. 작은 N smoke는 방향 witness이며 통계 인증이 아니다.
- sim/save truth와 `BattleHashCorpus` golden은 변경하지 않는다. 정책에 따라 campaign metric이 달라지는 것은 의도된 결과다.

## 승인 조건

HUB 구조 리뷰에서 새 asmdef 분리, `SM.Combat` 단일 참조, Editor projection ownership, 기본 Greedy 회귀, 1-ply 범위를 승인한다. 승인 뒤 상태를 `active`로 바꾸고 `test-batch-fast`, 여섯 정책별 `h100-metrics.ps1`, `h100-policy-witness.ps1` 결과를 기록한다.

**HUB 승인 (2026-07-16)**: 정책/계측 분리(sibling pure asmdef), `SM.Combat` 단일 참조 + no-engine, Editor projection 유일 adapter, 기본 greedy 회귀, SearchPlanner 1-ply v1 범위 승인. `BuildBoundaryGuardFastTests`가 `SM.HeadlessPolicies`의 no-engine + exact(Combat) + no-reference(Content/HeadlessMetrics/Meta/Persistence/Unity/Editor) 강제 확인. no-cheat 계약(observation player-visible only + HeadlessPolicyGuard fail-closed + 금지 vocabulary)이 컴파일·테스트로 고정됨을 확인. HUB 독립 재검증: test-batch-fast 972/968/0(FastUnit + 정책 포트폴리오 + 경계 가드). 6정책 replay hash 각 1.0. **첫 실측 신호: greedy·planner 캠페인 완주율 0%(전투 승률 88%인데 완주 0 = 난이도 벽 계량화)** — Stage 5+ 밸런스 대상. 상태 `active`.
