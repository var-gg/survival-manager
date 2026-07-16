# H100 헤드리스 계측 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-16
- 소스오브트루스: `docs/03_architecture/h100-headless-metrics-contract.md`
- 관련문서:
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`
  - `docs/03_architecture/sim-sweep-and-balance-kpis.md`
  - `docs/03_architecture/telemetry-contract.md`
  - `docs/04_decisions/adr-0030-h100-headless-metrics-boundary.md`

## 목적

이 문서는 H100 헤드리스 release-candidate 판정의 원시 레코드, 재현 해시, 결정적 산출물, 게이트 평가 경계를 고정한다. H100은 열 개 게이트를 모두 통과해야 하는 AND-gate이며, 이 계측 단계는 현재 게임이 게이트를 통과했다고 선언하는 단계가 아니다.

## 소유 경계

| 경계 | 책임 | 금지 |
| --- | --- | --- |
| `SM.Combat` | 전투 상태, 결과, 활동 telemetry, canonical state hash의 authoritative truth | H100 보고서나 파일 출력 소유 |
| `SM.HeadlessMetrics` | 전투·캠페인 레코드, 순수 projection, replay hash 조합, 결정적 JSONL/CSV, 게이트 평가 | `SM.Unity`, authored content, session, persistence, editor API 참조 |
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

## 재현 해시

`ReplayHash`는 새 전투 상태 정규화를 만들지 않는다. 기존 `BattleStateCanonicalHash.Compute(finalState)` 결과와 기존 activity telemetry replay hash를 길이-prefix된 UTF-8 바이트로 결합해 `H100ReplayHashV1`을 계산한다.

동일 sim version에서 같은 콘텐츠, seed, 입력과 실행 한도를 사용한 replay group의 모든 `ReplayHash`가 같아야 한다. 불일치는 무결성 게이트 실패다. 이 해시는 cross-platform 결정론을 새로 보증하지 않으며, canonical hash의 현재 보증 범위를 그대로 상속한다.

## 결정적 산출물

기본 출력 위치는 `Logs/h100-metrics/`이고 다음 파일을 생성한다.

- `battle-metrics.jsonl`
- `campaign-metrics.jsonl`
- `gate-report.json`
- `run-manifest.json`
- 선택적으로 `battle-metrics.csv`, `campaign-metrics.csv`

직렬화는 invariant culture, snake_case, 고정 property 순서를 사용한다. 레코드는 stable id 순서로, rule/count 컬렉션은 ordinal id 순서로 정렬한다. UTF-8 BOM과 실행 시각을 데이터 파일에 넣지 않는다. manifest hash는 정렬된 replay hash 열에서 계산한다.

## 게이트 평가

`h100-gates-v1.json`은 H100 Q1의 열 개 게이트와 v1 권고 임계치를 보존한다. 기준선 측정 뒤 인증 holdout을 열기 전에 한 번만 조정할 수 있고, 인증 데이터를 확인한 뒤 임계치를 변경하지 않는다.

`H100GateEvaluator`는 레코드에서 계산 가능한 관측치를 집계하고, blind review나 외부 severity처럼 별도 절차가 소유한 관측치는 explicit external observation으로만 받는다. 필요한 metric이 없으면 `metric unavailable`로 fail closed한다. 작은 smoke corpus나 Stage 1 runner 산출물이 전체 H100 통과를 주장해서는 안 된다.

## 실행과 검증

축소 witness는 다음 명령으로 실행한다.

```powershell
pwsh -File tools/h100-metrics.ps1 -BattleCount 4 -CampaignCount 1 -ReplayCopies 2
```

대량 실행은 같은 명령에서 수를 명시한다. `BattleCount`는 같은 입력을 반복하는 replay group 수이며 실제 battle record 수는 replay copy 수만큼 증가한다.

```powershell
pwsh -File tools/h100-metrics.ps1 -BattleCount 10000 -CampaignCount 10000 -ReplayCopies 2 -CampaignSiteSafety 32
```

wrapper는 Unity batch execute-method로 실제 content/session/simulator 경로를 실행한 뒤 필수 산출물 존재와 `replay_hash_match_rate == 1.0`을 확인한다. 전체 게이트는 아직 측정되지 않은 외부·paired·blind 지표 때문에 실패할 수 있으며, 이는 runner 실패와 구분한다.

코드 변경의 기본 검증 순서는 다음과 같다.

```powershell
pwsh -File tools/unity-bridge.ps1 test-batch-fast
pwsh -File tools/test-harness-lint.ps1 -RepoRoot .
pwsh -File tools/h100-metrics.ps1 -BattleCount 4 -CampaignCount 1 -ReplayCopies 2
```

실제 콘텐츠 runner는 Unity/editor 경계를 밟으므로 pure `FastUnit`만으로 실행 증거를 대체하지 않는다.

## 현재 한계와 후속

- `IPlaythroughDecisionPolicy`와 `ScriptedPlaythroughPolicy`는 현재 `SM.Tests.FastUnit`에 있고 `SM.Unity` session 타입을 소비한다. production asmdef가 test asmdef를 참조할 수 없으므로 Stage 1 runner는 같은 player-visible deterministic 선택 규칙을 editor validation 내부에서 사용한다. 정책 비교와 paired rollout을 시작하기 전에 production-safe policy port를 별도 설계한다.
- tagged/subsystem RNG stream이 없어 공용 RNG를 보존하는 counterfactual ablation을 아직 보증하지 않는다.
- `SM.HeadlessMetrics` 자체는 pure .NET으로 빌드 가능하지만 실제 콘텐츠/session composition은 Unity adapter에 남아 있다. 2M+ 전투용 pure dotnet CLI는 content snapshot과 campaign orchestration port를 분리한 뒤 추가한다.
