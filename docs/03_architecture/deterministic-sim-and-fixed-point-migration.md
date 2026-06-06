# 결정론 sim과 고정소수점 마이그레이션 계획 (v2)

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-06-07
- 소스오브트루스: `docs/03_architecture/deterministic-sim-and-fixed-point-migration.md`
- 관련문서:
  - `docs/04_decisions/adr-0029-deterministic-fixed-point-sim.md` (본 계획의 결정 근거)
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/03_architecture/combat-state-and-event-model.md`
  - `docs/03_architecture/replay-persistence-and-run-audit.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `TESTING.md`

## 목적

`SM.Combat` 런타임 sim을 float에서 결정론적 고정소수점으로 옮기는 **determinism contract**와 **단계별 실행 계획**을 정의한다. 결정 근거·기각 대안은 ADR-0029. 이 문서는 구현이 반복 참조하는 살아있는 계약이다.

> **핵심 원칙**: 이 작업은 *숫자 타입 교체*가 아니라 *authoritative dataflow + canonical ordering 폐쇄*다. Fixed 타입 도입만으로는 결정론이 오지 않는다. 아래 다섯 축이 **같이** 닫혀야 cross-platform bit-exact가 성립한다: ① numeric(고정소수점·범위·반올림) ② ordering(tie-break·결정적 순회) ③ time authority(정수 틱) ④ boundary(ingress/egress 단방향) ⑤ hash canonicalization.

## 1. 결정론 계약 (다섯 축)

1. **Numeric** — 모든 sim 트루스는 정수 backing fixed-point. 도메인별 타입 분리(§2). 모든 손실 연산은 단일 반올림 규칙. 오버플로는 범위 예산(§2)으로 관리.
2. **Ordering** — 모든 authoritative `OrderBy`에 stable tie-break. `Dictionary`/`HashSet` 순회 결과를 의사결정·합산에 직접 쓰지 않음(정렬 배열 + 명시 loop).
3. **Time authority** — 정수 틱이 권위. authored float duration은 진입 시 틱으로 변환(`DurationToTicks`, §3). `ElapsedSeconds`는 `StepIndex` 파생 display-only.
4. **Boundary** — ingress는 콘텐츠 float를 단 한 번 양자화(런타임 재파싱 금지, 리플레이는 raw fixed 스냅샷). egress는 `Fixed`→float 불변 projection(read-model을 sim에 되먹이지 않음). 컴파일/ lint로 강제(§4).
5. **Hash canonicalization** — 정준 상태 해시는 raw 정수 필드를 고정 순서·고정 endian으로 먹는다(§6). read-model float·telemetry·display time 제외. `GetHashCode`/reflection/JSON 순서 금지.

## 2. 수치 포맷 spec

범용 `FixedWide`를 만들지 **않는다**. 이 Unity/C#(C# 9, `System.Int128` 부재)에서 Q16.16/Int64 `(long)a*b>>16`은 중간 곱이 128-bit를 요구해 오버플로(deterministic 쓰레기값)한다. 따라서 도메인 타입으로 연산을 닫는다.

| 타입 | backing | 용도 | 허용 연산 |
| --- | --- | --- | --- |
| `Fixed32` | Q16.16 / Int32 (`long` 중간값) | 좌표·거리·정규화 벡터·작은 배수·퍼센트·각 분수 | `Fixed32 × Fixed32`, `±`, 비교, `Abs/Min/Max/Sign/Clamp` |
| `Score64` | Q16.16 또는 scaled / Int64 | positioning 스코어 누산·정렬·tie-break 전용 | `±`, 비교, `Score64.FromFixed(Fixed32)`; 곱은 `Fixed32` 결과를 widen해 누산 |
| `Hp64`/`Resource64` | Q16.16 또는 정수 / Int64 | HP·barrier·energy·damage 규모값 | `Wide × Fixed32`, `±`, 비교. **`Wide × Wide` 금지** |

