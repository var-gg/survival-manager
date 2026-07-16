# ADR-0030 H100 헤드리스 계측 순수 경계

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-16
- 소스오브트루스: `docs/04_decisions/adr-0030-h100-headless-metrics-boundary.md`
- 관련문서:
  - `docs/03_architecture/h100-headless-metrics-contract.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/04_decisions/adr-0006-combat-sim-boundary.md`
  - `docs/04_decisions/adr-0029-deterministic-fixed-point-sim.md`
  - `docs/04_decisions/adr-0031-h100-headless-policy-boundary.md`

## 문맥

H100 release-candidate 판정은 전투와 캠페인의 실전 출현 빈도, 인과 영향, 유능 플레이 선택, 재현성을 같은 구조화 표면에서 비교해야 한다. 기존 저장소에는 `BattleStateCanonicalHash`, activity replay hash, real-content editor sweep와 테스트용 playthrough policy가 있지만, 게이트 스키마와 결정적 레코드 포맷을 소유하는 production 경계는 없었다.

계측을 `SM.Editor` 또는 `SM.Unity`에만 두면 대규모 pure runner로 옮길 때 스키마와 판정 로직을 다시 작성해야 한다. 반대로 실제 content/session composition을 새 순수 assembly로 옮기면 현재 authored content와 `GameSessionState` 경계를 우회하고 작업 범위를 크게 넓힌다.

## 결정

- 새 `SM.HeadlessMetrics` asmdef가 레코드, projection, replay hash 조합, 결정적 writer, H100 gate spec/evaluator를 소유한다.
- `SM.HeadlessMetrics`는 `SM.Core`, `SM.Combat`만 참조하고 `noEngineReferences=true`를 사용한다. `SM.Content`, `SM.Meta`, persistence, `SM.Unity`, `SM.Editor`는 참조하지 않는다.
- 실 콘텐츠와 캠페인 orchestration은 `SM.Editor.Validation` runner가 소유한다. runner는 기존 `RuntimeCombatContentLookup`, `GameSessionState`, `BattleResolver` 경로를 사용해 결과를 순수 record로 넘긴다.
- `ReplayHash`는 `BattleStateCanonicalHash`를 재사용하고 activity replay hash를 versioned envelope로 결합한다. 별도 canonicalizer를 만들지 않는다.
- 계측은 sim/save truth를 변경하지 않는 additive projection이다. `BattleHashCorpus` golden은 수정하지 않는다.
- 게이트 metric이 없으면 evaluator가 fail closed한다. Stage 1의 부분 관측으로 H100 pass를 선언하지 않는다.
- test-only `IPlaythroughDecisionPolicy`를 production assembly가 참조하지 않는다. 정책 port 승격은 ADR-0031의 별도 pure policy 경계로 수행하고 tagged RNG counterfactual은 후속으로 남긴다.

## 검토한 대안

| 대안 | 판정 | 이유 |
| --- | --- | --- |
| `SM.Editor`에 레코드·판정을 모두 둔다 | 기각 | Unity/editor와 무관한 schema, hash, 집계가 대규모 runner에서 재사용되지 않는다. |
| `SM.Meta`에 계측을 둔다 | 기각 | 캠페인 규칙 truth와 release 판정·파일 출력 책임이 섞이고 `SM.Combat` 단독 전투 corpus 사용성이 떨어진다. |
| 기존 BatchOnly test report만 확장한다 | 기각 | production runner가 테스트 assembly에 의존하게 되고 test-only policy seam과 report shape가 장기 계약이 된다. |
| 순수 계측 core와 editor composition runner를 분리한다 | 채택 | 현재 runtime 경계를 보존하면서 schema·hash·판정을 Unity 독립적으로 재사용할 수 있다. |

## 결과와 영향

- 새 asmdef와 exact reference guard가 추가된다. `SM.Editor`와 `SM.Tests.FastUnit`은 순수 계측 assembly를 소비할 수 있지만 역방향 참조는 없다.
- Json.NET은 결정적 파일 포맷을 위해 precompiled reference로 사용한다. JSON property 순서와 컬렉션 정렬은 코드와 FastUnit으로 고정한다.
- 실제 콘텐츠 smoke는 Unity batchmode가 필요하다. pure FastUnit은 hash, projection, serialization, fail-closed evaluator를 검증한다.
- ADR-0031 이후 campaign runner는 player-visible deterministic 정책 여섯 개를 제공한다. decision counterfactual, formation paired rollout, blind 평가 metric은 여전히 unavailable로 실패한다.
- 후속 pure dotnet CLI는 `SM.HeadlessMetrics`를 그대로 재사용할 수 있으나 content snapshot과 campaign policy port를 추가로 분리해야 한다.

## 승인 조건

HUB 구조 리뷰에서 새 asmdef 경계, `SM.Editor.Validation` composition ownership, test-only policy를 복제하지 않고 후속 port로 남긴 결정을 승인한 뒤 상태를 `active`로 승격한다.

**HUB 승인 (2026-07-16)**: 경계·composition ownership·정책 port 후속 결정 승인. `BuildBoundaryGuardFastTests`가 `SM.HeadlessMetrics`의 no-engine + exact(Core, Combat) + no-reference(Content/Meta/Persistence/Unity/Editor)를 강제하도록 강화됨을 확인. 검증: test-batch-fast 963/0(FastUnit + H100 순수 7건 + 경계 가드), `h100-metrics.ps1` 6전투 smoke에서 replay hash 일치율 1.0(재현성 witness). `BattleHashCorpus` 골든 무변경. 상태 `active` 승격.
