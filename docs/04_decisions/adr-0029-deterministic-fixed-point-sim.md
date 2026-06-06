# ADR-0029 결정론적 고정소수점 sim — float→fixed 마이그레이션 (approach A)

- 상태: active
- 소유자: repository
- 최종수정일: 2026-06-07
- 소스오브트루스: `docs/04_decisions/adr-0029-deterministic-fixed-point-sim.md`
- 관련문서:
  - `docs/03_architecture/deterministic-sim-and-fixed-point-migration.md` (본 결정의 determinism contract + 단계별 실행 계획)
  - `docs/04_decisions/adr-0006-combat-sim-boundary.md` (전투 sim 경계 — 본 ADR이 그 위에 수치 결정론을 얹는다)
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/03_architecture/replay-persistence-and-run-audit.md`
  - `docs/03_architecture/combat-runtime-architecture.md`

## 문맥

진단(2026-06-07) 결론: `SM.Combat` sim 레이어가 **전부 float 시간·float 좌표·float 데미지**로 굴러간다. 검증된 결정론은 `BattleDeterminismBaselineTests`의 **"같은 바이너리/같은 프로세스 2회 재실행"** 한정이다(직렬화도 `float.ToString("R")`). 이 게임은 **PC(x86) + Mobile(ARM) 동시 scope**이고 로그라이트 **input-replay(seed+입력 스냅샷 재시뮬)**가 핵심 콘텐츠라, float 잔존부의 cross-platform 재생 발산이 실제 위험이다.

float이 cross-platform에서 깨지는 경로: FP contraction(IL2CPP→C++→clang은 C# 소스 수준에서 이식 가능한 contraction 통제 수단이 없다), 정확반올림 비보장 초월함수(`sin/cos/pow/log`)의 libm 차이, 그리고 `StatBlock.Get`의 `Dictionary.Sum()` 열거-순서 의존(런타임 간 불변 아님). 단 `+ − × ÷ sqrt`는 IEEE-754 정확반올림 의무 연산이므로 초월함수와 다른 범주다(이 구분은 GPT Pro 검수에서 교정됨 — 위험은 `sqrt` 자체가 아니라 주변 곱·정규화·threshold·MathF 호출경로의 제품 보증 불가에 있다).

본 결정은 외부 검수(GPT Pro, 2026-06-07, Pro·확장 모드)를 반영한 v2다. 핵심 재정의: **이 작업은 "숫자 타입 교체"가 아니라 "authoritative dataflow + canonical ordering 폐쇄" 프로젝트다.** Fixed 타입을 만들고 float를 지우는 것만으로는 결정론이 오지 않는다 — ordering·overflow·quantization·boundary feedback·hash canonicalization이 같이 닫혀야 한다.

## 결정

- **Approach A 채택**: 결정론적 고정소수점 **런타임 sim**. 두 경계를 둔다.
  - **Ingress(입력) 경계**: 콘텐츠/스탯은 float 에셋으로 저작하되, **빌드/에디터 bake 또는 전투 시작 직전 단 한 번** `Fixed`로 양자화한다. **리플레이 loadout 스냅샷은 저작 float가 아니라 raw fixed를 저장**한다(런타임 float 재파싱 금지 — 그러면 platform boundary 위로 돌아간다).
  - **Egress(출력) 경계**: `BattleReadModelBuilder`가 `Fixed` sim 상태 → float read-model로 변환한다. 연출 레이어(`SM.Unity`)는 무수정. read-model float는 **불변 projection**이며 sim 의사결정으로 절대 되먹이지 않는다.
- **경계는 prose가 아니라 컴파일로 강제**한다: asmdef를 `SM.Combat`(authoritative) ↔ read-model projection으로 분리하거나, lint로 "authoritative sim 메서드 파라미터에 `float`/`double`/`decimal`/`MathF`/`Mathf`/`Vector2`/read-model 타입 금지", "telemetry 값 sim 입력 금지"를 박는다. `BattleState.ElapsedSeconds`는 저장 필드가 아니라 `StepIndex` 파생 **display-only property**다.
- **수치 포맷은 도메인 타입으로 분리**한다(범용 `FixedWide`는 만들지 않는다 — 이 Unity/C#엔 `Int128`이 없어 `(long)a*b>>16` 중간 곱이 오버플로한다):
  - `Fixed32` (Q16.16, Int32 backing, `long` 중간값): 기하·정규화 벡터·작은 배수·퍼센트.
  - `Score64`: positioning 스코어 누산·비교·tie-break 전용(덧셈/비교 중심, `Fixed32` 결과를 widen해 누산).
  - `Hp64`/`Resource64`: HP/barrier/energy/damage 규모값. `Wide × Fixed32`만 허용, `Wide × Wide` 금지.
  - 모든 손실 연산은 **단일 반올림 규칙**을 공유한다(음수 포함 — C# 산술 우시프트(floor)와 정수 나눗셈(trunc-toward-zero) 불일치를 `ShiftRightTrunc` helper로 통일).
- **시간은 정수 틱이 권위**다: `ActionTimerRemaining`/`CooldownRemaining`/상태 `RemainingSeconds`를 **틱 카운트**로 환원하고, resolve 게이트를 float 타이머 비교에서 정수 틱 비교로 승격한다(`BattleWindupTickMath`의 정수 틱을 권위로). authored float duration → 틱 변환 규칙(`DurationToTicks`)과 골든 테이블을 시간화 전에 고정한다.
- **RNG**: 전투 sim RNG는 이미 stateless 정수 해시다. 임계 비교를 float(`roll/10000f < stat`)에서 **정수 공간**(`remainder < thresholdInt`)으로 정리해 float를 완전히 제거한다.
- **`StatBlock` deterministic ordering**: `Dictionary.Where().Sum()` 열거-순서 의존을 정렬된 결정적 배열 + 명시 loop로 교체한다(고정소수점 이전에도 존재하는 잠재 버그).
- **모든 authoritative `OrderBy`에 stable tie-break 의무화**: 양자화가 near-equal float를 동일 raw로 collapse시켜 tie가 늘어난다. 예: speed desc → `ThenBy(UnitId)`. `Dictionary`/`HashSet` 순회 결과를 의사결정에 직접 쓰지 않는다.
- **검증**: same-process `float.ToString("R")` baseline을 **고정소수점 raw 필드 정준 해시**(`StateHash`/`FinalStateHash`/`BattleKeyframeDigest.StateHash` 재정의)로 교체한다. **cross-platform runner는 마지막 단계가 아니라 Phase 0부터** 가동하고, backend matrix는 단순 "ARM"이 아니라 **Editor Mono / x86_64 IL2CPP / Android·iOS ARM64 IL2CPP / shipping(Release·Master) config**를 포함한다. tick-level first-divergence 이분탐색을 지원한다.
- **`SM.Meta`의 `System.Random`**(loot/recruit/refit)은 본 범위 밖이나, 로그라이트 run-reproduction에 영향하므로 별도 후속으로 자체 정수 PRNG 교체 또는 결과 raw 스냅샷을 기록한다.

상세 계약·단계별 실행·Phase 0 체크리스트는 `docs/03_architecture/deterministic-sim-and-fixed-point-migration.md`에 둔다.

## 검토한 대안

| option | description | verdict |
| --- | --- | --- |
| **A. 전면 고정소수점 + ingress/egress 경계** | 런타임 sim 트루스를 Fixed로, 콘텐츠는 float 저작 후 진입 시 양자화, read-model은 float | **채택** — 유일하게 cross-platform bit-exact를 보장. 초월함수 표면이 작아(sqrt/sin/cos 4지점) 알고리즘 위험 낮음 |
| B1. native float discipline | float 유지 + 코드 스타일/컴파일 옵션/테스트로 결정론 강제 | reject — IL2CPP→clang의 FP contraction·libm을 C# 소스 수준에서 이식 가능하게 통제 불가. "검증 통과"는 운이지 보증이 아님 |
| B2. true software Float32 (Berkeley SoftFloat류) | float 값을 정수로 들고 IEEE Float32 연산을 소프트웨어 구현 | reject — 기술적으로 가능하나 sin/cos/pow 별도 구현·C 포팅·AOT/성능 비용이 크고, 현재 수치 범위·초월함수 표면이 작아 Q-format fixed보다 ROI 낮음 |
| C. 순진한 하이브리드 (좌표만 고정) | 좌표는 Fixed, HP/데미지는 float | reject — 결정론은 전체 시스템 속성. float 데미지→HP→사망→타겟→위치로 발산이 전파되어 부분 고정은 결정론을 못 줌. 단 "콘텐츠 float 저작→진입 시 양자화" 핵심은 A에 흡수 |
| A-lite | 시간/스탯/RNG/분기표면만 정수화 + display float + periodic full-state snapshot 재생 | defer — 리플레이가 관전·공유용일 때만 ROI 우위. 우리는 run-reproduction + QA sim + AI 콘텐츠 검증이 핵심이라 결국 authoritative state 대부분이 hash 대상 → A가 맞음 |
| Photon Quantum 등 deterministic 프레임워크 | fixed-point + replay/rollback infra 제품화 | defer — 네트워크/rollback까지 원하면 검토. 현재 순수 `SM.Combat`만 고치는 데는 과함(엔진 교체 비용) |

## 결과 / 영향

- **SimVersion bump** + baseline 1회 재기록 + 기존 float-sim 리플레이 자산 비호환 처리(`BattleReplayHeader.SimVersion` 게이트). 프로토타입 단계라 무효화 리플레이·튜닝 콘텐츠가 적어 지금이 가장 싼 시점.
- **밸런스 1회 재튜닝**: 양자화로 모든 수치가 미세 변동 → 인카운터 결과 변화. sim 배치로 분포 재확인.
- **`SM.Unity` 연출 레이어 무수정**(egress 경계 덕분). `SM.Combat` 내부와 `SM.Core` 수치 토대, `StatBlock`, 결정론 테스트/하네스가 변경 대상.
- **포지셔닝 휴리스틱 거동은 결정적으로 바뀔 수 있음**(스코어 근소 동점 재정렬) — 허용(여전히 결정론). 트레드밀 진단 + PlayMode 육안으로 확인.
- 본 결정은 코드 직결 architecture(asmdef 경계 + 런타임 sim + replay schema)라 pindoc이 아닌 git ADR에 둔다(decision-location policy 준수).