- **반올림 통일**: C# 산술 우시프트(`>>`)는 음수에서 floor(−∞), 정수 나눗셈(`/`)은 trunc-toward-zero. `Mul`(시프트)과 `Div`(나눗셈)가 음수에서 불일치한다. **한 규칙으로 통일**하고 helper로 강제:

  ```text
  ShiftRightTrunc(value, bits): value>=0 ? value>>bits : -((-value)>>bits)
  Mul(a,b): FromRaw(ShiftRightTrunc((long)a.Raw*b.Raw, 16))
  ```

  좌표 ±8이라 음수 연산이 일상 → 누적 bias 방지. 음수 `Mul`/`Div`, `-a*b == -(a*b)` 대칭, `Abs`/`Normalize` 대칭을 테스트로 고정.
- **오버플로 정책**: dev 빌드는 checked/assert, release는 정책 명시(saturate/error/wrap 중 택1). div-by-zero 처리(assert/clamp/exception) 명시.
- **범위 예산**(Phase 0에서 수치 확정): max coordinate/map radius, max speed/knockback/teleport, max overlap penalty·nav score·candidate count, max HP/barrier/damage, max crit/incoming/focus/more multiplier, max modifier stack, max battle ticks.
- **정밀도는 "충분"이 아니라 검증 대상**: Q16.16 해상도 ≈ 1.5e-5. `0.1*65536=6553.6`(비정수) → 틱당 ±~6e-6, 1000틱 ±~0.006. 좌표 범위가 좁으니 **좌표만 Q12.20/Q8.24**로 fractional bit를 늘리는 선택지를 Phase 0/1에서 평가. 필요 시 이동 residual accumulator(유닛별 적분 나머지 보존). "1000틱 이동 drift / range threshold 근방 / tiny-vector normalize" 테스트로 못박는다.

## 3. 수학 함수 전략

| 연산 | 전략 |
| --- | --- |
| `sqrt` | 정수 isqrt(이진 자리/Newton). Q16.16은 `isqrt(v<<16)`. 정확·결정론. (※ `sqrt`는 IEEE 기본 연산이라 초월함수와 다른 범주지만, A에서는 어차피 isqrt로 치환해 호출경로 보증 불확실성을 제거.) |
| `Normalize` | `NormalizeOrFallback(v, fallbackKey)`: `lenSq <= NormalizeEpsSq`면 `HashDirection(fallbackKey)`, 아니면 `v / Sqrt(lenSq)`. **모든 normalize 호출이 이 helper 경유**(near-coincident 벡터가 양자화 노이즈를 방향 결정으로 증폭하는 것 차단 — "좌표 완전 일치"만 처리하면 부족). `NormalizeEpsSq`는 raw 기준 고정. |
| `sin/cos` | **커밋된 raw int turn-LUT**(예 4096 엔트리) + `Fixed32` 선형보간. 각은 **integer turn 분수**로 표현(인덱스 = turn × N, 정수) — 런타임 degrees/radians/π/`MathF` 변환 금지. cos는 인덱스 +N/4. LUT 생성기는 editor/tool 전용, player build 실행 금지, table hash 테스트로 고정(macOS/Windows 재생성 시 마지막 bit 차이 방지). 현 각도 표면(넉백 회전·separation fallback·slot ring)은 4096 nearest로도 충분할 수 있음. |
| `pow(0.75, int)` | 정수 거듭제곱 루프 또는 소형 LUT(0..N). 정확. |
| `abs/min/max/sign/clamp` | 정수 그대로 정확. 기계적 치환. |
| `log/floor/ceil` | 텔레메트리·분석만 → float 유지(sim 트루스 아님). |

`DurationToTicks` spec(Phase 2 time authority 전 고정):

```text
positive duration -> max(1, ceil(seconds*TickRate - eps))   // 0틱 금지(명시 Instant 제외)
0틱 windup/cooldown/status semantics 명시(같은 틱 다단공격 방지)
status가 적용 틱을 tick 0으로 셀지 tick 1로 셀지 명시
resolve/recovery transition convention 명시
골든 테이블: 0, 0.001, dt/2-eps, dt/2, dt-eps, dt, dt+eps, 2dt, 10dt
```

현 `BattleWindupTickMath`가 float 감산을 흉내내는 관측 채널이므로, **기존 동작 보존을 원하면 이 함수 case table을 먼저 추출**해 권위 규칙으로 옮긴다. (cadence는 DPS/CC lock 밸런스 직결 — 1틱 차이가 크다.)

## 4. 경계 정의

**Fixed 전환(런타임 sim 트루스)**: `SM.Core` 신규 `Fixed32`/`Score64`/`Hp64`/`FixedMath`/sin-LUT; `CombatVector2`(→ 병행 `FixedVector2`, §5 Phase 3); `UnitSnapshot` 좌표·HP/Energy/Barrier·타이머(→틱); `BattleState` 시간(→정수 틱); `FloatRange→FixedRange`; Footprint/Behavior 수치; `StatBlock`(정수 + 결정적 순서); `MovementResolver`/`EngagementSlotService`/`TargetScoringService`/`HitResolutionService`/`CombatActionResolver`/`RoleBrain`/`BattleFactory`.

**float 유지(경계 밖)**:
- **Ingress**: 콘텐츠 에셋 저작값. `BattleFactory`/loadout 컴파일에서 **단 한 번** 양자화. 리플레이 input 스냅샷은 **raw fixed**(또는 baked manifest hash) 저장 — 런타임에 저작 float 재파싱 금지. culture-invariant decimal parsing은 tool/import 단계 한정.
- **Egress**: `BattleReadModelBuilder` 이후 전부. read-model 좌표는 float로 노출(불변 projection). `SM.Unity`(BattleScreenController/Timeline/Presentation/ActorView/LocomotionCadence/HumanoidAnimationDriver) **무수정**.
- **Telemetry/분석/리포트**: float 유지. **단 sim branch 입력으로 되먹이기 금지**(테스트로 검증).

**컴파일 강제**:
- asmdef 분리(authoritative ↔ read-model projection) 또는 lint: authoritative sim 메서드 파라미터에 `float`/`double`/`decimal`/`MathF`/`Mathf`/`Vector2`/read-model 타입 금지; `SM.Combat`에 `UnityEngine` 참조 금지; telemetry/analytics 값 sim 입력 금지; `BattleState.ElapsedSeconds`는 `StepIndex` 파생 display-only property.

## 5. 단계별 계획 (각 단계 = main 빌드+테스트 green)

> cross-platform 보장은 마지막 경계 폐쇄 후에만 성립한다. 중간 단계 합격선 = "same-binary 무회귀 + 빌드 green". **단 cross-platform tooling은 Phase 0부터 가동**한다(아래). 수치를 의도적으로 바꾸는 단계는 "완료 정의"에 baseline 골든 재기록 1회를 포함한다.

- **Phase 0 — Determinism contract + harness (관측 가능성 우선, 행동 변화 0)**
  - authoritative 입력/상태/출력 경계 문서, float 금지 lint, read-model feedback 금지 규칙, canonical binary hash writer skeleton.
  - **cross-platform runner skeleton(Mono Editor + x86_64 IL2CPP + ARM64 IL2CPP)** + 현 float sim도 hash dump 가능하게. (지금 mismatch 나도 됨 — 목표는 green이 아니라 관측 가능성. 여기서 ARM runner를 못 만들면 후속 "x86/ARM golden"은 희망사항이다.)
- **Phase 1 — Numeric foundation** (`SM.Core`)
  - `Fixed32`(Q16.16; 좌표용 Q12.20/Q8.24 분리 여부 결정), `Score64`, `Hp64`/`Resource64`, `FixedMath`(isqrt·SinTurns/CosTurns·PowInt·ShiftRightTrunc), `FixedSinLut`(커밋 raw table + table hash). **범용 `Wide×Wide` API 미제공.** 음수 rounding·overflow·div0 정책 확정. sim 미사용. EditMode 테스트(라운드트립·사칙 known-value·Sqrt 완전제곱/단조·Sin 대칭·`sin²+cos²≈1`·음수 대칭·PowInt·오버플로). FixedMath 마이크로테스트는 Mono + IL2CPP x86 + IL2CPP ARM에서 즉시 돌린다.
- **Phase 2 — Stat/RNG/time authority 먼저** (공간보다 선행)
  - `StatBlock` modifier를 정렬 결정적 배열로(Dictionary/HashSet/LINQ reduction 제거); RNG roll을 정수 threshold 비교로; `ActionTimerRemaining`/`CooldownRemaining`/상태 `RemainingSeconds`를 틱 카운트로; `BattleState.ElapsedSeconds` 제거 또는 derived; `DurationToTicks` 골든 테이블; 모든 ordering에 stable tie-break.
  - 근거: `MoveSpeed`/`AttackRange`는 매 틱 `StatBlock.Get` 재평가(`Dictionary.Sum` 순서의존)이고 resolve 게이트가 float 타이머다. 공간을 먼저 하면 movement 입력이 float-stat shim이라, 이 단계에서 모든 공간 골든이 다시 깨져 Phase 1 산출물이 폐기된다. **time authority를 먼저 올린다.**
- **Phase 3 — Spatial migration in slices**
  - **`CombatVector2`를 즉시 갈아엎지 않는다**(호출부 수백 개 동시 붕괴 → 개발자가 implicit Fixed↔float 변환 삽입 → 경계 재누수). 신규 `FixedVector2`/`CombatPosition`/`CombatDirection` 병행 도입, **implicit 변환 금지**, 명시 함수(`QuantizeCombatVector2`/`ToReadModelVector2`)만. position/range/distance, `NormalizeOrFallback` 통일, sin/cos integer-turn 전환, `MovementResolver` 스코어 `Score64` 누산, speed ordering tie-break.
- **Phase 4 — Damage / HP / resources**
  - `HitResolutionService` 고정소수점화. mitigation rounding/clamp 위치 명시, `Max(1, ...)` min-damage 단위 정의(1 raw? 1.0 HP?), crit/incoming/focus multiplier order 고정. HP/Barrier/Energy 도메인 타입 전환. damage distribution regression.
- **Phase 5 — Boundary closure**
  - `BattleFactory`/loadout compile을 유일 ingress로; 리플레이 input 스냅샷에 raw fixed 저장(또는 baked manifest hash); `BattleReadModelBuilder` 이후만 float; telemetry→sim feedback 부재 테스트; `SM.Combat` 잔여 float/double/MathF/Mathf scan; 구 shim 삭제. ("잔여 float 제거"보다 "양방향 dataflow 차단"을 더 강하게 본다.)
- **Phase 6 — Cross-platform golden hardening**
  - canonical `StateHash`/`FinalStateHash`/Keyframe StateHash 확정; tick-level divergence dump; seed corpus(short/long/dense collision/many modifiers/tiny durations/boundary HP/tie-heavy); backend matrix(Editor Mono smoke / Desktop x86_64 IL2CPP shipping / Android·iOS ARM64 IL2CPP shipping); `SimVersion` bump; 구 리플레이 비호환 게이트. Phase 0의 runner를 hard gate로 승격.

## 6. 검증 강화

- **Canonical hash spec**(API처럼 문서화):

  ```text
  CanonicalStateHashV1:
    endian: little-endian 명시
    units: stable UnitId 오름차순
    per unit: position raw, hp raw, energy raw, action state enum, target id, timers/ticks, statuses(StatusId+SourceId 정렬)
    queues: event tick 오름차순, priority, stable sequence id
    rng: seed/context counters 또는 stateless context 필드
    config: tick rate, sim version, content hash
    제외: read-model float, telemetry, display time, localized strings
    금지: object.GetHashCode, string.GetHashCode, reflection field order, JSON property order
  ```

  raw 값이 같아도 pending event/HashSet 순서가 다르면 다음 틱 적용 순서가 갈려 200틱 뒤 final hash가 분기한다 → queue/status ordering까지 정준화 대상.
- **Backend matrix**: 단순 "ARM" 불충분. Editor Mono / Standalone·Desktop x86_64 IL2CPP / Android·iOS ARM64 IL2CPP / Release·Master config는 서로 다른 실행물. ARM CI 우선순위: ① 실기 device farm/사내 배치 ② Apple Silicon macOS ARM64 IL2CPP smoke ③ QEMU는 보조(shipping oracle 금지).
- **Tick-level divergence**: final hash만으론 부족. N틱마다 keyframe hash + 분기 시 first-bad-tick 이분탐색 + per-system digest. 현 `BattleKeyframeDigest` scaffold 재사용으로 확장 비용 낮음.
- **회귀 가드 lint**: `tools/smoke-check.ps1` 또는 `tools/test-harness-lint.ps1`에 "authoritative `SM.Combat`에 `float`/`double`/`MathF`/`Mathf`/`Vector2` 재유입 시 실패"(egress 빌더·telemetry 화이트리스트). hash writer는 손작성/source-generated(reflection·IL2CPP stripping 회피).
- **리베이스라인**: 수치 변경 단계마다 baseline 1회 재기록, `SimVersion` bump로 구 리플레이 명시 단절.

### Cross-platform 골든 워크플로우 (Phase 0 tooling)

Phase 0에서 깐 harness 도구:

- `tools/combat-determinism-lint.ps1` — authoritative `SM.Combat`의 float/MathF 표면 스캔(inventory + `-Strict` gate). baseline 470 hits / 33 files.
- `BattleStateCanonicalHash`(SM.Combat) — `CanonicalStateHashV1` raw-field digest. `BattleHashCorpus`가 seed corpus를 끝까지 돌려 keyframe·final hash 텍스트를 만든다.
- `BattleHashCorpusDumpEntry`(SM.Editor, `-executeMethod`) — backend별 corpus 파일 덤프.
- `tools/hash-corpus-check.ps1` — `-Produce -Backend <tag>`(batch 덤프) / `-Golden <a> -Candidate <b>`(first-divergence 비교).

backend matrix 실행(CI/디바이스):

1. 각 backend에서 `-Produce`: Editor Mono / Windows x86_64 IL2CPP / Android·iOS ARM64 IL2CPP → `TestResults/hash-corpus-<backend>.txt`.
2. backend 쌍을 `-Golden`/`-Candidate`로 비교 → 불일치 시 첫 divergent seed/step 출력(desync 위치).
3. **현 float sim은 same-binary만 일치**(cross-platform 불일치 예상) — 이는 Phase 0 관측이 정상 작동한다는 신호다. cross-platform MATCH는 Fixed 마이그레이션 완료(Phase 5) 후 성립하며, 그때 golden을 commit해 Phase 6 hard gate로 승격한다.

> `-Produce`(batchmode)는 열린 GUI 에디터와 프로젝트 락이 충돌한다 — CI 또는 에디터를 닫은 세션에서 실행한다.

## 7. 회귀 위험 + 대응

| 위험 | 대응 |
| --- | --- |
| 밸런스 시프트(양자화) | 1회성 재튜닝 수용. 프로토타입=콘텐츠 적어 비용 낮음. sim 배치로 분포 재확인 |
| `FixedWide` 범용 곱 오버플로 | 범용 Wide 미제공. `Hp64×Fixed32`만 허용, `Wide×Wide` 금지(§2) |
| 음수 rounding 불일치(`>>` vs `/`) | `ShiftRightTrunc`로 통일 + 음수 대칭 테스트(§2) |
| 스코어 합산 오버플로(overlap²*12, nav*420) | `Score64` 누산. Phase 1/3 범위 감사 |
| `StatBlock` 합산 순서 의존 | Phase 2에서 정렬 배열로(현존 잠재 버그) |
| 시간 양자화로 cadence 변화 | `DurationToTicks` + 골든 테이블 선고정, DPS/CC drift 테스트 |
| tiny-vector normalize 증폭 | `NormalizeOrFallback` 전면 적용 |
| LUT 플랫폼별 재생성 차이 | LUT raw table 커밋 + table hash, generator는 tool 전용 |
| ordering tie 증가 | 모든 authoritative `OrderBy`에 stable tie-break, Dictionary 순회 의사결정 금지 |
| ingress 런타임 재파싱 누수 | 리플레이 raw fixed 스냅샷 / build bake |
| 구 리플레이 무효화 | `SimVersion` 게이트 |
| (범위 밖) `SM.Meta` `System.Random` | 후속: 자체 정수 PRNG(PCG/xoshiro) 또는 결과 raw 스냅샷 |
| Unity 함정 | `Vector2`/`Mathf`/`Time`/`AnimationCurve.Evaluate` 우발 사용 lint 차단; `decimal`/`double`은 tool만; LINQ reduction 제거; hash writer reflection 금지 |

## 8. Phase 0 결정 (locked) — Phase 1 착수 전 확정

§1~§7 계약을 구현 가능한 구체값으로 확정한다. 범위 예산은 실제 코드 수치(arena ±8/±3.2, `maxSteps` 300@10Hz, `StatKey` 표면)로 grounding했다. balance 가정은 맨 끝 "오너 컨펌 대기"로 분리.

### Numeric

- **`Fixed32` = Q16.16 / Int32**, `long` 중간값. **좌표 단일 Q16.16 확정**(Q8.24 분리 불요) — Phase 1 drift 테스트(`FixedFormatDriftTests`: 1000틱 누적·range threshold lenSq·tiny-vector normalize) **전부 통과**(2026-06-07): ① 고정소수 덧셈 누적 drift 0(1000틱 == step×1000 비트 동일), ② 1000틱 precision drift < 0.02, ③ lenSq 사거리 판정이 arena 스케일에서 0.01단위 거리차까지 분해, ④ normalize는 L ≥ 0.02에서 길이오차 < 4%, 단 성분 < ~0.004(raw 256)에서 성분² underflow로 lenSq=0. → Phase 3 `NormalizeOrFallback`은 `lenSq==0`(또는 < ε²) floor에서 기본 방향 반환 필수. 근거: 좌표 ±8·거리² ≤ ~290·배수 O(0.25–4) 전부 ±32768에 큰 여유.
- **반올림 = truncate-toward-zero**, 전 연산(`Mul`/`Div`/`Sqrt`/normalize) 공유. C# 산술 우시프트(floor)와 정수 나눗셈(trunc) 불일치는 `ShiftRightTrunc(v,bits)=v>=0?v>>bits:-((-v)>>bits)`로 통일. `-a*b == -(a*b)` 대칭을 테스트로 고정.
- **overflow policy**: dev/test = `checked`(assert), release = **saturate**(클램프) + 범위 예산 위반 로그. wrap 금지(deterministic이어도 게임 파괴).
- **div-by-zero**: 0 반환(현 `CombatVector2.operator/`가 이미 0 반환 — 동작 보존). normalize는 `NormalizeOrFallback`로 별도 처리.

도메인 타입 3종 — 범용 `FixedWide`는 만들지 않는다(Int128 부재로 `Wide×Wide` 오버플로):

| 타입 | backing | 허용 연산 |
| --- | --- | --- |
| `Fixed32` | Q16.16 / Int32 | `Fixed32 × Fixed32`, ±, 비교, Abs/Min/Max/Sign/Clamp, Sqrt, Normalize |
| `Score64` | Q16.16 / Int64 | ±, 비교, `FromFixed(Fixed32)`. 곱은 Fixed32에서 끝낸 뒤 widen 누산 |
| `Hp64`/`Resource64` | Q16.16 / Int64 | `Wide × Fixed32`, ±, 비교. **`Wide × Wide` 금지** |

### Range budget (확정값 + 포맷 검증)

| 축 | 값 | 포맷 적합성 |
| --- | --- | --- |
| coordinate | X ±8, Y ±3.2 (`ArenaHalfWidth/Height`) | `Fixed32` ✓ (±32768) |
| distance / lenSq | ≤ ~17 / ≤ ~290 | `Fixed32` ✓ |
| per-tick 변위 | speed×0.1 ≤ ~1 | `Fixed32` ✓ |
| multiplier (crit/incoming/focus/more) | ~0.25–4 | `Fixed32` ✓ |
| positioning score | overlap²×12 + nav×420, 누산 ≤ ~10⁴–10⁵ | **`Score64` 필수**(Int32 초과) |
| HP/barrier/energy/damage | ceiling ≤ 1,000,000 (오너 가정) | **`Hp64`**. `Hp64(≤6.6e10 raw) × Fixed32(mult≤4 → 2.6e5 raw) = 1.7e16 < Int64 9.2e18` → Int128 불필요 ✓ |
| modifier stack / battle ticks | ≤ ~32 / ≤ ~400 (`maxSteps` 300, P90≤38s) | int ✓ |

### Time

- **TickRate = 10 (정수 SoT)**. `FixedStepSeconds`(0.1)는 display-derived(틱→초 표시용)로만 잔존.
- `DurationToTicks(sec) = max(1, ceil(sec*10 - 1e-4))` — positive duration은 **최소 1틱**, 0틱은 명시 `Instant` 플래그에만(같은 틱 다단공격 차단).
- resolve 규약: `contactTick = windupStartTick + windupTicks`(다음 step contact), recovery는 `cooldownTicks` 동안 `Recover`. 현 `BattleWindupTickMath.StepsUntilResolve` case table을 Phase 2에서 추출해 권위로 승격.
- 골든 테이블 입력(sec): `0, 0.001, 0.05, 0.1, 0.11, 0.2, 1.0, 3.0`.

### LUT / angle

- **sin/cos = 4096-entry turn-LUT, raw int 상수로 repository 커밋**. 생성기는 editor/tool 전용(player build 실행 금지), `LUT table hash` 테스트로 고정.
- **angle = `AngleTurn32`(uint, 전체 turn = 2³²)**: 인덱스 = 상위 12bit, 보간 = 하위 fraction. 런타임 degrees/radians/π/`MathF` 금지. authored degree는 ingress에서 turn으로 bake.

### Ingress

- `Fixed32.FromFloatQuantized`는 **ingress/tool 전용**(sim 내부 호출 금지, lint 대상).
- **리플레이 = raw fixed loadout snapshot** 저장(저작 float 재파싱 금지). `BattleReplayHeader`의 `ContentVersion`/content hash 활용.
- culture-invariant decimal parsing은 import/bake 단계 한정.

### Egress

- read-model float = 불변 projection. authoritative 코드의 read-model namespace 참조 금지(asmdef/lint).
- telemetry/analytics 값 sim branch 입력 금지(테스트 검증). `BattleState.ElapsedSeconds`는 `StepIndex` 파생 display-only property.

### Ordering (stable tie-break 의무)

- speed order: `speed desc → UnitId asc(ordinal)`
- target score: `score → 동점 시 UnitId asc`
- event queue: `tick → priority → stable sequence id`
- modifier order: `stat key → op phase(Flat→Increased→More→Clamp) → source id → insertion sequence`
- authoritative 경로에서 `Dictionary`/`HashSet` 순회 결과 직접 사용 금지(정렬 배열로).

### Hash

- **`CanonicalStateHashV1`**(Phase 0 #2 구현 완료: LE, UnitId asc, status `StatusId` 정렬, UTF-8, float raw bits→FNV-1a 64). Phase 2~4에서 **event/effect queue + RNG context**를 동일 규약으로 확장하고, Phase 1+에서 float raw → Fixed raw로 교체.
- 제외: read-model float·telemetry·display time. 금지: `GetHashCode`/reflection/JSON 순서.

### CI

- Unity **6000.4.7f1** 고정. backend runner: Editor Mono / Windows x86_64 IL2CPP / Android·iOS ARM64 IL2CPP (shipping = Release·Master).
- failure artifact: first divergent tick, per-unit digest, replay input snapshot.

### 오너 컨펌 대기 (balance 가정)

- **HP/데미지/리소스 ceiling = 1,000,000** — `Hp64` 사이징 근거. 로그라이트 스케일이 이를 넘을 의도면 알려줄 것(>1e7면 포맷 재검토). 그 외엔 이 값으로 진행.
- **TickRate 10Hz 유지** — 현 sim과 동일. 변경 의도 없으면 확정.
- (참고) Fixed32 단일 vs 좌표 Q8.24 분리는 Phase 1 drift 테스트 결과로 판정 — 지금은 단일 Q16.16로 확정.

## 우선순위 요약

지금 바로 닫아야 할 determinism 채무 우선순위: **① StatBlock ordering ② integer time authority ③ RNG threshold 정수화 ④ fixed math spec(Phase 0/1) ⑤ cross-platform runner(Phase 0)**. Spatial `CombatVector2` 대수술은 그 다음(Phase 3)이다. "Fixed 타입 만들고 float 지우기"를 1단계로 두지 않는다.
